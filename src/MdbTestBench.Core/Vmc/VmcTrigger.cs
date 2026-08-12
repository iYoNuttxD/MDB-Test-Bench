namespace MdbTestBench.Core.Vmc;

public enum VmcTrigger
{
    Connect,
    Disconnect,
    Reset,
    SetupComplete,
    Disable,
    Enable,
    BeginSession,
    RequestVend,
    ApproveVend,
    DenyVend,
    CancelVend,
    CompleteVend,
    CompleteSession,
    CancelSession,
    NoStateChange,
    Fail
}
