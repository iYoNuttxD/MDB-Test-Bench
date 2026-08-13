using System.Buffers.Binary;

namespace MdbTestBench.Core.Protocol.Cashless;

public readonly record struct MdbPackedBcdCurrencyCode
{
    public MdbPackedBcdCurrencyCode(ushort value)
    {
        if (value != ushort.MaxValue && !MdbCashlessBinary.IsPackedBcd(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Currency/country code must be packed BCD or FFFF (unknown).");
        Value = value;
    }

    public ushort Value { get; }
    public bool IsUnknown => Value == ushort.MaxValue;
    public bool IsIso4217 => !IsUnknown && ((Value >> 12) & 0x0F) == 1;
    public int? IsoNumericCode => IsIso4217
        ? ((Value >> 8) & 0x0F) * 100 + ((Value >> 4) & 0x0F) * 10 + (Value & 0x0F)
        : null;

    public static MdbPackedBcdCurrencyCode Unknown => new(ushort.MaxValue);

    public static MdbPackedBcdCurrencyCode FromIso4217Numeric(int numericCode)
    {
        if (numericCode is < 0 or > 999) throw new ArgumentOutOfRangeException(nameof(numericCode));
        var hundreds = numericCode / 100;
        var tens = numericCode / 10 % 10;
        var ones = numericCode % 10;
        return new((ushort)(0x1000 | (hundreds << 8) | (tens << 4) | ones));
    }
}

public readonly record struct MdbMonetaryScale
{
    public MdbMonetaryScale(byte scaleFactor, byte decimalPlaces)
    {
        if (scaleFactor == 0) throw new ArgumentOutOfRangeException(nameof(scaleFactor), "Scale factor must be greater than zero.");
        if (decimalPlaces > 9) throw new ArgumentOutOfRangeException(nameof(decimalPlaces), "Decimal places above 9 are not supported safely.");
        ScaleFactor = scaleFactor;
        DecimalPlaces = decimalPlaces;
    }

    public byte ScaleFactor { get; }
    public byte DecimalPlaces { get; }

    public decimal ToActual(ulong scaledValue) =>
        scaledValue * ScaleFactor / Pow10(DecimalPlaces);

    public ulong ToScaled(decimal actualValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actualValue);
        var scaled = actualValue * Pow10(DecimalPlaces) / ScaleFactor;
        if (scaled != decimal.Truncate(scaled) || scaled > ulong.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(actualValue), "Value is not exactly representable with this MDB scale.");
        return (ulong)scaled;
    }

    private static decimal Pow10(byte exponent)
    {
        decimal result = 1;
        for (var index = 0; index < exponent; index++) result *= 10;
        return result;
    }
}

public static class MdbCashlessBinary
{
    public static ushort ReadUInt16(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2) throw new ArgumentException("Two bytes are required.", nameof(bytes));
        return BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    public static uint ReadUInt32(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4) throw new ArgumentException("Four bytes are required.", nameof(bytes));
        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    public static void WriteUInt16(Span<byte> destination, ushort value)
    {
        if (destination.Length < 2) throw new ArgumentException("Two bytes are required.", nameof(destination));
        BinaryPrimitives.WriteUInt16BigEndian(destination, value);
    }

    public static void WriteUInt32(Span<byte> destination, uint value)
    {
        if (destination.Length < 4) throw new ArgumentException("Four bytes are required.", nameof(destination));
        BinaryPrimitives.WriteUInt32BigEndian(destination, value);
    }

    public static bool IsPackedBcd(ushort value) =>
        ((value >> 12) & 0x0F) <= 9 && ((value >> 8) & 0x0F) <= 9 &&
        ((value >> 4) & 0x0F) <= 9 && (value & 0x0F) <= 9;
}

public static class MdbChecksum
{
    public static byte Calculate(ReadOnlySpan<byte> bytes)
    {
        byte result = 0;
        foreach (var value in bytes) result = unchecked((byte)(result + value));
        return result;
    }

    public static bool IsValid(ReadOnlySpan<byte> block) =>
        block.Length >= 2 && Calculate(block[..^1]) == block[^1];

    public static byte[] Append(ReadOnlySpan<byte> bytes)
    {
        var block = new byte[bytes.Length + 1];
        bytes.CopyTo(block);
        block[^1] = Calculate(bytes);
        return block;
    }
}
