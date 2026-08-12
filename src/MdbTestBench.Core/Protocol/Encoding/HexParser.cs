namespace MdbTestBench.Core.Protocol.Encoding;

using System.Globalization;

public sealed record HexParseResult(bool IsValid, ReadOnlyMemory<byte> Bytes, string NormalizedHex, string? Error)
{
    public static HexParseResult Invalid(string error) => new(false, ReadOnlyMemory<byte>.Empty, string.Empty, error);
}

public static class HexParser
{
    public const int DefaultMaxBytes = 4_096;

    public static HexParseResult Parse(string? input, int maxBytes = DefaultMaxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        if (string.IsNullOrWhiteSpace(input)) return HexParseResult.Invalid("Enter at least one hexadecimal byte.");

        var compact = new string(input.Where(character => !char.IsWhiteSpace(character)).ToArray());
        if (compact.Length % 2 != 0) return HexParseResult.Invalid("Hexadecimal input must contain an even number of characters.");
        if (compact.Length / 2 > maxBytes) return HexParseResult.Invalid($"Payload exceeds the {maxBytes}-byte limit.");
        if (compact.Any(character => !Uri.IsHexDigit(character)))
            return HexParseResult.Invalid("Only hexadecimal characters (0-9, A-F) and spaces are allowed.");

        var bytes = Convert.FromHexString(compact);
        return new HexParseResult(true, bytes,
            string.Join(' ', bytes.Select(value => value.ToString("X2", CultureInfo.InvariantCulture))), null);
    }
}
