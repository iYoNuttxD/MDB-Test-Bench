namespace MdbTestBench.Core.Logging;

public sealed class InMemoryMdbLogSink : IMdbLogSink
{
    private readonly object _gate = new();
    private readonly List<MdbLogEntry> _entries = [];

    public event EventHandler<MdbLogEntry>? EntryAdded;

    public ValueTask WriteAsync(MdbLogEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) _entries.Add(entry);
        EntryAdded?.Invoke(this, entry);
        return ValueTask.CompletedTask;
    }

    public IReadOnlyList<MdbLogEntry> Snapshot()
    {
        lock (_gate) return _entries.ToArray();
    }

    public void Clear()
    {
        lock (_gate) _entries.Clear();
    }
}
