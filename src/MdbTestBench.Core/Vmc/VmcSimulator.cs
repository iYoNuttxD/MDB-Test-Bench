namespace MdbTestBench.Core.Vmc;

public sealed class VmcSimulator(VmcStateMachine stateMachine)
{
    public VmcState State => stateMachine.State;

    public VmcState Apply(VmcTrigger trigger) => stateMachine.Fire(trigger);
}
