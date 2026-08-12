namespace MdbTestBench.Core.Vmc;

public enum VmcState
{
    Disconnected,
    Connected,
    Reset,
    Configured,
    Enabled,
    SessionActive,
    VendPending,
    VendApproved,
    VendDenied,
    VendCompleted,
    Faulted
}
