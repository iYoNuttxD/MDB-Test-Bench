using System.Text;
using MdbTestBench.Transport.Serial;

namespace MdbTestBench.Transport.Tests;

public sealed class SerialWireFormatterTests
{
    [Fact]
    public void BinaryBytesPreservesPayload()
    {
        var result = SerialWireFormatter.Encode(new byte[] { 0x01, 0xA0 }, new SerialWireFormatOptions());
        Assert.Equal(new byte[] { 0x01, 0xA0 }, result.ToArray());
    }

    [Theory]
    [InlineData(AsciiHexTerminator.None, "01A0")]
    [InlineData(AsciiHexTerminator.CR, "01A0\r")]
    [InlineData(AsciiHexTerminator.LF, "01A0\n")]
    [InlineData(AsciiHexTerminator.CRLF, "01A0\r\n")]
    public void AsciiHexUsesSelectedTerminator(AsciiHexTerminator terminator, string expected)
    {
        var result = SerialWireFormatter.Encode(new byte[] { 0x01, 0xA0 }, new SerialWireFormatOptions
        {
            Format = SerialWireFormat.AsciiHex,
            Terminator = terminator
        });
        Assert.Equal(expected, Encoding.ASCII.GetString(result.Span));
    }
}
