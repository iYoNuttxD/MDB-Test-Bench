using MdbTestBench.Core.Protocol;

namespace MdbTestBench.Core.Logging;

public enum MdbLogSeverity { Trace, Debug, Information, Warning, Error, Critical }

public sealed record MdbLogEntry(
    DateTimeOffset Timestamp,
    MdbDirection Direction,
    string Source,
    string Destination,
    string Command,
    string DecodedDescription,
    ReadOnlyMemory<byte> RawData,
    MdbLogSeverity Severity)
{
    public string RawDataHex => Convert.ToHexString(RawData.Span);
}
