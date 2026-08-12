using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Commands;
using MdbTestBench.Core.Vmc;

namespace MdbTestBench.Core.Tests;

public sealed class ManualCommandBuilderTests
{
    [Fact]
    public void VendRequestCreatesSemanticPayloadWithoutInventingWireBytes()
    {
        var result = ManualCommandBuilder.Build(new ManualCommandInput(
            ManualCommandKind.VendRequest, 5.00m, 1));

        Assert.Equal(MdbCommandType.Vend, result.Frame.Command);
        Assert.Equal(MdbSubcommandType.VendRequest, result.Frame.Subcommand);
        Assert.Equal(VmcTrigger.RequestVend, result.Trigger);
        Assert.Equal("Price: 5.00 · Product: 0001", result.LogicalPayload);
        Assert.True(result.Frame.RawPayload.IsEmpty);
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
    [InlineData(ManualCommandKind.Expansion, MdbCommandType.Expansion, MdbSubcommandType.None)]
    public void MapsStructuredCommands(ManualCommandKind kind, MdbCommandType command, MdbSubcommandType subcommand)
    {
        var result = ManualCommandBuilder.Build(new ManualCommandInput(kind));
        Assert.Equal(command, result.Frame.Command);
        Assert.Equal(subcommand, result.Frame.Subcommand);
    }
}
