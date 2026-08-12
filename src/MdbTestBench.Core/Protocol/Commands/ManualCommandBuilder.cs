using System.Globalization;
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
    Expansion
}

public sealed record ManualCommandInput(
    ManualCommandKind Kind,
    decimal? Price = null,
    int? ProductNumber = null,
    decimal? Value = null);

public sealed record ManualCommandBuildResult(MdbFrame Frame, string LogicalPayload, VmcTrigger Trigger);

public static class ManualCommandBuilder
{
    private static readonly MdbAddress Cashless = new(0x10, MdbDeviceType.CashlessDevice1);

    public static ManualCommandBuildResult Build(ManualCommandInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Kind == ManualCommandKind.VendRequest && input.Price is null)
            throw new ArgumentException("Vend Request requires a valid price.", nameof(input));
        if (input.Kind == ManualCommandKind.VendRequest && input.ProductNumber is null)
            throw new ArgumentException("Vend Request requires a product number.", nameof(input));
        if (input.Price is < 0 or > 999_999) throw new ArgumentOutOfRangeException(nameof(input), "Price must be between 0 and 999999.");
        if (input.ProductNumber is < 0 or > 65_535) throw new ArgumentOutOfRangeException(nameof(input), "Product number must be between 0 and 65535.");

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
            ManualCommandKind.VendFailure => (MdbCommandType.Vend, MdbSubcommandType.VendFailure, VmcTrigger.CancelVend),
            ManualCommandKind.SessionComplete => (MdbCommandType.Vend, MdbSubcommandType.SessionComplete, VmcTrigger.CompleteSession),
            ManualCommandKind.Expansion => (MdbCommandType.Expansion, MdbSubcommandType.None, VmcTrigger.NoStateChange),
            _ => throw new ArgumentOutOfRangeException(nameof(input))
        };

        var payloadParts = new List<string>();
        if (input.Kind == ManualCommandKind.VendRequest)
        {
            payloadParts.Add($"Price: {input.Price!.Value.ToString("0.00", CultureInfo.InvariantCulture)}");
            payloadParts.Add($"Product: {input.ProductNumber!.Value:D4}");
        }
        if (input.Kind == ManualCommandKind.SetupMaxMinPrices && input.Value is not null)
            payloadParts.Add($"Value: {input.Value.Value.ToString("0.00", CultureInfo.InvariantCulture)}");
        var logicalPayload = payloadParts.Count == 0 ? "No logical fields" : string.Join(" · ", payloadParts);

        var frame = MdbFrame.CommandFrame(MdbAddress.Vmc, Cashless, command, subcommand) with
        {
            InterpretedPayload = logicalPayload
        };
        return new ManualCommandBuildResult(frame, logicalPayload, trigger);
    }
}
