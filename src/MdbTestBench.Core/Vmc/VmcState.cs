namespace MdbTestBench.Core.Vmc;

public enum VmcState
{
    Disconnected,
    Connected,
    Reset,
    Setup,
    Disabled,
    Enabled,
    SessionIdle,
    VendPending,
    VendApproved,
    VendDenied,
    SessionComplete,
    Error
}
