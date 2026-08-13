using System.Globalization;
using MdbTestBench.Core.Protocol.Cashless;
using MdbTestBench.Core.Protocol.Frames;
using MdbTestBench.Core.Vmc;

namespace MdbTestBench.Core.Protocol.Commands;

public enum ManualCommandKind
{
    Reset,
    SetupConfig,
    SetupMaxMinPrices,
    ReaderDisable,
    ReaderEnable,
    ReaderCancel,
    WaitSession,
    VendRequest,
    VendCancel,
    VendSuccess,
    VendFailure,
    SessionComplete,
    CashSale,
    RevalueRequest,
    RevalueLimitRequest,
    Expansion
}

public sealed record ManualCommandInput(
    ManualCommandKind Kind,
    decimal? Price = null,
    int? ProductNumber = null,
    decimal? Value = null,
    MdbCashlessDevice Device = MdbCashlessDevice.CashlessDevice1,
    MdbFeatureLevel FeatureLevel = MdbFeatureLevel.Level1,
    MdbMonetaryScale? MonetaryScale = null,
    MdbVmcIdentification? VmcIdentification = null);

public sealed record ManualCommandBuildResult(
    MdbFrame Frame,
    MdbCashlessCommand SemanticCommand,
    ReadOnlyMemory<byte> MdbBytes,
    string LogicalPayload,
    VmcTrigger Trigger);

public static class ManualCommandBuilder
{
    private static readonly MdbCashlessEncoder Encoder = new();

    public static ManualCommandBuildResult Build(ManualCommandInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Kind is ManualCommandKind.VendRequest or ManualCommandKind.CashSale && input.Price is null)
            throw new ArgumentException($"{input.Kind} requires a valid price.", nameof(input));
        if (input.Kind is ManualCommandKind.VendRequest or ManualCommandKind.CashSale && input.ProductNumber is null)
            throw new ArgumentException($"{input.Kind} requires a product number.", nameof(input));
        if (input.Kind == ManualCommandKind.RevalueRequest && input.Value is null)
            throw new ArgumentException("Revalue Request requires a value.", nameof(input));
        if (input.Price is < 0 or > 999_999) throw new ArgumentOutOfRangeException(nameof(input), "Price must be between 0 and 999999.");
        if (input.Value is < 0 or > 999_999) throw new ArgumentOutOfRangeException(nameof(input), "Value must be between 0 and 999999.");
        if (input.ProductNumber is < 0 or > 65_535) throw new ArgumentOutOfRangeException(nameof(input), "Product number must be between 0 and 65535.");
        if (input.FeatureLevel == MdbFeatureLevel.Custom)
            throw new ArgumentOutOfRangeException(nameof(input), "A concrete MDB feature level is required for encoding.");

        var scale = input.MonetaryScale ?? new MdbMonetaryScale(1, 2);
        var price = input.Price is null ? (ushort)0 : ToUInt16(scale.ToScaled(input.Price.Value), nameof(input.Price));
        var value = input.Value is null ? (ushort)0 : ToUInt16(scale.ToScaled(input.Value.Value), nameof(input.Value));
        var item = input.ProductNumber is null ? ushort.MaxValue : checked((ushort)input.ProductNumber.Value);

        MdbCashlessCommand semanticCommand = input.Kind switch
        {
            ManualCommandKind.Reset => new MdbResetCommand(input.Device),
            ManualCommandKind.SetupConfig => new MdbSetupConfigCommand(input.FeatureLevel, 0, 0, 0, input.Device),
            ManualCommandKind.SetupMaxMinPrices => new MdbSetupMaxMinPricesCommand(
                input.Price is null ? ushort.MaxValue : price, input.Value is null ? (ushort)0 : value, input.Device),
            ManualCommandKind.ReaderDisable => new MdbReaderDisableCommand(input.Device),
            ManualCommandKind.ReaderEnable => new MdbReaderEnableCommand(input.Device),
            ManualCommandKind.ReaderCancel => new MdbReaderCancelCommand(input.Device),
            ManualCommandKind.WaitSession => new MdbPollCommand(input.Device),
            ManualCommandKind.VendRequest => new MdbVendRequestCommand(price, item, input.Device),
            ManualCommandKind.VendCancel => new MdbVendCancelCommand(input.Device),
            ManualCommandKind.VendSuccess => new MdbVendSuccessCommand(item, input.Device),
            ManualCommandKind.VendFailure => new MdbVendFailureCommand(input.Device),
            ManualCommandKind.SessionComplete => new MdbSessionCompleteCommand(input.Device),
            ManualCommandKind.CashSale => new MdbCashSaleCommand(price, item, input.Device),
            ManualCommandKind.RevalueRequest => new MdbRevalueRequestCommand(value, input.Device),
            ManualCommandKind.RevalueLimitRequest => new MdbRevalueLimitRequestCommand(input.Device),
            ManualCommandKind.Expansion when input.VmcIdentification is not null =>
                new MdbExpansionRequestIdCommand(input.VmcIdentification, input.Device),
            ManualCommandKind.Expansion => throw new ArgumentException(
                "Expansion Request ID requires an explicit 3/12/12/2-byte VMC identification; no identity is invented.", nameof(input)),
            _ => throw new ArgumentOutOfRangeException(nameof(input))
        };
        if ((int)input.FeatureLevel < (int)semanticCommand.MinimumFeatureLevel)
            throw new InvalidOperationException(
                $"{semanticCommand.CommandType}/{semanticCommand.SubcommandType} requires {semanticCommand.MinimumFeatureLevel} or later.");

        var (command, subcommand, trigger) = input.Kind switch
        {
            ManualCommandKind.Reset => (MdbCommandType.Reset, MdbSubcommandType.None, VmcTrigger.Reset),
            ManualCommandKind.SetupConfig => (MdbCommandType.Setup, MdbSubcommandType.SetupConfig, VmcTrigger.SetupComplete),
            ManualCommandKind.SetupMaxMinPrices => (MdbCommandType.Setup, MdbSubcommandType.SetupMaxMinPrices, VmcTrigger.SetupComplete),
            ManualCommandKind.ReaderDisable => (MdbCommandType.Reader, MdbSubcommandType.Disable, VmcTrigger.Disable),
            ManualCommandKind.ReaderEnable => (MdbCommandType.Reader, MdbSubcommandType.Enable, VmcTrigger.Enable),
            ManualCommandKind.ReaderCancel => (MdbCommandType.Reader, MdbSubcommandType.Cancel, VmcTrigger.CancelSession),
            ManualCommandKind.WaitSession => (MdbCommandType.Poll, MdbSubcommandType.None, VmcTrigger.BeginSession),
            ManualCommandKind.VendRequest => (MdbCommandType.Vend, MdbSubcommandType.VendRequest, VmcTrigger.RequestVend),
            ManualCommandKind.VendCancel => (MdbCommandType.Vend, MdbSubcommandType.VendCancel, VmcTrigger.CancelVend),
            ManualCommandKind.VendSuccess => (MdbCommandType.Vend, MdbSubcommandType.VendSuccess, VmcTrigger.CompleteVend),
            ManualCommandKind.VendFailure => (MdbCommandType.Vend, MdbSubcommandType.VendFailure, VmcTrigger.FailVend),
            ManualCommandKind.SessionComplete => (MdbCommandType.Vend, MdbSubcommandType.SessionComplete, VmcTrigger.CompleteSession),
            ManualCommandKind.CashSale => (MdbCommandType.Vend, MdbSubcommandType.CashSale, VmcTrigger.NoStateChange),
            ManualCommandKind.RevalueRequest => (MdbCommandType.Revalue, MdbSubcommandType.RevalueRequest, VmcTrigger.NoStateChange),
            ManualCommandKind.RevalueLimitRequest => (MdbCommandType.Revalue, MdbSubcommandType.RevalueLimitRequest, VmcTrigger.NoStateChange),
            ManualCommandKind.Expansion => (MdbCommandType.Expansion, MdbSubcommandType.ExpansionRequestId, VmcTrigger.NoStateChange),
            _ => throw new ArgumentOutOfRangeException(nameof(input))
        };

        var payloadParts = new List<string>();
        if (input.Kind is ManualCommandKind.VendRequest or ManualCommandKind.CashSale)
        {
            payloadParts.Add($"Price: {input.Price!.Value.ToString("0.00", CultureInfo.InvariantCulture)}");
            payloadParts.Add($"Scaled: {price} (factor {scale.ScaleFactor}, decimals {scale.DecimalPlaces})");
            payloadParts.Add($"Product: {input.ProductNumber!.Value:D4}");
        }
        if (input.Kind == ManualCommandKind.SetupMaxMinPrices)
        {
            payloadParts.Add(input.Price is null ? "Maximum: unknown (FFFF)" : $"Maximum: {input.Price.Value:0.00}");
            payloadParts.Add(input.Value is null ? "Minimum: unknown (0000)" : $"Minimum: {input.Value.Value:0.00}");
        }
        if (input.Kind == ManualCommandKind.RevalueRequest)
        {
            payloadParts.Add($"Value: {input.Value!.Value.ToString("0.00", CultureInfo.InvariantCulture)}");
            payloadParts.Add($"Scaled: {value} (factor {scale.ScaleFactor}, decimals {scale.DecimalPlaces})");
        }
        var logicalPayload = payloadParts.Count == 0 ? "No logical fields" : string.Join(" · ", payloadParts);

        var bytes = Encoder.Encode(semanticCommand);
        var destination = input.Device == MdbCashlessDevice.CashlessDevice1
            ? new MdbAddress(0x10, MdbDeviceType.CashlessDevice1)
            : new MdbAddress(0x60, MdbDeviceType.CashlessDevice2);
        var frame = MdbFrame.CommandFrame(MdbAddress.Vmc, destination, command, subcommand, bytes) with
        {
            InterpretedPayload = logicalPayload,
            WireCommandByte = bytes.Span[0],
            CashlessDevice = input.Device
        };
        return new ManualCommandBuildResult(frame, semanticCommand, bytes, logicalPayload, trigger);
    }

    private static ushort ToUInt16(ulong value, string parameterName) => value <= ushort.MaxValue
        ? (ushort)value
        : throw new ArgumentOutOfRangeException(parameterName, "Scaled MDB value exceeds the 16-bit Level 1/2 range.");
}
