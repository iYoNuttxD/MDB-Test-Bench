using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;
using MdbTestBench.Core.Protocol.Frames;
using MdbTestBench.Core.Vmc;
using MdbTestBench.Transport.Abstractions;

namespace MdbTestBench.Transport.Simulation;

public sealed class SimulatedCashlessTransport : IMdbTransport, IRawCommandTransport
{
    private static readonly MdbAddress CashlessAddress = new(0x10, MdbDeviceType.CashlessDevice1);
    private readonly SimulatedCashlessOptions _options;
    private readonly VmcSimulator _vmc;
    private readonly IMdbCashlessEncoder _encoder;
    private readonly IMdbCashlessDecoder _decoder;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public SimulatedCashlessTransport(
        SimulatedCashlessOptions? options = null,
        PollingMode pollingMode = PollingMode.HostManaged,
        VmcSimulator? vmcSimulator = null,
        IMdbCashlessEncoder? encoder = null,
        IMdbCashlessDecoder? decoder = null)
    {
        _options = options ?? new SimulatedCashlessOptions();
        if (_options.ResponseDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Simulator response delay cannot be negative.");
        if (_options.OperationTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Simulator timeout must be greater than zero.");
        _vmc = vmcSimulator ?? new VmcSimulator();
        _encoder = encoder ?? new MdbCashlessEncoder();
        _decoder = decoder ?? new MdbCashlessDecoder();
        Capabilities = new TransportCapabilities
        {
            Name = "Simulated cashless device",
            RequiresPhysicalHardware = false,
            PollingMode = pollingMode,
            SupportedPollingModes = new HashSet<PollingMode>
            {
                PollingMode.AdapterManaged,
                PollingMode.HostManaged
            }
        };
    }

    public bool IsConnected { get; private set; }
    public VmcState State => _vmc.State;
    public VmcSimulator Vmc => _vmc;
    public SimulatorBehavior Behavior => _options.Behavior;

    public bool CanExchange(MdbFrame request)
    {
        if (!IsConnected) return false;
        if (request.Command == MdbCommandType.Expansion) return true;
        if (request.Command == MdbCommandType.Poll && State is VmcState.Reset or VmcState.VendPending) return true;
        if (request.Command == MdbCommandType.Setup && request.Subcommand == MdbSubcommandType.SetupMaxMinPrices)
            return State == VmcState.Disabled;
        if (request.Command == MdbCommandType.Revalue)
            return State == VmcState.SessionIdle;
        return _vmc.CanApply(MapTrigger(request));
    }

    public TransportCapabilities Capabilities { get; }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!IsConnected)
            {
                if (_vmc.State != VmcState.Disconnected) _vmc.Restart();
                _vmc.Apply(VmcTrigger.Connect);
                IsConnected = true;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
            {
                _vmc.Apply(VmcTrigger.Disconnect);
                IsConnected = false;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<MdbFrame> ExchangeAsync(MdbFrame request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsConnected) throw new TransportException(TransportError.Disconnected,
            "The simulated transport is not connected.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_options.OperationTimeout);
            try
            {
                var delay = _options.Behavior == SimulatorBehavior.Timeout
                    ? _options.OperationTimeout + TimeSpan.FromMilliseconds(100)
                    : _options.ResponseDelay;
                await Task.Delay(delay, timeoutSource.Token);
                var decodedRequest = DecodeRequest(request);
                if (decodedRequest.Command is null)
                    throw new TransportException(TransportError.InvalidData,
                        decodedRequest.Error ?? "Invalid MDB command block.");
                var response = _options.Behavior == SimulatorBehavior.UnexpectedResponse
                    ? MdbResponseType.Unknown : Process(request, decodedRequest);
                var responseBytes = EncodeResponse(response);
                if (_options.Behavior == SimulatorBehavior.MalformedResponse)
                    responseBytes = new byte[] { 0x05, 0x00 };
                else if (_options.Behavior == SimulatorBehavior.UnexpectedResponse)
                    responseBytes = MdbCashlessResponseEncoder.EncodeData([0xE0]);
                var responseOptions = new MdbCashlessDecodeOptions(
                    decodedRequest.Command.MinimumFeatureLevel,
                    decodedRequest.Command is MdbRevalueRequestExpandedCommand or MdbSetupMaxMinPricesExpandedCommand);
                var decodedResponse = _decoder.DecodeResponse(responseBytes.Span, responseOptions);
                var cashlessAddress = decodedRequest.Device == MdbCashlessDevice.CashlessDevice2
                    ? new MdbAddress(0x60, MdbDeviceType.CashlessDevice2)
                    : CashlessAddress;
                return new MdbFrame(
                    DateTimeOffset.UtcNow,
                    MdbDirection.Rx,
                    cashlessAddress,
                    request.Source,
                    request.Command,
                    request.Subcommand,
                    decodedResponse.ResponseType,
                    responseBytes,
                    decodedResponse is MdbMalformedCashlessResponse malformed
                        ? malformed.Error : decodedResponse.GetType().Name,
                    CashlessDevice: decodedRequest.Device);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Simulated exchange exceeded {_options.OperationTimeout}.");
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<RawExchangeResult> ExchangeRawAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsConnected) throw new TransportException(TransportError.Disconnected,
            "The simulator is not connected.");
        if (payload.Length > 4_096)
            throw new TransportException(TransportError.InvalidData, "Raw payload exceeds the 4096-byte transport limit.");
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_options.OperationTimeout);
        try
        {
            await Task.Delay(_options.Behavior == SimulatorBehavior.Timeout
                ? _options.OperationTimeout + TimeSpan.FromMilliseconds(100)
                : _options.ResponseDelay, timeoutSource.Token);
            var response = _options.Behavior switch
            {
                SimulatorBehavior.MalformedResponse => new byte[] { 0xF },
                SimulatorBehavior.UnexpectedResponse => new byte[] { 0xDE, 0xAD },
                _ => payload.ToArray()
            };
            return new RawExchangeResult(payload, response, "Simulator raw diagnostic response");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Simulated raw exchange exceeded {_options.OperationTimeout}.");
        }
    }

    private MdbResponseType Process(MdbFrame request, MdbDecodedCommand decodedRequest)
    {
        if (decodedRequest.Command is null)
            throw new TransportException(TransportError.InvalidData, decodedRequest.Error ?? "Invalid MDB command block.");
        var command = decodedRequest.Command;
        return (command.CommandType, command.SubcommandType, State) switch
        {
            (MdbCommandType.Reset, _, not VmcState.Disconnected) => Transition(VmcTrigger.Reset, MdbResponseType.Ack),
            (MdbCommandType.Poll, _, VmcState.Reset) => MdbResponseType.JustReset,
            (MdbCommandType.Setup, MdbSubcommandType.SetupConfig, VmcState.Reset) => Transition(VmcTrigger.SetupComplete, MdbResponseType.ReaderConfigData),
            (MdbCommandType.Setup, MdbSubcommandType.SetupMaxMinPrices, VmcState.Disabled) => MdbResponseType.Ack,
            (MdbCommandType.Reader, MdbSubcommandType.Enable, VmcState.Disabled) => Transition(VmcTrigger.Enable, MdbResponseType.Ack),
            (MdbCommandType.Reader, MdbSubcommandType.Disable, VmcState.Enabled) => Transition(VmcTrigger.Disable, MdbResponseType.Ack),
            (MdbCommandType.Reader, MdbSubcommandType.Cancel, VmcState.SessionIdle) => Transition(VmcTrigger.CancelSession, MdbResponseType.Cancelled),
            (MdbCommandType.Poll, _, VmcState.Enabled) => Transition(VmcTrigger.BeginSession, MdbResponseType.BeginSession),
            (MdbCommandType.Poll, _, VmcState.VendPending) => Transition(VmcTrigger.ApproveVend, MdbResponseType.VendApproved),
            (MdbCommandType.Vend, MdbSubcommandType.VendRequest, VmcState.SessionIdle) => ProcessVendRequest(),
            (MdbCommandType.Vend, MdbSubcommandType.VendCancel, VmcState.VendPending) => Transition(VmcTrigger.CancelVend, MdbResponseType.VendDenied),
            (MdbCommandType.Vend, MdbSubcommandType.VendSuccess, VmcState.VendApproved) => Transition(VmcTrigger.CompleteVend, MdbResponseType.Ack),
            (MdbCommandType.Vend, MdbSubcommandType.VendFailure, VmcState.VendApproved) => Transition(VmcTrigger.FailVend, MdbResponseType.Ack),
            (MdbCommandType.Vend, MdbSubcommandType.SessionComplete, VmcState.SessionComplete) => Transition(VmcTrigger.CompleteSession, MdbResponseType.EndSession),
            (MdbCommandType.Vend, MdbSubcommandType.SessionComplete, VmcState.VendDenied) => Transition(VmcTrigger.CompleteSession, MdbResponseType.EndSession),
            (MdbCommandType.Vend, MdbSubcommandType.SessionComplete, VmcState.SessionIdle) => Transition(VmcTrigger.CompleteSession, MdbResponseType.EndSession),
            (MdbCommandType.Vend, MdbSubcommandType.CashSale, VmcState.Enabled) => MdbResponseType.Ack,
            (MdbCommandType.Revalue, MdbSubcommandType.RevalueRequest, VmcState.SessionIdle) => MdbResponseType.RevalueApproved,
            (MdbCommandType.Revalue, MdbSubcommandType.RevalueLimitRequest, VmcState.SessionIdle) => MdbResponseType.RevalueLimit,
            (MdbCommandType.Expansion, MdbSubcommandType.ExpansionRequestId, _) => MdbResponseType.PeripheralId,
            _ => throw new InvalidVmcTransitionException(State, MapTrigger(request))
        };
    }

    private MdbDecodedCommand DecodeRequest(MdbFrame request)
    {
        var bytes = request.RawBytes.IsEmpty ? EncodeLegacyRequest(request) : request.RawBytes;
        return _decoder.DecodeCommand(bytes.Span);
    }

    private ReadOnlyMemory<byte> EncodeLegacyRequest(MdbFrame frame)
    {
        var device = frame.Destination.DeviceType == MdbDeviceType.CashlessDevice2
            ? MdbCashlessDevice.CashlessDevice2 : MdbCashlessDevice.CashlessDevice1;
        MdbCashlessCommand command = (frame.Command, frame.Subcommand) switch
        {
            (MdbCommandType.Reset, _) => new MdbResetCommand(device),
            (MdbCommandType.Poll, _) => new MdbPollCommand(device),
            (MdbCommandType.Setup, MdbSubcommandType.SetupMaxMinPrices) =>
                new MdbSetupMaxMinPricesCommand(ushort.MaxValue, 0, device),
            (MdbCommandType.Setup, _) => new MdbSetupConfigCommand(MdbFeatureLevel.Level1, 0, 0, 0, device),
            (MdbCommandType.Reader, MdbSubcommandType.Enable) => new MdbReaderEnableCommand(device),
            (MdbCommandType.Reader, MdbSubcommandType.Disable) => new MdbReaderDisableCommand(device),
            (MdbCommandType.Reader, MdbSubcommandType.Cancel) => new MdbReaderCancelCommand(device),
            (MdbCommandType.Vend, MdbSubcommandType.VendRequest) => new MdbVendRequestCommand(500, 1, device),
            (MdbCommandType.Vend, MdbSubcommandType.VendCancel) => new MdbVendCancelCommand(device),
            (MdbCommandType.Vend, MdbSubcommandType.VendSuccess) => new MdbVendSuccessCommand(1, device),
            (MdbCommandType.Vend, MdbSubcommandType.VendFailure) => new MdbVendFailureCommand(device),
            (MdbCommandType.Vend, MdbSubcommandType.SessionComplete) => new MdbSessionCompleteCommand(device),
            (MdbCommandType.Vend, MdbSubcommandType.CashSale) => new MdbCashSaleCommand(500, 1, device),
            (MdbCommandType.Revalue, MdbSubcommandType.RevalueRequest) => new MdbRevalueRequestCommand(100, device),
            (MdbCommandType.Revalue, MdbSubcommandType.RevalueLimitRequest) => new MdbRevalueLimitRequestCommand(device),
            _ => throw new TransportException(TransportError.InvalidData,
                "The simulator requires an encoded MDB block for this semantic command.")
        };
        return _encoder.Encode(command);
    }

    private static ReadOnlyMemory<byte> EncodeResponse(MdbResponseType response) => response switch
    {
        MdbResponseType.Ack => MdbCashlessResponseEncoder.Ack(),
        MdbResponseType.Nak => MdbCashlessResponseEncoder.Nak(),
        MdbResponseType.JustReset => MdbCashlessResponseEncoder.JustReset(),
        MdbResponseType.ReaderConfigData => MdbCashlessResponseEncoder.ReaderConfig(
            MdbFeatureLevel.Level1, MdbPackedBcdCurrencyCode.FromIso4217Numeric(840), 1, 2, 5, 0x0F),
        MdbResponseType.BeginSession => MdbCashlessResponseEncoder.BeginSession(1_000),
        MdbResponseType.VendApproved => MdbCashlessResponseEncoder.VendApproved(500),
        MdbResponseType.VendDenied => MdbCashlessResponseEncoder.VendDenied(),
        MdbResponseType.EndSession => MdbCashlessResponseEncoder.EndSession(),
        MdbResponseType.Cancelled => MdbCashlessResponseEncoder.Cancelled(),
        MdbResponseType.RevalueApproved => MdbCashlessResponseEncoder.RevalueApproved(),
        MdbResponseType.RevalueDenied => MdbCashlessResponseEncoder.RevalueDenied(),
        MdbResponseType.RevalueLimit => MdbCashlessResponseEncoder.EncodeData([0x0F, 0x03, 0xE8]),
        MdbResponseType.PeripheralId => MdbCashlessResponseEncoder.PeripheralId(
            "SIM", "000000000001", "MDBSIMULATOR", 0x0101),
        _ => MdbCashlessResponseEncoder.EncodeData([0xE0])
    };

    private MdbResponseType ProcessVendRequest()
    {
        _vmc.Apply(VmcTrigger.RequestVend);
        return _options.Behavior switch
        {
            SimulatorBehavior.AlwaysDeny => Transition(VmcTrigger.DenyVend, MdbResponseType.VendDenied),
            SimulatorBehavior.AlwaysApprove => Transition(VmcTrigger.ApproveVend, MdbResponseType.VendApproved),
            _ => MdbResponseType.Ack
        };
    }

    private MdbResponseType Transition(VmcTrigger trigger, MdbResponseType response)
    {
        _vmc.Apply(trigger);
        return response;
    }

    private static VmcTrigger MapTrigger(MdbFrame request) => (request.Command, request.Subcommand) switch
    {
        (MdbCommandType.Reset, _) => VmcTrigger.Reset,
        (MdbCommandType.Setup, _) => VmcTrigger.SetupComplete,
        (MdbCommandType.Reader, MdbSubcommandType.Enable) => VmcTrigger.Enable,
        (MdbCommandType.Reader, MdbSubcommandType.Disable) => VmcTrigger.Disable,
        (MdbCommandType.Reader, MdbSubcommandType.Cancel) => VmcTrigger.CancelSession,
        (MdbCommandType.Poll, _) => VmcTrigger.BeginSession,
        (MdbCommandType.Vend, MdbSubcommandType.VendRequest) => VmcTrigger.RequestVend,
        (MdbCommandType.Vend, MdbSubcommandType.VendCancel) => VmcTrigger.CancelVend,
        (MdbCommandType.Vend, MdbSubcommandType.VendSuccess) => VmcTrigger.CompleteVend,
        (MdbCommandType.Vend, MdbSubcommandType.VendFailure) => VmcTrigger.FailVend,
        (MdbCommandType.Vend, MdbSubcommandType.SessionComplete) => VmcTrigger.CompleteSession,
        _ => VmcTrigger.NoStateChange
    };

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _gate.WaitAsync();
        try
        {
            if (_disposed) return;
            if (IsConnected)
            {
                _vmc.Apply(VmcTrigger.Disconnect);
                IsConnected = false;
            }
            _disposed = true;
        }
        finally { _gate.Release(); }
        GC.SuppressFinalize(this);
    }
}
