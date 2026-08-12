using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Frames;
using MdbTestBench.Core.Vmc;
using MdbTestBench.Transport.Abstractions;

namespace MdbTestBench.Transport.Simulation;

public sealed class SimulatedCashlessTransport : IMdbTransport, IRawCommandTransport
{
    private static readonly MdbAddress CashlessAddress = new(0x10, MdbDeviceType.CashlessDevice1);
    private readonly SimulatedCashlessOptions _options;
    private readonly VmcSimulator _vmc;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public SimulatedCashlessTransport(
        SimulatedCashlessOptions? options = null,
        PollingMode pollingMode = PollingMode.HostManaged,
        VmcSimulator? vmcSimulator = null)
    {
        _options = options ?? new SimulatedCashlessOptions();
        if (_options.ResponseDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Simulator response delay cannot be negative.");
        if (_options.OperationTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Simulator timeout must be greater than zero.");
        _vmc = vmcSimulator ?? new VmcSimulator();
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
        if (request.Command == MdbCommandType.Setup && request.Subcommand == MdbSubcommandType.SetupMaxMinPrices &&
            State == VmcState.Disabled) return true;
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
                var response = Process(request);
                var malformed = _options.Behavior == SimulatorBehavior.MalformedResponse;
                return new MdbFrame(
                    DateTimeOffset.UtcNow,
                    MdbDirection.Rx,
                    CashlessAddress,
                    request.Source,
                    request.Command,
                    request.Subcommand,
                    malformed ? MdbResponseType.Unknown : response,
                    malformed ? new byte[] { 0xFF } : ReadOnlyMemory<byte>.Empty,
                    malformed ? "Malformed simulated response" : response.ToString());
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

    private MdbResponseType Process(MdbFrame request)
    {
        if (_options.Behavior == SimulatorBehavior.UnexpectedResponse) return MdbResponseType.Unknown;
        return (request.Command, request.Subcommand, State) switch
        {
            (MdbCommandType.Reset, _, not VmcState.Disconnected) => Transition(VmcTrigger.Reset, MdbResponseType.JustReset),
            (MdbCommandType.Setup, _, VmcState.Reset) => Transition(VmcTrigger.SetupComplete, MdbResponseType.ReaderConfigData),
            (MdbCommandType.Setup, MdbSubcommandType.SetupMaxMinPrices, VmcState.Disabled) => MdbResponseType.Data,
            (MdbCommandType.Reader, MdbSubcommandType.Enable, VmcState.Disabled) => Transition(VmcTrigger.Enable, MdbResponseType.Ack),
            (MdbCommandType.Reader, MdbSubcommandType.Disable, VmcState.Enabled) => Transition(VmcTrigger.Disable, MdbResponseType.Ack),
            (MdbCommandType.Reader, MdbSubcommandType.Cancel, VmcState.SessionIdle) => Transition(VmcTrigger.CancelSession, MdbResponseType.EndSession),
            (MdbCommandType.Poll, _, VmcState.Enabled) => Transition(VmcTrigger.BeginSession, MdbResponseType.BeginSession),
            (MdbCommandType.Vend, MdbSubcommandType.VendRequest, VmcState.SessionIdle) => ProcessVendRequest(),
            (MdbCommandType.Vend, MdbSubcommandType.VendCancel, VmcState.VendPending) => Transition(VmcTrigger.CancelVend, MdbResponseType.Ack),
            (MdbCommandType.Vend, MdbSubcommandType.VendCancel, VmcState.VendApproved) => Transition(VmcTrigger.CancelVend, MdbResponseType.Ack),
            (MdbCommandType.Vend, MdbSubcommandType.VendSuccess, VmcState.VendApproved) => Transition(VmcTrigger.CompleteVend, MdbResponseType.Ack),
            (MdbCommandType.Vend, MdbSubcommandType.VendFailure, VmcState.VendApproved) => Transition(VmcTrigger.CancelVend, MdbResponseType.Ack),
            (MdbCommandType.Vend, MdbSubcommandType.SessionComplete, VmcState.SessionComplete) => Transition(VmcTrigger.CompleteSession, MdbResponseType.EndSession),
            (MdbCommandType.Vend, MdbSubcommandType.SessionComplete, VmcState.VendDenied) => Transition(VmcTrigger.CompleteSession, MdbResponseType.EndSession),
            (MdbCommandType.Expansion, _, _) => MdbResponseType.Data,
            _ => throw new InvalidVmcTransitionException(State, MapTrigger(request))
        };
    }

    private MdbResponseType ProcessVendRequest()
    {
        _vmc.Apply(VmcTrigger.RequestVend);
        return _options.Behavior == SimulatorBehavior.AlwaysDeny
            ? Transition(VmcTrigger.DenyVend, MdbResponseType.VendDenied)
            : Transition(VmcTrigger.ApproveVend, MdbResponseType.VendApproved);
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
