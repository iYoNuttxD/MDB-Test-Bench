using MdbTestBench.Core.Capabilities;

namespace MdbTestBench.Core.Protocol.Cashless;

public enum MdbCashlessDevice
{
    CashlessDevice1 = 1,
    CashlessDevice2 = 2
}

public abstract record MdbCashlessCommand(MdbCashlessDevice Device)
{
    public abstract MdbCommandType CommandType { get; }
    public abstract MdbSubcommandType SubcommandType { get; }
    public virtual MdbFeatureLevel MinimumFeatureLevel => MdbFeatureLevel.Level1;
}

public sealed record MdbResetCommand(MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1)
    : MdbCashlessCommand(Device)
{
    public override MdbCommandType CommandType => MdbCommandType.Reset;
    public override MdbSubcommandType SubcommandType => MdbSubcommandType.None;
}

public sealed record MdbSetupConfigCommand(
    MdbFeatureLevel VmcFeatureLevel,
    byte DisplayColumns,
    byte DisplayRows,
    byte DisplayInformation,
    MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1) : MdbCashlessCommand(Device)
{
    public override MdbCommandType CommandType => MdbCommandType.Setup;
    public override MdbSubcommandType SubcommandType => MdbSubcommandType.SetupConfig;
}

public sealed record MdbSetupMaxMinPricesCommand(
    ushort MaximumPrice,
    ushort MinimumPrice,
    MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1) : MdbCashlessCommand(Device)
{
    public override MdbCommandType CommandType => MdbCommandType.Setup;
    public override MdbSubcommandType SubcommandType => MdbSubcommandType.SetupMaxMinPrices;
}

public sealed record MdbSetupMaxMinPricesExpandedCommand(
    uint MaximumPrice,
    uint MinimumPrice,
    MdbPackedBcdCurrencyCode CurrencyCode,
    MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1) : MdbCashlessCommand(Device)
{
    public override MdbCommandType CommandType => MdbCommandType.Setup;
    public override MdbSubcommandType SubcommandType => MdbSubcommandType.SetupMaxMinPrices;
    public override MdbFeatureLevel MinimumFeatureLevel => MdbFeatureLevel.Level3;
}

public sealed record MdbPollCommand(MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1)
    : MdbCashlessCommand(Device)
{
    public override MdbCommandType CommandType => MdbCommandType.Poll;
    public override MdbSubcommandType SubcommandType => MdbSubcommandType.None;
}

public abstract record MdbReaderCommand(MdbCashlessDevice Device, MdbSubcommandType ReaderSubcommand)
    : MdbCashlessCommand(Device)
{
    public override MdbCommandType CommandType => MdbCommandType.Reader;
    public override MdbSubcommandType SubcommandType => ReaderSubcommand;
}

public sealed record MdbReaderDisableCommand(MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1)
    : MdbReaderCommand(Device, MdbSubcommandType.Disable);
public sealed record MdbReaderEnableCommand(MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1)
    : MdbReaderCommand(Device, MdbSubcommandType.Enable);
public sealed record MdbReaderCancelCommand(MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1)
    : MdbReaderCommand(Device, MdbSubcommandType.Cancel);

public abstract record MdbVendCommand(MdbCashlessDevice Device, MdbSubcommandType VendSubcommand)
    : MdbCashlessCommand(Device)
{
    public override MdbCommandType CommandType => MdbCommandType.Vend;
    public override MdbSubcommandType SubcommandType => VendSubcommand;
}

public sealed record MdbVendRequestCommand(
    ushort ItemPrice,
    ushort ItemNumber,
    MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1)
    : MdbVendCommand(Device, MdbSubcommandType.VendRequest);
public sealed record MdbVendCancelCommand(MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1)
    : MdbVendCommand(Device, MdbSubcommandType.VendCancel);
public sealed record MdbVendSuccessCommand(
    ushort ItemNumber,
    MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1)
    : MdbVendCommand(Device, MdbSubcommandType.VendSuccess);
public sealed record MdbVendFailureCommand(MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1)
    : MdbVendCommand(Device, MdbSubcommandType.VendFailure);
public sealed record MdbSessionCompleteCommand(MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1)
    : MdbVendCommand(Device, MdbSubcommandType.SessionComplete);
public sealed record MdbCashSaleCommand(
    ushort ItemPrice,
    ushort ItemNumber,
    MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1)
    : MdbVendCommand(Device, MdbSubcommandType.CashSale);

public sealed record MdbRevalueRequestCommand(
    ushort Amount,
    MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1) : MdbCashlessCommand(Device)
{
    public override MdbCommandType CommandType => MdbCommandType.Revalue;
    public override MdbSubcommandType SubcommandType => MdbSubcommandType.RevalueRequest;
    public override MdbFeatureLevel MinimumFeatureLevel => MdbFeatureLevel.Level2;
}

public sealed record MdbRevalueRequestExpandedCommand(
    uint Amount,
    MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1) : MdbCashlessCommand(Device)
{
    public override MdbCommandType CommandType => MdbCommandType.Revalue;
    public override MdbSubcommandType SubcommandType => MdbSubcommandType.RevalueRequest;
    public override MdbFeatureLevel MinimumFeatureLevel => MdbFeatureLevel.Level3;
}

public sealed record MdbRevalueLimitRequestCommand(MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1)
    : MdbCashlessCommand(Device)
{
    public override MdbCommandType CommandType => MdbCommandType.Revalue;
    public override MdbSubcommandType SubcommandType => MdbSubcommandType.RevalueLimitRequest;
    public override MdbFeatureLevel MinimumFeatureLevel => MdbFeatureLevel.Level2;
}

public sealed record MdbVmcIdentification(
    string ManufacturerCode,
    string SerialNumber,
    string ModelNumber,
    ushort SoftwareVersionPackedBcd);

public sealed record MdbExpansionRequestIdCommand(
    MdbVmcIdentification Identification,
    MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1) : MdbCashlessCommand(Device)
{
    public override MdbCommandType CommandType => MdbCommandType.Expansion;
    public override MdbSubcommandType SubcommandType => MdbSubcommandType.ExpansionRequestId;
}

public sealed record MdbExpansionEnableOptionsCommand(
    MdbLevel3Options Options,
    MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1) : MdbCashlessCommand(Device)
{
    public override MdbCommandType CommandType => MdbCommandType.Expansion;
    public override MdbSubcommandType SubcommandType => MdbSubcommandType.ExpansionEnableOptions;
    public override MdbFeatureLevel MinimumFeatureLevel => MdbFeatureLevel.Level3;
}

[Flags]
public enum MdbLevel3Options : uint
{
    None = 0,
    FileTransportLayer = 1u << 0,
    ThirtyTwoBitMonetary = 1u << 1,
    MultiCurrencyAndLanguage = 1u << 2,
    NegativeVend = 1u << 3,
    DataEntry = 1u << 4,
    AlwaysIdle = 1u << 5,
    RemoteVend = 1u << 6,
    BasketPartialRefundOptionsPrice = 1u << 7,
    Coupon = 1u << 8,
    AskBeginSession = 1u << 9,
    EnhancedItemNumberInformation = 1u << 10
}

public sealed record MdbDecodedCommand(
    MdbCashlessCommand? Command,
    MdbCashlessDevice? Device,
    byte? CommandByte,
    ReadOnlyMemory<byte> RawBytes,
    bool ChecksumValid,
    string? Error = null,
    ReadOnlyMemory<byte> UnknownPayload = default);

public sealed record MdbCashlessDecodeOptions(
    MdbFeatureLevel FeatureLevel = MdbFeatureLevel.Level1,
    bool ExpandedCurrencyMode = false);

public abstract record MdbCashlessResponse(ReadOnlyMemory<byte> RawBytes)
{
    public abstract MdbResponseType ResponseType { get; }
    public virtual ReadOnlyMemory<byte> RawPayload => RawBytes.Length > 1
        ? RawBytes[0..^1]
        : ReadOnlyMemory<byte>.Empty;
}

public sealed record MdbAckResponse(ReadOnlyMemory<byte> Bytes) : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.Ack; }
public sealed record MdbNakResponse(ReadOnlyMemory<byte> Bytes) : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.Nak; }
public sealed record MdbJustResetResponse(ReadOnlyMemory<byte> Bytes) : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.JustReset; }
public sealed record MdbReaderConfigResponse(
    MdbFeatureLevel ReaderFeatureLevel,
    MdbPackedBcdCurrencyCode CountryOrCurrencyCode,
    byte ScaleFactor,
    byte DecimalPlaces,
    byte ApplicationMaximumResponseTimeSeconds,
    byte MiscellaneousOptions,
    ReadOnlyMemory<byte> Bytes) : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.ReaderConfigData; }
public sealed record MdbDisplayRequestResponse(byte DisplayTimeTenths, string DisplayData, ReadOnlyMemory<byte> Bytes)
    : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.DisplayRequest; }
public sealed record MdbBeginSessionResponse(
    ulong FundsAvailable,
    uint? PaymentMediaId,
    byte? PaymentType,
    ushort? PaymentData,
    ushort? UserLanguage,
    MdbPackedBcdCurrencyCode? UserCurrencyCode,
    byte? CardOptions,
    bool ExpandedCurrencyMode,
    ReadOnlyMemory<byte> Bytes) : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.BeginSession; }
public sealed record MdbSessionCancelRequestResponse(ReadOnlyMemory<byte> Bytes) : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.SessionCancelRequest; }
public sealed record MdbVendApprovedResponse(ulong VendAmount, bool ExpandedCurrencyMode, ReadOnlyMemory<byte> Bytes)
    : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.VendApproved; }
public sealed record MdbVendDeniedResponse(ReadOnlyMemory<byte> Bytes) : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.VendDenied; }
public sealed record MdbEndSessionResponse(ReadOnlyMemory<byte> Bytes) : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.EndSession; }
public sealed record MdbCancelledResponse(ReadOnlyMemory<byte> Bytes) : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.Cancelled; }
public sealed record MdbPeripheralIdResponse(
    string ManufacturerCode,
    string SerialNumber,
    string ModelNumber,
    ushort SoftwareVersionPackedBcd,
    MdbLevel3Options? OptionalFeatures,
    ReadOnlyMemory<byte> Bytes) : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.PeripheralId; }
public sealed record MdbMalfunctionResponse(byte ErrorCode, ReadOnlyMemory<byte> Bytes) : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.Malfunction; }
public sealed record MdbCommandOutOfSequenceResponse(byte? ReaderState, ReadOnlyMemory<byte> Bytes)
    : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.CommandOutOfSequence; }
public sealed record MdbRevalueApprovedResponse(ReadOnlyMemory<byte> Bytes) : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.RevalueApproved; }
public sealed record MdbRevalueDeniedResponse(ReadOnlyMemory<byte> Bytes) : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.RevalueDenied; }
public sealed record MdbRevalueLimitResponse(ulong Amount, bool ExpandedCurrencyMode, ReadOnlyMemory<byte> Bytes)
    : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.RevalueLimit; }
public sealed record MdbUnknownCashlessResponse(byte ResponseCode, ReadOnlyMemory<byte> Payload, ReadOnlyMemory<byte> Bytes)
    : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.Unknown; public override ReadOnlyMemory<byte> RawPayload => Payload; }
public sealed record UnknownExpansionResponse(byte ResponseCode, ReadOnlyMemory<byte> Payload, ReadOnlyMemory<byte> Bytes)
    : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.Unknown; public override ReadOnlyMemory<byte> RawPayload => Payload; }
public sealed record MdbMalformedCashlessResponse(string Error, ReadOnlyMemory<byte> Bytes)
    : MdbCashlessResponse(Bytes)
{ public override MdbResponseType ResponseType => MdbResponseType.Unknown; public override ReadOnlyMemory<byte> RawPayload => Bytes; }

public static class MdbLevel3CapabilityMapper
{
    public static MdbCapabilities Apply(MdbCapabilities current, MdbLevel3Options supported) => current with
    {
        Expansion = CapabilityStatus.Supported,
        RemoteVend = Status(supported, MdbLevel3Options.RemoteVend),
        MultiCurrency = Status(supported, MdbLevel3Options.MultiCurrencyAndLanguage),
        NegativeVend = Status(supported, MdbLevel3Options.NegativeVend),
        DataEntry = Status(supported, MdbLevel3Options.DataEntry),
        Basket = Status(supported, MdbLevel3Options.BasketPartialRefundOptionsPrice),
        Refund = Status(supported, MdbLevel3Options.BasketPartialRefundOptionsPrice),
        Extensions = new Dictionary<string, CapabilityStatus>(current.Extensions, StringComparer.OrdinalIgnoreCase)
        {
            ["ThirtyTwoBitMonetary"] = Status(supported, MdbLevel3Options.ThirtyTwoBitMonetary),
            ["FileTransportLayer"] = Status(supported, MdbLevel3Options.FileTransportLayer),
            ["Coupon"] = Status(supported, MdbLevel3Options.Coupon),
            ["AskBeginSession"] = Status(supported, MdbLevel3Options.AskBeginSession),
            ["EnhancedItemNumberInformation"] = Status(supported, MdbLevel3Options.EnhancedItemNumberInformation)
        }
    };

    private static CapabilityStatus Status(MdbLevel3Options value, MdbLevel3Options bit) =>
        value.HasFlag(bit) ? CapabilityStatus.Supported : CapabilityStatus.Unsupported;
}
