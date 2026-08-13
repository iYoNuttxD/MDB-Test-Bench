using MdbTestBench.Core.Protocol.Cashless;

namespace MdbTestBench.Core.Protocol.Frames;

public sealed record MdbFrame(
    DateTimeOffset Timestamp,
    MdbDirection Direction,
    MdbAddress Source,
    MdbAddress Destination,
    MdbCommandType Command,
    MdbSubcommandType Subcommand,
    MdbResponseType? Response,
    ReadOnlyMemory<byte> RawPayload,
    string? InterpretedPayload = null,
    byte? WireCommandByte = null,
    MdbCashlessDevice? CashlessDevice = null,
    IReadOnlyDictionary<string, string>? DecodedFields = null)
{
    // Source/Destination are logical application endpoints. RawBytes is the actual
    // 8-bit MDB block representation and never contains a synthetic VMC address byte.
    public ReadOnlyMemory<byte> RawBytes => RawPayload;

    public static MdbFrame CommandFrame(
        MdbAddress source,
        MdbAddress destination,
        MdbCommandType command,
        MdbSubcommandType subcommand = MdbSubcommandType.None,
        ReadOnlyMemory<byte> payload = default) =>
        new(DateTimeOffset.UtcNow, MdbDirection.Tx, source, destination, command,
            subcommand, null, payload);
}
