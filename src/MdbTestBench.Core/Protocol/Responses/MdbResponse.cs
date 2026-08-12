namespace MdbTestBench.Core.Protocol.Responses;

public sealed record MdbResponse(
    MdbResponseType Type,
    ReadOnlyMemory<byte> RawPayload = default,
    string? Description = null);
