namespace MdbTestBench.Core.Logging;

public sealed class InMemoryMdbLogSink : IMdbLogSink
{
    public const int DefaultCapacity = 10_000;

    private readonly object _gate = new();
    private readonly List<MdbLogEntry> _entries = [];
    private readonly int _capacity;

    public InMemoryMdbLogSink(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    public event EventHandler<MdbLogEntry>? EntryAdded;

    public ValueTask WriteAsync(MdbLogEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _entries.Add(entry);
            if (_entries.Count > _capacity)
                _entries.RemoveRange(0, _entries.Count - _capacity);
        }
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
