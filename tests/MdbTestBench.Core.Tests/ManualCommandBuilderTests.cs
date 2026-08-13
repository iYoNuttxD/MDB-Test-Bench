using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;
using MdbTestBench.Core.Protocol.Commands;
using MdbTestBench.Core.Vmc;

namespace MdbTestBench.Core.Tests;

public sealed class ManualCommandBuilderTests
{
    [Fact]
    public void VendRequestCreatesSemanticPayloadAndAuthoritativeMdbBytes()
    {
        var result = ManualCommandBuilder.Build(new ManualCommandInput(
            ManualCommandKind.VendRequest, 5.00m, 1));

        Assert.Equal(MdbCommandType.Vend, result.Frame.Command);
        Assert.Equal(MdbSubcommandType.VendRequest, result.Frame.Subcommand);
        Assert.Equal(VmcTrigger.RequestVend, result.Trigger);
        Assert.Equal("Price: 5.00 · Scaled: 500 (factor 1, decimals 2) · Product: 0001", result.LogicalPayload);
        Assert.Equal(new byte[] { 0x13, 0x00, 0x01, 0xF4, 0x00, 0x01, 0x09 }, result.MdbBytes.ToArray());
        Assert.Equal(result.MdbBytes, result.Frame.RawBytes);
    }

    [Fact]
    public void VendRequestRequiresPriceAndProduct()
    {
        Assert.Throws<ArgumentException>(() => ManualCommandBuilder.Build(
            new ManualCommandInput(ManualCommandKind.VendRequest)));
    }

    [Theory]
    [InlineData(ManualCommandKind.Reset, MdbCommandType.Reset, MdbSubcommandType.None)]
    [InlineData(ManualCommandKind.SetupConfig, MdbCommandType.Setup, MdbSubcommandType.SetupConfig)]
    [InlineData(ManualCommandKind.ReaderEnable, MdbCommandType.Reader, MdbSubcommandType.Enable)]
    [InlineData(ManualCommandKind.VendCancel, MdbCommandType.Vend, MdbSubcommandType.VendCancel)]
    public void MapsStructuredCommands(ManualCommandKind kind, MdbCommandType command, MdbSubcommandType subcommand)
    {
        var result = ManualCommandBuilder.Build(new ManualCommandInput(kind));
        Assert.Equal(command, result.Frame.Command);
        Assert.Equal(subcommand, result.Frame.Subcommand);
    }

    [Fact]
    public void ExpansionDoesNotInventVmcIdentity()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            ManualCommandBuilder.Build(new ManualCommandInput(ManualCommandKind.Expansion)));
        Assert.Contains("no identity is invented", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Level1MonetaryOverflowIsRejectedInsteadOfTruncated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ManualCommandBuilder.Build(
            new ManualCommandInput(ManualCommandKind.VendRequest, 700m, 1)));
    }

    [Fact]
    public void Device2SelectionChangesOnlyAddressFamily()
    {
        var result = ManualCommandBuilder.Build(new ManualCommandInput(
            ManualCommandKind.VendRequest, 5.00m, 1, Device: MdbCashlessDevice.CashlessDevice2));
        Assert.Equal(new byte[] { 0x63, 0x00, 0x01, 0xF4, 0x00, 0x01, 0x59 }, result.MdbBytes.ToArray());
        Assert.Equal(MdbDeviceType.CashlessDevice2, result.Frame.Destination.DeviceType);
    }

    [Fact]
    public void Level1ProfileCannotBuildLevel2Revalue()
    {
        Assert.Throws<InvalidOperationException>(() => ManualCommandBuilder.Build(new ManualCommandInput(
            ManualCommandKind.RevalueRequest, Value: 1.00m, FeatureLevel: MdbFeatureLevel.Level1)));
        var level2 = ManualCommandBuilder.Build(new ManualCommandInput(
            ManualCommandKind.RevalueRequest, Value: 1.00m, FeatureLevel: MdbFeatureLevel.Level2));
        Assert.Equal(new byte[] { 0x15, 0x00, 0x00, 0x64, 0x79 }, level2.MdbBytes.ToArray());
    }
}
