namespace MdbTestBench.Core.Vmc;

public sealed class InvalidVmcTransitionException(VmcState state, VmcTrigger trigger)
    : InvalidOperationException($"Trigger '{trigger}' is invalid while the VMC is in '{state}'.")
{
    public VmcState State { get; } = state;
    public VmcTrigger Trigger { get; } = trigger;
}
