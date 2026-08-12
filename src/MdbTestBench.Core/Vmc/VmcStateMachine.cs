namespace MdbTestBench.Core.Vmc;

public sealed class VmcStateMachine
{
    private readonly object _gate = new();

    public VmcState State { get; private set; } = VmcState.Disconnected;

    public VmcState Fire(VmcTrigger trigger)
    {
        lock (_gate)
        {
            State = (State, trigger) switch
            {
                (VmcState.Disconnected, VmcTrigger.Connect) => VmcState.Connected,
                (_, VmcTrigger.Disconnect) => VmcState.Disconnected,
                (VmcState.Connected, VmcTrigger.Reset) => VmcState.Reset,
                (VmcState.Reset, VmcTrigger.SetupComplete) => VmcState.Configured,
                (VmcState.Configured, VmcTrigger.Enable) => VmcState.Enabled,
                (VmcState.Enabled, VmcTrigger.BeginSession) => VmcState.SessionActive,
                (VmcState.SessionActive, VmcTrigger.RequestVend) => VmcState.VendPending,
                (VmcState.VendPending, VmcTrigger.ApproveVend) => VmcState.VendApproved,
                (VmcState.VendPending, VmcTrigger.DenyVend) => VmcState.VendDenied,
                (VmcState.VendApproved, VmcTrigger.CompleteVend) => VmcState.VendCompleted,
                (VmcState.VendDenied, VmcTrigger.CompleteSession) => VmcState.Enabled,
                (VmcState.VendCompleted, VmcTrigger.CompleteSession) => VmcState.Enabled,
                (_, VmcTrigger.Fail) => VmcState.Faulted,
                _ => throw new InvalidVmcTransitionException(State, trigger)
            };

            return State;
        }
    }
}
