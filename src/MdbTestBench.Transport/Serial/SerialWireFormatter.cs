using System.Text;

namespace MdbTestBench.Transport.Serial;

public enum SerialWireFormat { BinaryBytes, AsciiHex }
public enum AsciiHexTerminator { None, CR, LF, CRLF }

public sealed record SerialWireFormatOptions
{
    public SerialWireFormat Format { get; init; } = SerialWireFormat.BinaryBytes;
    public AsciiHexTerminator Terminator { get; init; } = AsciiHexTerminator.None;
}

public static class SerialWireFormatter
{
    public static ReadOnlyMemory<byte> Encode(ReadOnlySpan<byte> payload, SerialWireFormatOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Format == SerialWireFormat.BinaryBytes) return payload.ToArray();

        var terminator = options.Terminator switch
        {
            AsciiHexTerminator.None => string.Empty,
            AsciiHexTerminator.CR => "\r",
            AsciiHexTerminator.LF => "\n",
            AsciiHexTerminator.CRLF => "\r\n",
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
        return Encoding.ASCII.GetBytes(Convert.ToHexString(payload) + terminator);
    }
}
