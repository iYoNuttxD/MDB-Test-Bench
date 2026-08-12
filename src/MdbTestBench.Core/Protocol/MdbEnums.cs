namespace MdbTestBench.Core.Protocol;

public enum MdbDirection { Tx, Rx }

public enum MdbDeviceType
{
    Vmc,
    CashlessDevice1,
    CashlessDevice2,
    CoinChanger,
    BillValidator,
    Unknown
}

public enum MdbFeatureLevel { Level1 = 1, Level2 = 2, Level3 = 3, Custom = 255 }

public enum MdbCommandType
{
    Reset,
    Setup,
    Poll,
    Reader,
    Vend,
    Expansion,
    Custom
}

public enum MdbSubcommandType
{
    None,
    SetupConfig,
    SetupMaxMinPrices,
    Disable,
    Enable,
    Cancel,
    VendRequest,
    VendCancel,
    VendSuccess,
    VendFailure,
    SessionComplete,
    CashSale,
    Custom
}

public enum MdbResponseType
{
    Ack,
    Nak,
    JustReset,
    ReaderConfigData,
    DisplayRequest,
    BeginSession,
    SessionCancelRequest,
    VendApproved,
    VendDenied,
    EndSession,
    Data,
    NoResponse,
    Unknown
}
