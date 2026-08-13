using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;

namespace MdbTestBench.Core.Tests;

public sealed class MdbCashlessEncoderTests
{
    private readonly MdbCashlessEncoder _encoder = new();

    [Theory]
    [MemberData(nameof(Level1Vectors))]
    public void EncodesMdb43Level1Vectors(MdbCashlessCommand command, byte[] expected) =>
        Assert.Equal(expected, _encoder.Encode(command).ToArray());

    public static TheoryData<MdbCashlessCommand, byte[]> Level1Vectors => new()
    {
        { new MdbResetCommand(), [0x10, 0x10] },
        { new MdbResetCommand(MdbCashlessDevice.CashlessDevice2), [0x60, 0x60] },
        { new MdbPollCommand(), [0x12, 0x12] },
        { new MdbSetupConfigCommand(MdbFeatureLevel.Level1, 0, 0, 0), [0x11, 0x00, 0x01, 0x00, 0x00, 0x00, 0x12] },
        { new MdbSetupMaxMinPricesCommand(500, 100), [0x11, 0x01, 0x01, 0xF4, 0x00, 0x64, 0x6B] },
        { new MdbReaderDisableCommand(), [0x14, 0x00, 0x14] },
        { new MdbReaderEnableCommand(), [0x14, 0x01, 0x15] },
        { new MdbReaderCancelCommand(), [0x14, 0x02, 0x16] },
        { new MdbVendRequestCommand(500, 1), [0x13, 0x00, 0x01, 0xF4, 0x00, 0x01, 0x09] },
        { new MdbVendCancelCommand(), [0x13, 0x01, 0x14] },
        { new MdbVendSuccessCommand(1), [0x13, 0x02, 0x00, 0x01, 0x16] },
        { new MdbVendFailureCommand(), [0x13, 0x03, 0x16] },
        { new MdbSessionCompleteCommand(), [0x13, 0x04, 0x17] },
        { new MdbCashSaleCommand(500, 1), [0x13, 0x05, 0x01, 0xF4, 0x00, 0x01, 0x0E] }
    };

    [Fact]
    public void EncodesLevel2Revalue()
    {
        Assert.Equal(new byte[] { 0x15, 0x00, 0x00, 0x64, 0x79 },
            _encoder.Encode(new MdbRevalueRequestCommand(100)).ToArray());
        Assert.Equal(new byte[] { 0x65, 0x01, 0x66 },
            _encoder.Encode(new MdbRevalueLimitRequestCommand(MdbCashlessDevice.CashlessDevice2)).ToArray());
    }

    [Fact]
    public void EncodesConfirmedExpansionCommands()
    {
        var identification = new MdbVmcIdentification("ABC", "000000000001", "MODEL0000001", 0x0102);
        var bytes = _encoder.Encode(new MdbExpansionRequestIdCommand(identification)).ToArray();
        Assert.Equal(32, bytes.Length);
        Assert.Equal(0x17, bytes[0]);
        Assert.Equal(0x00, bytes[1]);
        Assert.True(MdbChecksum.IsValid(bytes));

        Assert.Equal(new byte[] { 0x17, 0x04, 0x00, 0x00, 0x00, 0x42, 0x5D },
            _encoder.Encode(new MdbExpansionEnableOptionsCommand(
                MdbLevel3Options.ThirtyTwoBitMonetary | MdbLevel3Options.RemoteVend)).ToArray());
    }
}
