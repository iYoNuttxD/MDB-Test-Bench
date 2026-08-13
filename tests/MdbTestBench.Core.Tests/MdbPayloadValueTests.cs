using MdbTestBench.Core.Protocol.Cashless;

namespace MdbTestBench.Core.Tests;

public sealed class MdbPayloadValueTests
{
    [Fact]
    public void BigEndianHelpersRoundTripBoundaries()
    {
        Span<byte> bytes = stackalloc byte[4];
        MdbCashlessBinary.WriteUInt32(bytes, uint.MaxValue);
        Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, bytes.ToArray());
        Assert.Equal(uint.MaxValue, MdbCashlessBinary.ReadUInt32(bytes));
        MdbCashlessBinary.WriteUInt16(bytes, 0x1234);
        Assert.Equal((ushort)0x1234, MdbCashlessBinary.ReadUInt16(bytes));
    }

    [Fact]
    public void MonetaryScaleUsesSpecificationFormula()
    {
        var scale = new MdbMonetaryScale(5, 2);
        Assert.Equal(0.35m, scale.ToActual(7));
        Assert.Equal(7UL, scale.ToScaled(0.35m));
        Assert.Throws<ArgumentOutOfRangeException>(() => scale.ToScaled(0.36m));
    }

    [Fact]
    public void IsoCurrencyUsesPackedBcdWithLeadingOne()
    {
        var usd = MdbPackedBcdCurrencyCode.FromIso4217Numeric(840);
        var euro = MdbPackedBcdCurrencyCode.FromIso4217Numeric(978);
        Assert.Equal((ushort)0x1840, usd.Value);
        Assert.Equal((ushort)0x1978, euro.Value);
        Assert.Equal(840, usd.IsoNumericCode);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MdbPackedBcdCurrencyCode(0x1A40));
    }

    [Fact]
    public void ChecksumIsEightBitSumWithCarryDiscarded()
    {
        Assert.Equal(0x09, MdbChecksum.Calculate([0x13, 0x00, 0x01, 0xF4, 0x00, 0x01]));
        Assert.True(MdbChecksum.IsValid([0x13, 0x00, 0x01, 0xF4, 0x00, 0x01, 0x09]));
    }
}
