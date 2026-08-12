using MdbTestBench.Core.Protocol.Encoding;

namespace MdbTestBench.Core.Tests;

public sealed class HexParserTests
{
    [Theory]
    [InlineData("01 A0 ff", "01 A0 FF")]
    [InlineData("01A0FF", "01 A0 FF")]
    [InlineData("01\nA0\tFF", "01 A0 FF")]
    public void ValidInputParsesSafely(string input, string normalized)
    {
        var result = HexParser.Parse(input);
        Assert.True(result.IsValid);
        Assert.Equal(normalized, result.NormalizedHex);
        Assert.Equal(new byte[] { 0x01, 0xA0, 0xFF }, result.Bytes.ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("GG")]
    [InlineData("01-ZZ")]
    public void InvalidInputReturnsFriendlyError(string input)
    {
        var result = HexParser.Parse(input);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Error!);
    }

    [Fact]
    public void OversizedInputIsRejected()
    {
        Assert.False(HexParser.Parse("00 01", maxBytes: 1).IsValid);
    }
}
