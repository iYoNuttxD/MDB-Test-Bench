using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Frames;
using MdbTestBench.Core.Vmc;
using MdbTestBench.Transport.Abstractions;

namespace MdbTestBench.Transport.Simulation;

public sealed class SimulatedCashlessTransport(
    SimulatedCashlessOptions? options = null,
    PollingMode pollingMode = PollingMode.HostManaged) : IMdbTransport
{
    private static readonly MdbAddress CashlessAddress = new(0x10, MdbDeviceType.CashlessDevice1);
    private readonly SimulatedCashlessOptions _options = options ?? new();
    private readonly VmcStateMachine _stateMachine = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public bool IsConnected { get; private set; }
    public VmcState State => _stateMachine.State;

    public TransportCapabilities Capabilities { get; } = new()
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

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsConnected)
        {
            _stateMachine.Fire(VmcTrigger.Connect);
            IsConnected = true;
        }
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsConnected)
        {
            _stateMachine.Fire(VmcTrigger.Disconnect);
            IsConnected = false;
        }
        return Task.CompletedTask;
    }

    public async Task<MdbFrame> ExchangeAsync(
        MdbFrame request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsConnected) throw new InvalidOperationException("The simulated transport is not connected.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_options.OperationTimeout);
            try
            {
                await Task.Delay(_options.ResponseDelay, timeoutSource.Token);
                var response = Process(request);
                return new MdbFrame(
                    DateTimeOffset.UtcNow,
                    MdbDirection.Rx,
                    CashlessAddress,
                    request.Source,
                    request.Command,
                    request.Subcommand,
                    response,
                    ReadOnlyMemory<byte>.Empty,
                    response.ToString());
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Simulated exchange exceeded {_options.OperationTimeout}.");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private MdbResponseType Process(MdbFrame request) => (request.Command, request.Subcommand, State) switch
    {
        (MdbCommandType.Reset, _, VmcState.Connected) => Transition(VmcTrigger.Reset, MdbResponseType.JustReset),
        (MdbCommandType.Setup, _, VmcState.Reset) => Transition(VmcTrigger.SetupComplete, MdbResponseType.ReaderConfigData),
        (MdbCommandType.Reader, MdbSubcommandType.Enable, VmcState.Configured) => Transition(VmcTrigger.Enable, MdbResponseType.Ack),
        (MdbCommandType.Poll, _, VmcState.Enabled) => Transition(VmcTrigger.BeginSession, MdbResponseType.BeginSession),
        (MdbCommandType.Vend, MdbSubcommandType.VendRequest, VmcState.SessionActive) => ProcessVendRequest(),
        (MdbCommandType.Vend, MdbSubcommandType.VendSuccess, VmcState.VendApproved) => Transition(VmcTrigger.CompleteVend, MdbResponseType.Ack),
        (MdbCommandType.Vend, MdbSubcommandType.SessionComplete, VmcState.VendCompleted) => Transition(VmcTrigger.CompleteSession, MdbResponseType.EndSession),
        (MdbCommandType.Vend, MdbSubcommandType.SessionComplete, VmcState.VendDenied) => Transition(VmcTrigger.CompleteSession, MdbResponseType.EndSession),
        _ => throw new InvalidVmcTransitionException(State, MapTrigger(request))
    };

    private MdbResponseType ProcessVendRequest()
    {
        _stateMachine.Fire(VmcTrigger.RequestVend);
        return _options.ApproveVends
            ? Transition(VmcTrigger.ApproveVend, MdbResponseType.VendApproved)
            : Transition(VmcTrigger.DenyVend, MdbResponseType.VendDenied);
    }

    private MdbResponseType Transition(VmcTrigger trigger, MdbResponseType response)
    {
        _stateMachine.Fire(trigger);
        return response;
    }

    private static VmcTrigger MapTrigger(MdbFrame request) => (request.Command, request.Subcommand) switch
    {
        (MdbCommandType.Reset, _) => VmcTrigger.Reset,
        (MdbCommandType.Setup, _) => VmcTrigger.SetupComplete,
        (MdbCommandType.Reader, MdbSubcommandType.Enable) => VmcTrigger.Enable,
        (MdbCommandType.Poll, _) => VmcTrigger.BeginSession,
        (MdbCommandType.Vend, MdbSubcommandType.VendRequest) => VmcTrigger.RequestVend,
        (MdbCommandType.Vend, MdbSubcommandType.VendSuccess) => VmcTrigger.CompleteVend,
        (MdbCommandType.Vend, MdbSubcommandType.SessionComplete) => VmcTrigger.CompleteSession,
        _ => VmcTrigger.Fail
    };

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (IsConnected) await DisconnectAsync();
        _gate.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
