namespace MdbTestBench.Core.Vmc;

public sealed class VmcStateMachine
{
    private readonly object _gate = new();

    public VmcState State { get; private set; } = VmcState.Disconnected;

    public bool CanFire(VmcTrigger trigger)
    {
        lock (_gate) return TryTransition(State, trigger, out _);
    }

    public VmcState Fire(VmcTrigger trigger)
    {
        lock (_gate)
        {
            if (!TryTransition(State, trigger, out var next))
                throw new InvalidVmcTransitionException(State, trigger);
            State = next;
            return State;
        }
    }

    private static bool TryTransition(VmcState state, VmcTrigger trigger, out VmcState next)
    {
        next = (state, trigger) switch
        {
            (VmcState.Disconnected, VmcTrigger.Connect) => VmcState.Connected,
            (_, VmcTrigger.Disconnect) => VmcState.Disconnected,
            (not VmcState.Disconnected, VmcTrigger.Reset) => VmcState.Reset,
            (VmcState.Reset, VmcTrigger.SetupComplete) => VmcState.Disabled,
            (VmcState.Disabled, VmcTrigger.Enable) => VmcState.Enabled,
            (VmcState.Enabled, VmcTrigger.Disable) => VmcState.Disabled,
            (VmcState.Enabled, VmcTrigger.BeginSession) => VmcState.SessionIdle,
            (VmcState.SessionIdle, VmcTrigger.RequestVend) => VmcState.VendPending,
            (VmcState.VendPending, VmcTrigger.ApproveVend) => VmcState.VendApproved,
            (VmcState.VendPending, VmcTrigger.DenyVend) => VmcState.VendDenied,
            (VmcState.VendPending, VmcTrigger.CancelVend) => VmcState.SessionIdle,
            (VmcState.VendApproved, VmcTrigger.CompleteVend) => VmcState.SessionComplete,
            (VmcState.VendApproved, VmcTrigger.FailVend) => VmcState.SessionComplete,
            (VmcState.SessionIdle, VmcTrigger.CancelSession) => VmcState.Enabled,
            (VmcState.SessionIdle, VmcTrigger.CompleteSession) => VmcState.Enabled,
            (VmcState.VendDenied, VmcTrigger.CompleteSession) => VmcState.Enabled,
            (VmcState.SessionComplete, VmcTrigger.CompleteSession) => VmcState.Enabled,
            (_, VmcTrigger.NoStateChange) => state,
            (_, VmcTrigger.Fail) => VmcState.Error,
            _ => state
        };
        return next != state || trigger is VmcTrigger.NoStateChange or VmcTrigger.Disconnect;
    }
}
