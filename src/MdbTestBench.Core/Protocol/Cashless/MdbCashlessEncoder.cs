namespace MdbTestBench.Core.Protocol.Cashless;

public interface IMdbCashlessEncoder
{
    ReadOnlyMemory<byte> Encode(MdbCashlessCommand command);
}

public sealed class MdbCashlessEncoder : IMdbCashlessEncoder
{
    public ReadOnlyMemory<byte> Encode(MdbCashlessCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var data = command switch
        {
            MdbResetCommand => Array.Empty<byte>(),
            MdbSetupConfigCommand value => EncodeSetupConfig(value),
            MdbSetupMaxMinPricesCommand value => EncodeMaxMin(value),
            MdbSetupMaxMinPricesExpandedCommand value => EncodeExpandedMaxMin(value),
            MdbPollCommand => Array.Empty<byte>(),
            MdbReaderDisableCommand => [0x00],
            MdbReaderEnableCommand => [0x01],
            MdbReaderCancelCommand => [0x02],
            MdbVendRequestCommand value => EncodeTwoUInt16(0x00, value.ItemPrice, value.ItemNumber),
            MdbVendCancelCommand => [0x01],
            MdbVendSuccessCommand value => EncodeUInt16(0x02, value.ItemNumber),
            MdbVendFailureCommand => [0x03],
            MdbSessionCompleteCommand => [0x04],
            MdbCashSaleCommand value => EncodeTwoUInt16(0x05, value.ItemPrice, value.ItemNumber),
            MdbRevalueRequestCommand value => EncodeUInt16(0x00, value.Amount),
            MdbRevalueRequestExpandedCommand value => EncodeUInt32(0x00, value.Amount),
            MdbRevalueLimitRequestCommand => [0x01],
            MdbExpansionRequestIdCommand value => EncodeRequestId(value.Identification),
            MdbExpansionEnableOptionsCommand value => EncodeOptions(value.Options),
            _ => throw new NotSupportedException($"{command.GetType().Name} is not implemented by the MDB 4.3 cashless encoder.")
        };

        var commandByte = MdbCashlessAddressing.GetCommandByte(command.Device, command.CommandType);
        var withoutChecksum = new byte[data.Length + 1];
        withoutChecksum[0] = commandByte;
        data.CopyTo(withoutChecksum, 1);
        if (withoutChecksum.Length > 35)
            throw new ArgumentOutOfRangeException(nameof(command), "MDB command exceeds the 36-byte block limit including checksum.");
        return MdbChecksum.Append(withoutChecksum);
    }

    private static byte[] EncodeSetupConfig(MdbSetupConfigCommand command)
    {
        if (command.VmcFeatureLevel is not (MdbFeatureLevel.Level1 or MdbFeatureLevel.Level2 or MdbFeatureLevel.Level3))
            throw new ArgumentOutOfRangeException(nameof(command), "VMC feature level must be Level1, Level2, or Level3.");
        return [0x00, (byte)command.VmcFeatureLevel, command.DisplayColumns, command.DisplayRows, command.DisplayInformation];
    }

    private static byte[] EncodeMaxMin(MdbSetupMaxMinPricesCommand command)
    {
        var data = new byte[5];
        data[0] = 0x01;
        MdbCashlessBinary.WriteUInt16(data.AsSpan(1), command.MaximumPrice);
        MdbCashlessBinary.WriteUInt16(data.AsSpan(3), command.MinimumPrice);
        return data;
    }

    private static byte[] EncodeExpandedMaxMin(MdbSetupMaxMinPricesExpandedCommand command)
    {
        if (!command.CurrencyCode.IsIso4217)
            throw new ArgumentOutOfRangeException(nameof(command), "Expanded currency mode requires an ISO 4217 packed-BCD code.");
        var data = new byte[11];
        data[0] = 0x01;
        MdbCashlessBinary.WriteUInt32(data.AsSpan(1), command.MaximumPrice);
        MdbCashlessBinary.WriteUInt32(data.AsSpan(5), command.MinimumPrice);
        MdbCashlessBinary.WriteUInt16(data.AsSpan(9), command.CurrencyCode.Value);
        return data;
    }

    private static byte[] EncodeTwoUInt16(byte subcommand, ushort first, ushort second)
    {
        var data = new byte[5];
        data[0] = subcommand;
        MdbCashlessBinary.WriteUInt16(data.AsSpan(1), first);
        MdbCashlessBinary.WriteUInt16(data.AsSpan(3), second);
        return data;
    }

    private static byte[] EncodeUInt16(byte subcommand, ushort value)
    {
        var data = new byte[3];
        data[0] = subcommand;
        MdbCashlessBinary.WriteUInt16(data.AsSpan(1), value);
        return data;
    }

    private static byte[] EncodeUInt32(byte subcommand, uint value)
    {
        var data = new byte[5];
        data[0] = subcommand;
        MdbCashlessBinary.WriteUInt32(data.AsSpan(1), value);
        return data;
    }

    private static byte[] EncodeRequestId(MdbVmcIdentification identification)
    {
        ArgumentNullException.ThrowIfNull(identification);
        ValidateAscii(identification.ManufacturerCode, 3, nameof(identification.ManufacturerCode));
        ValidateAscii(identification.SerialNumber, 12, nameof(identification.SerialNumber));
        ValidateAscii(identification.ModelNumber, 12, nameof(identification.ModelNumber));
        if (!MdbCashlessBinary.IsPackedBcd(identification.SoftwareVersionPackedBcd))
            throw new ArgumentOutOfRangeException(nameof(identification), "Software version must be two packed-BCD bytes.");

        var data = new byte[30];
        data[0] = 0x00;
        System.Text.Encoding.ASCII.GetBytes(identification.ManufacturerCode, data.AsSpan(1, 3));
        System.Text.Encoding.ASCII.GetBytes(identification.SerialNumber, data.AsSpan(4, 12));
        System.Text.Encoding.ASCII.GetBytes(identification.ModelNumber, data.AsSpan(16, 12));
        MdbCashlessBinary.WriteUInt16(data.AsSpan(28), identification.SoftwareVersionPackedBcd);
        return data;
    }

    private static byte[] EncodeOptions(MdbLevel3Options options)
    {
        const MdbLevel3Options allDefined =
            MdbLevel3Options.FileTransportLayer | MdbLevel3Options.ThirtyTwoBitMonetary |
            MdbLevel3Options.MultiCurrencyAndLanguage | MdbLevel3Options.NegativeVend |
            MdbLevel3Options.DataEntry | MdbLevel3Options.AlwaysIdle | MdbLevel3Options.RemoteVend |
            MdbLevel3Options.BasketPartialRefundOptionsPrice | MdbLevel3Options.Coupon |
            MdbLevel3Options.AskBeginSession | MdbLevel3Options.EnhancedItemNumberInformation;
        if ((options & ~allDefined) != 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Reserved MDB Level 3 option bits must be zero.");
        var data = new byte[5];
        data[0] = 0x04;
        MdbCashlessBinary.WriteUInt32(data.AsSpan(1), (uint)options);
        return data;
    }

    private static void ValidateAscii(string value, int exactLength, string parameterName)
    {
        if (value.Length != exactLength || value.Any(character => character is < ' ' or > '~'))
            throw new ArgumentException($"{parameterName} must contain exactly {exactLength} printable ASCII characters.", parameterName);
    }
}
