namespace MdbTestBench.Core.Protocol.Cashless;

public static class MdbCashlessResponseEncoder
{
    public static ReadOnlyMemory<byte> Ack() => new byte[] { 0x00 };
    public static ReadOnlyMemory<byte> Nak() => new byte[] { 0xFF };
    public static ReadOnlyMemory<byte> JustReset() => EncodeData([0x00]);
    public static ReadOnlyMemory<byte> VendDenied() => EncodeData([0x06]);
    public static ReadOnlyMemory<byte> EndSession() => EncodeData([0x07]);
    public static ReadOnlyMemory<byte> Cancelled() => EncodeData([0x08]);
    public static ReadOnlyMemory<byte> RevalueApproved() => EncodeData([0x0D]);
    public static ReadOnlyMemory<byte> RevalueDenied() => EncodeData([0x0E]);

    public static ReadOnlyMemory<byte> ReaderConfig(
        MdbFeatureLevel level,
        MdbPackedBcdCurrencyCode currencyCode,
        byte scaleFactor,
        byte decimalPlaces,
        byte maximumResponseTimeSeconds,
        byte miscellaneousOptions)
    {
        if (level is not (MdbFeatureLevel.Level1 or MdbFeatureLevel.Level2 or MdbFeatureLevel.Level3))
            throw new ArgumentOutOfRangeException(nameof(level));
        var data = new byte[8];
        data[0] = 0x01;
        data[1] = (byte)level;
        MdbCashlessBinary.WriteUInt16(data.AsSpan(2), currencyCode.Value);
        data[4] = scaleFactor;
        data[5] = decimalPlaces;
        data[6] = maximumResponseTimeSeconds;
        data[7] = miscellaneousOptions;
        return EncodeData(data);
    }

    public static ReadOnlyMemory<byte> BeginSession(ushort fundsAvailable)
    {
        var data = new byte[3];
        data[0] = 0x03;
        MdbCashlessBinary.WriteUInt16(data.AsSpan(1), fundsAvailable);
        return EncodeData(data);
    }

    public static ReadOnlyMemory<byte> VendApproved(ushort amount)
    {
        var data = new byte[3];
        data[0] = 0x05;
        MdbCashlessBinary.WriteUInt16(data.AsSpan(1), amount);
        return EncodeData(data);
    }

    public static ReadOnlyMemory<byte> PeripheralId(
        string manufacturerCode,
        string serialNumber,
        string modelNumber,
        ushort softwareVersionPackedBcd,
        MdbLevel3Options? options = null)
    {
        if (manufacturerCode.Length != 3 || serialNumber.Length != 12 || modelNumber.Length != 12)
            throw new ArgumentException("Peripheral ID strings must have MDB-defined fixed lengths.");
        var data = new byte[options.HasValue ? 34 : 30];
        data[0] = 0x09;
        System.Text.Encoding.ASCII.GetBytes(manufacturerCode, data.AsSpan(1, 3));
        System.Text.Encoding.ASCII.GetBytes(serialNumber, data.AsSpan(4, 12));
        System.Text.Encoding.ASCII.GetBytes(modelNumber, data.AsSpan(16, 12));
        MdbCashlessBinary.WriteUInt16(data.AsSpan(28), softwareVersionPackedBcd);
        if (options.HasValue) MdbCashlessBinary.WriteUInt32(data.AsSpan(30), (uint)options.Value);
        return EncodeData(data);
    }

    public static ReadOnlyMemory<byte> EncodeData(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty || data.Length > 35)
            throw new ArgumentOutOfRangeException(nameof(data), "MDB response data must contain 1 to 35 bytes.");
        return MdbChecksum.Append(data);
    }
}
