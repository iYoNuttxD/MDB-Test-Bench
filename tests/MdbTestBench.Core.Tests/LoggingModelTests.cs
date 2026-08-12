using MdbTestBench.Core.Logging;
using MdbTestBench.Core.Protocol;

namespace MdbTestBench.Core.Tests;

public sealed class LoggingModelTests
{
    [Fact]
    public void Entry_PreservesTimestampAndRawData()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var entry = new MdbLogEntry(timestamp, MdbDirection.Tx, "VMC", "Cashless",
            "Reset", "Reset request", new byte[] { 0x10, 0x20 }, MdbLogSeverity.Information);

        Assert.Equal(timestamp, entry.Timestamp);
        Assert.Equal("1020", entry.RawDataHex);
        Assert.Equal(MdbDirection.Tx, entry.Direction);
    }
}
