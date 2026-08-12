using MdbTestBench.Core.Logging;
using MdbTestBench.Core.Protocol;

namespace MdbTestBench.Core.Tests;

public sealed class InMemoryLogTests
{
    private static MdbLogEntry Entry(string command) => new(DateTimeOffset.UtcNow, MdbDirection.Tx,
        "VMC", "DUT", command, "test", ReadOnlyMemory<byte>.Empty, MdbLogSeverity.Information);

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

    [Fact]
    public async Task SinkRetainsOnlyConfiguredCapacity()
    {
        var sink = new InMemoryMdbLogSink(capacity: 2);

        await sink.WriteAsync(Entry("one"));
        await sink.WriteAsync(Entry("two"));
        await sink.WriteAsync(Entry("three"));

        Assert.Equal(["two", "three"], sink.Snapshot().Select(entry => entry.Command));
    }

    [Fact]
    public void SinkRejectsInvalidCapacity() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryMdbLogSink(0));
}
