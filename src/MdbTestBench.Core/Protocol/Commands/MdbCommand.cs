namespace MdbTestBench.Core.Protocol.Commands;

public sealed record MdbCommand(
    MdbCommandType Type,
    MdbSubcommandType Subcommand = MdbSubcommandType.None,
    ReadOnlyMemory<byte> Payload = default);
