namespace MdbTestBench.Core.Logging;

public interface IMdbLogSink
{
    ValueTask WriteAsync(MdbLogEntry entry, CancellationToken cancellationToken = default);
}

public sealed class NullMdbLogSink : IMdbLogSink
{
    public ValueTask WriteAsync(MdbLogEntry entry, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
