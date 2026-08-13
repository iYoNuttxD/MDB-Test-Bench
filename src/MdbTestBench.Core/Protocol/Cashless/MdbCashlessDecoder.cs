namespace MdbTestBench.Core.Protocol.Cashless;

public interface IMdbCashlessDecoder
{
    MdbDecodedCommand DecodeCommand(ReadOnlySpan<byte> block);
    MdbCashlessResponse DecodeResponse(ReadOnlySpan<byte> block, MdbCashlessDecodeOptions? options = null);
}

public sealed class MdbCashlessDecoder : IMdbCashlessDecoder
{
    public MdbDecodedCommand DecodeCommand(ReadOnlySpan<byte> block)
    {
        var raw = block.ToArray();
        if (block.Length < 2)
            return InvalidCommand(raw, "MDB command block requires a command byte and checksum.");
        if (block.Length > 36)
            return InvalidCommand(raw, "MDB command block exceeds the 36-byte limit.");
        if (!MdbChecksum.IsValid(block))
            return InvalidCommand(raw, "MDB command checksum is invalid.");
        if (!MdbCashlessAddressing.TryDecode(block[0], out var device, out var commandType))
            return new(null, null, block[0], raw, true, "Unknown or non-cashless MDB command address.", block[1..^1].ToArray());

        var data = block[1..^1];
        try
        {
            var command = DecodeKnownCommand(device, commandType, data);
            if (command is null)
                return new(null, device, block[0], raw, true, "Unknown subcommand or unsupported payload shape.", data.ToArray());
            return new(command, device, block[0], raw, true);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return new(null, device, block[0], raw, true, exception.Message, data.ToArray());
        }
    }

    public MdbCashlessResponse DecodeResponse(ReadOnlySpan<byte> block, MdbCashlessDecodeOptions? options = null)
    {
        options ??= new MdbCashlessDecodeOptions();
        var raw = block.ToArray();
        if (block.Length == 0) return new MdbMalformedCashlessResponse("Empty MDB response.", raw);
        if (block.Length == 1)
        {
            return block[0] switch
            {
                0x00 => new MdbAckResponse(raw),
                0xFF => new MdbNakResponse(raw),
                _ => new MdbMalformedCashlessResponse("Single-byte response is neither ACK nor NAK.", raw)
            };
        }
        if (block.Length > 36) return new MdbMalformedCashlessResponse("MDB response exceeds the 36-byte block limit.", raw);
        if (!MdbChecksum.IsValid(block)) return new MdbMalformedCashlessResponse("MDB response checksum is invalid.", raw);

        var data = block[..^1];
        try
        {
            return DecodeKnownResponse(data, raw, options);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or System.Text.DecoderFallbackException)
        {
            return new MdbMalformedCashlessResponse(exception.Message, raw);
        }
    }

    private static MdbCashlessCommand? DecodeKnownCommand(
        MdbCashlessDevice device,
        MdbCommandType command,
        ReadOnlySpan<byte> data) => command switch
    {
        MdbCommandType.Reset when data.IsEmpty => new MdbResetCommand(device),
        MdbCommandType.Poll when data.IsEmpty => new MdbPollCommand(device),
        MdbCommandType.Setup => DecodeSetup(device, data),
        MdbCommandType.Reader => DecodeReader(device, data),
        MdbCommandType.Vend => DecodeVend(device, data),
        MdbCommandType.Revalue => DecodeRevalue(device, data),
        MdbCommandType.Expansion => DecodeExpansion(device, data),
        _ => null
    };

    private static MdbCashlessCommand? DecodeSetup(MdbCashlessDevice device, ReadOnlySpan<byte> data)
    {
        if (data.Length == 5 && data[0] == 0x00 && data[1] is >= 1 and <= 3)
            return new MdbSetupConfigCommand((MdbFeatureLevel)data[1], data[2], data[3], data[4], device);
        if (data.Length == 5 && data[0] == 0x01)
            return new MdbSetupMaxMinPricesCommand(
                MdbCashlessBinary.ReadUInt16(data[1..3]), MdbCashlessBinary.ReadUInt16(data[3..5]), device);
        if (data.Length == 11 && data[0] == 0x01)
            return new MdbSetupMaxMinPricesExpandedCommand(
                MdbCashlessBinary.ReadUInt32(data[1..5]), MdbCashlessBinary.ReadUInt32(data[5..9]),
                new MdbPackedBcdCurrencyCode(MdbCashlessBinary.ReadUInt16(data[9..11])), device);
        return null;
    }

    private static MdbCashlessCommand? DecodeReader(MdbCashlessDevice device, ReadOnlySpan<byte> data) =>
        data.Length == 1 ? data[0] switch
        {
            0x00 => new MdbReaderDisableCommand(device),
            0x01 => new MdbReaderEnableCommand(device),
            0x02 => new MdbReaderCancelCommand(device),
            _ => null
        } : null;

    private static MdbCashlessCommand? DecodeVend(MdbCashlessDevice device, ReadOnlySpan<byte> data)
    {
        if (data.Length == 5 && data[0] is 0x00 or 0x05)
        {
            var price = MdbCashlessBinary.ReadUInt16(data[1..3]);
            var item = MdbCashlessBinary.ReadUInt16(data[3..5]);
            return data[0] == 0x00
                ? new MdbVendRequestCommand(price, item, device)
                : new MdbCashSaleCommand(price, item, device);
        }
        if (data.Length == 3 && data[0] == 0x02)
            return new MdbVendSuccessCommand(MdbCashlessBinary.ReadUInt16(data[1..3]), device);
        if (data.Length != 1) return null;
        return data[0] switch
        {
            0x01 => new MdbVendCancelCommand(device),
            0x03 => new MdbVendFailureCommand(device),
            0x04 => new MdbSessionCompleteCommand(device),
            _ => null
        };
    }

    private static MdbCashlessCommand? DecodeRevalue(MdbCashlessDevice device, ReadOnlySpan<byte> data)
    {
        if (data.Length == 3 && data[0] == 0x00)
            return new MdbRevalueRequestCommand(MdbCashlessBinary.ReadUInt16(data[1..3]), device);
        if (data.Length == 5 && data[0] == 0x00)
            return new MdbRevalueRequestExpandedCommand(MdbCashlessBinary.ReadUInt32(data[1..5]), device);
        return data.Length == 1 && data[0] == 0x01 ? new MdbRevalueLimitRequestCommand(device) : null;
    }

    private static MdbCashlessCommand? DecodeExpansion(MdbCashlessDevice device, ReadOnlySpan<byte> data)
    {
        if (data.Length == 30 && data[0] == 0x00)
        {
            var identification = new MdbVmcIdentification(
                DecodeAscii(data[1..4]), DecodeAscii(data[4..16]), DecodeAscii(data[16..28]),
                MdbCashlessBinary.ReadUInt16(data[28..30]));
            return new MdbExpansionRequestIdCommand(identification, device);
        }
        if (data.Length == 5 && data[0] == 0x04)
            return new MdbExpansionEnableOptionsCommand((MdbLevel3Options)MdbCashlessBinary.ReadUInt32(data[1..5]), device);
        return null;
    }

    private static MdbCashlessResponse DecodeKnownResponse(
        ReadOnlySpan<byte> data,
        ReadOnlyMemory<byte> raw,
        MdbCashlessDecodeOptions options)
    {
        if (data.IsEmpty) return new MdbMalformedCashlessResponse("Response contains only a checksum.", raw);
        return data[0] switch
        {
            0x00 when data.Length == 1 => new MdbJustResetResponse(raw),
            0x01 when data.Length == 8 => DecodeReaderConfig(data, raw),
            0x02 when data.Length is >= 2 and <= 34 => new MdbDisplayRequestResponse(data[1], DecodeAscii(data[2..]), raw),
            0x03 => DecodeBeginSession(data, raw, options),
            0x04 when data.Length == 1 => new MdbSessionCancelRequestResponse(raw),
            0x05 => DecodeVendApproved(data, raw, options),
            0x06 when data.Length == 1 => new MdbVendDeniedResponse(raw),
            0x07 when data.Length == 1 => new MdbEndSessionResponse(raw),
            0x08 when data.Length == 1 => new MdbCancelledResponse(raw),
            0x09 when data.Length is 30 or 34 => DecodePeripheralId(data, raw),
            0x0A when data.Length == 2 => new MdbMalfunctionResponse(data[1], raw),
            0x0B when data.Length is 1 or 2 => new MdbCommandOutOfSequenceResponse(data.Length == 2 ? data[1] : null, raw),
            0x0D when data.Length == 1 => new MdbRevalueApprovedResponse(raw),
            0x0E when data.Length == 1 => new MdbRevalueDeniedResponse(raw),
            0x0F => DecodeRevalueLimit(data, raw, options),
            >= 0x1B and <= 0x1F or 0xFF => new UnknownExpansionResponse(data[0], data[1..].ToArray(), raw),
            _ when IsImplementedResponseCode(data[0]) => new MdbMalformedCashlessResponse($"Invalid payload length for response 0x{data[0]:X2}.", raw),
            _ => new MdbUnknownCashlessResponse(data[0], data[1..].ToArray(), raw)
        };
    }

    private static MdbReaderConfigResponse DecodeReaderConfig(ReadOnlySpan<byte> data, ReadOnlyMemory<byte> raw)
    {
        if (data[1] is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(data), "Unknown reader feature level.");
        return new((MdbFeatureLevel)data[1], new(MdbCashlessBinary.ReadUInt16(data[2..4])),
            data[4], data[5], data[6], data[7], raw);
    }

    private static MdbCashlessResponse DecodeBeginSession(
        ReadOnlySpan<byte> data, ReadOnlyMemory<byte> raw, MdbCashlessDecodeOptions options)
    {
        if (data.Length == 3 && options.FeatureLevel == MdbFeatureLevel.Level1)
            return new MdbBeginSessionResponse(MdbCashlessBinary.ReadUInt16(data[1..3]), null, null, null,
                null, null, null, false, raw);
        if (data.Length == 10 && options.FeatureLevel is MdbFeatureLevel.Level2 or MdbFeatureLevel.Level3 &&
            !options.ExpandedCurrencyMode)
            return new MdbBeginSessionResponse(MdbCashlessBinary.ReadUInt16(data[1..3]),
                MdbCashlessBinary.ReadUInt32(data[3..7]), data[7], MdbCashlessBinary.ReadUInt16(data[8..10]),
                null, null, null, false, raw);
        if (data.Length == 17 && options.FeatureLevel == MdbFeatureLevel.Level3 && options.ExpandedCurrencyMode)
            return new MdbBeginSessionResponse(MdbCashlessBinary.ReadUInt32(data[1..5]),
                MdbCashlessBinary.ReadUInt32(data[5..9]), data[9], MdbCashlessBinary.ReadUInt16(data[10..12]),
                MdbCashlessBinary.ReadUInt16(data[12..14]), new(MdbCashlessBinary.ReadUInt16(data[14..16])),
                data[16], true, raw);
        return new MdbMalformedCashlessResponse("BEGIN SESSION payload does not match the configured feature/currency mode.", raw);
    }

    private static MdbCashlessResponse DecodeVendApproved(
        ReadOnlySpan<byte> data, ReadOnlyMemory<byte> raw, MdbCashlessDecodeOptions options)
    {
        if (data.Length == 3 && !options.ExpandedCurrencyMode)
            return new MdbVendApprovedResponse(MdbCashlessBinary.ReadUInt16(data[1..3]), false, raw);
        if (data.Length == 5 && options.FeatureLevel == MdbFeatureLevel.Level3 && options.ExpandedCurrencyMode)
            return new MdbVendApprovedResponse(MdbCashlessBinary.ReadUInt32(data[1..5]), true, raw);
        return new MdbMalformedCashlessResponse("VEND APPROVED payload does not match the configured currency mode.", raw);
    }

    private static MdbCashlessResponse DecodeRevalueLimit(
        ReadOnlySpan<byte> data, ReadOnlyMemory<byte> raw, MdbCashlessDecodeOptions options)
    {
        if (data.Length == 3 && options.FeatureLevel is MdbFeatureLevel.Level2 or MdbFeatureLevel.Level3 &&
            !options.ExpandedCurrencyMode)
            return new MdbRevalueLimitResponse(MdbCashlessBinary.ReadUInt16(data[1..3]), false, raw);
        if (data.Length == 5 && options.FeatureLevel == MdbFeatureLevel.Level3 && options.ExpandedCurrencyMode)
            return new MdbRevalueLimitResponse(MdbCashlessBinary.ReadUInt32(data[1..5]), true, raw);
        return new MdbMalformedCashlessResponse("REVALUE LIMIT payload does not match the configured currency mode.", raw);
    }

    private static MdbPeripheralIdResponse DecodePeripheralId(ReadOnlySpan<byte> data, ReadOnlyMemory<byte> raw) =>
        new(DecodeAscii(data[1..4]), DecodeAscii(data[4..16]), DecodeAscii(data[16..28]),
            MdbCashlessBinary.ReadUInt16(data[28..30]),
            data.Length == 34 ? (MdbLevel3Options)MdbCashlessBinary.ReadUInt32(data[30..34]) : null, raw);

    private static string DecodeAscii(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
            if (value > 0x7F) throw new System.Text.DecoderFallbackException("Expected ASCII data.");
        return System.Text.Encoding.ASCII.GetString(bytes);
    }

    private static bool IsImplementedResponseCode(byte code) =>
        code is <= 0x0B and not 0x0C or >= 0x0D and <= 0x0F;

    private static MdbDecodedCommand InvalidCommand(ReadOnlyMemory<byte> raw, string error) =>
        new(null, null, raw.IsEmpty ? null : raw.Span[0], raw, false, error);
}
