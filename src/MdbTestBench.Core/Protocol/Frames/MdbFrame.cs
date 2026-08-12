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
    string? InterpretedPayload = null)
{
    public static MdbFrame CommandFrame(
        MdbAddress source,
        MdbAddress destination,
        MdbCommandType command,
        MdbSubcommandType subcommand = MdbSubcommandType.None,
        ReadOnlyMemory<byte> payload = default) =>
        new(DateTimeOffset.UtcNow, MdbDirection.Tx, source, destination, command,
            subcommand, null, payload);
}
