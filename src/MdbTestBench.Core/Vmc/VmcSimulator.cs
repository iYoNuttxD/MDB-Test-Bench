namespace MdbTestBench.Core.Vmc;

public sealed class VmcSimulator
{
    private VmcStateMachine _stateMachine;

    public VmcSimulator(VmcStateMachine? stateMachine = null) =>
        _stateMachine = stateMachine ?? new VmcStateMachine();

    public VmcState State => _stateMachine.State;

    public bool CanApply(VmcTrigger trigger) => _stateMachine.CanFire(trigger);

    public VmcState Apply(VmcTrigger trigger) => _stateMachine.Fire(trigger);

    public void Restart() => _stateMachine = new VmcStateMachine();
}
