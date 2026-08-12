using MdbTestBench.Core.Logging;
using MdbTestBench.Core.Protocol;

namespace MdbTestBench.Core.Tests;

public sealed class InMemoryLogTests
{
    [Fact]
    public async Task SinkCapturesClearsAndFormatsEntries()
    {
        var sink = new InMemoryMdbLogSink();
        var entry = new MdbLogEntry(DateTimeOffset.UtcNow, MdbDirection.Tx, "VMC", "DUT",
            "VEND REQUEST", "Price: 5.00", new byte[] { 0x13, 0x00 }, MdbLogSeverity.Information);
        await sink.WriteAsync(entry);

        Assert.Single(sink.Snapshot());
        Assert.Contains("13 00", MdbLogFormatter.ToText(sink.Snapshot()), StringComparison.Ordinal);
        Assert.Contains("VEND REQUEST", MdbLogFormatter.ToJson(sink.Snapshot()), StringComparison.Ordinal);

        sink.Clear();
        Assert.Empty(sink.Snapshot());
    }
}
