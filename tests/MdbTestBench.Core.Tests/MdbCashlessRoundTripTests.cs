using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;

namespace MdbTestBench.Core.Tests;

public sealed class MdbCashlessRoundTripTests
{
    private readonly MdbCashlessEncoder _encoder = new();
    private readonly MdbCashlessDecoder _decoder = new();

    [Theory]
    [MemberData(nameof(Commands))]
    public void EncodeThenDecodePreservesSemanticCommand(MdbCashlessCommand command)
    {
        var bytes = _encoder.Encode(command);
        var decoded = _decoder.DecodeCommand(bytes.Span);

        Assert.True(decoded.ChecksumValid);
        Assert.Null(decoded.Error);
        Assert.NotNull(decoded.Command);
        Assert.Equal(command, decoded.Command);
        Assert.Equal(bytes.ToArray(), decoded.RawBytes.ToArray());
    }

    public static TheoryData<MdbCashlessCommand> Commands => new()
    {
        new MdbResetCommand(),
        new MdbPollCommand(MdbCashlessDevice.CashlessDevice2),
        new MdbSetupConfigCommand(MdbFeatureLevel.Level2, 20, 4, 0),
        new MdbSetupMaxMinPricesCommand(1_000, 50),
        new MdbReaderEnableCommand(),
        new MdbVendRequestCommand(500, 1),
        new MdbVendCancelCommand(),
        new MdbVendSuccessCommand(1),
        new MdbVendFailureCommand(),
        new MdbSessionCompleteCommand(),
        new MdbCashSaleCommand(500, 1),
        new MdbRevalueRequestCommand(100),
        new MdbRevalueRequestExpandedCommand(100_000),
        new MdbRevalueLimitRequestCommand(),
        new MdbExpansionRequestIdCommand(new MdbVmcIdentification("ABC", "000000000001", "MODEL0000001", 0x0102)),
        new MdbExpansionEnableOptionsCommand(MdbLevel3Options.RemoteVend)
    };

    [Fact]
    public void DecoderRejectsTruncatedExtraAndBadChecksumCommandsWithoutThrowing()
    {
        Assert.False(_decoder.DecodeCommand([0x13]).ChecksumValid);
        Assert.False(_decoder.DecodeCommand([0x13, 0x00]).ChecksumValid);

        var extra = MdbChecksum.Append([0x13, 0x01, 0x00]);
        var decoded = _decoder.DecodeCommand(extra);
        Assert.True(decoded.ChecksumValid);
        Assert.Null(decoded.Command);
        Assert.NotEmpty(decoded.UnknownPayload.ToArray());
    }
}
