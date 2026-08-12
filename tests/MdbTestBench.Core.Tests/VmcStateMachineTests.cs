using MdbTestBench.Core.Vmc;

namespace MdbTestBench.Core.Tests;

public sealed class VmcStateMachineTests
{
    [Fact]
    public void ValidSequence_ReturnsToEnabledAfterSuccessfulVend()
    {
        var machine = new VmcStateMachine();
        var sequence = new[]
        {
            VmcTrigger.Connect, VmcTrigger.Reset, VmcTrigger.SetupComplete,
            VmcTrigger.Enable, VmcTrigger.BeginSession, VmcTrigger.RequestVend,
            VmcTrigger.ApproveVend, VmcTrigger.CompleteVend, VmcTrigger.CompleteSession
        };

        foreach (var trigger in sequence) machine.Fire(trigger);

        Assert.Equal(VmcState.Enabled, machine.State);
    }

    [Fact]
    public void CanFireBlocksIncompatibleStructuredCommand()
    {
        var machine = new VmcStateMachine();
        machine.Fire(VmcTrigger.Connect);

        Assert.True(machine.CanFire(VmcTrigger.Reset));
        Assert.False(machine.CanFire(VmcTrigger.RequestVend));
    }

    [Fact]
    public void InvalidSequence_ThrowsAndPreservesState()
    {
        var machine = new VmcStateMachine();

        var exception = Assert.Throws<InvalidVmcTransitionException>(
            () => machine.Fire(VmcTrigger.Enable));

        Assert.Equal(VmcState.Disconnected, exception.State);
        Assert.Equal(VmcState.Disconnected, machine.State);
    }
}
