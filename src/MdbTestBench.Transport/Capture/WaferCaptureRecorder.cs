using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace MdbTestBench.Transport.Capture;

public sealed class CaptureSizeLimitReachedException(long maximumBytes)
    : IOException($"Capture reached its configured limit of {maximumBytes} bytes.");

public sealed class WaferCaptureRecorder : IAsyncDisposable
{
    private enum RecorderState { AcceptingEvents, Stopping, Finalized, Disposed }

    private readonly SemaphoreSlim _gate = new(1, 1);
    private StreamWriter? _writer;
    private readonly WaferCaptureDocument _header;
    private readonly long _maximumBytes;
    private readonly WaferCaptureInterpreter _interpreter;
    private readonly List<WaferProbe> _probes = [];
    private readonly object _probeGate = new();
    private long _sequence;
    private long _previousTick;
    private long _previousRxTick;
    private long _readChunkIndex;
    private RecorderState _state = RecorderState.AcceptingEvents;
    private WaferCaptureArtifact? _artifact;

    private WaferCaptureRecorder(WaferCaptureDocument header, string spoolPath, long maximumBytes,
        WaferCaptureInterpreter interpreter, StreamWriter writer)
    {
        _header = header;
        EventSpoolPath = spoolPath;
        _maximumBytes = maximumBytes;
        _interpreter = interpreter;
        _writer = writer;
    }

    public event EventHandler<WaferCaptureEvent>? EventRecorded;
    public string EventSpoolPath { get; }
    public long CaptureSizeBytes { get; private set; }
    public bool SizeLimitReached { get; private set; }

    public static Task<WaferCaptureRecorder> StartAsync(WaferCaptureDocument header, string temporaryDirectory,
        long maximumBytes = WaferCaptureFormat.DefaultMaximumBytes, WaferCaptureInterpreter? interpreter = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1024);
        Directory.CreateDirectory(temporaryDirectory);
        var path = Path.Combine(temporaryDirectory, $"{header.CaptureId}.events.jsonl");
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var writer = new StreamWriter(stream, new UTF8Encoding(false), 64 * 1024, leaveOpen: false) { AutoFlush = true };
        return Task.FromResult(new WaferCaptureRecorder(header, path, maximumBytes,
            interpreter ?? new WaferCaptureInterpreter(), writer));
    }

    public Task RecordRawAsync(WaferCaptureDirection direction, ReadOnlyMemory<byte> bytes, string operation,
        long operationStartedTick, long operationEndedTick, string? transportState,
        CancellationToken cancellationToken = default)
    {
        if (bytes.IsEmpty) throw new ArgumentException("A raw capture event cannot be empty.", nameof(bytes));
        var evt = WaferCaptureEvent.Raw(0, DateTimeOffset.UtcNow, operationEndedTick, null, direction,
            bytes.Span, operation, null,
            ToMicroseconds(operationEndedTick - operationStartedTick),
            null, transportState,
            _interpreter.Interpret(direction, bytes.Span));
        return AppendAsync(evt, cancellationToken);
    }

    public Task AddMarkerAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Marker text is required.", nameof(text));
        return AppendSimpleAsync(WaferCaptureEventType.Marker, "OperatorMarker", text.Trim(), null, null, cancellationToken);
    }

    public Task RecordErrorAsync(string kind, string message, CancellationToken cancellationToken = default) =>
        AppendSimpleAsync(WaferCaptureEventType.Error, "TransportError", null, kind, message, cancellationToken);

    public Task RecordTransportStateAsync(string state, CancellationToken cancellationToken = default) =>
        AppendSimpleAsync(WaferCaptureEventType.TransportState, state, null, null, null, cancellationToken);

    public void AddProbe(WaferProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        lock (_probeGate) _probes.Add(probe);
    }

    public async Task<WaferCaptureArtifact> StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_artifact is not null) return _artifact;
            ObjectDisposedException.ThrowIf(_state == RecorderState.Disposed, this);
            _state = RecorderState.Stopping;
            await CloseWriterAsync();
            var timing = _header.Capture with { EndedAtUtc = DateTimeOffset.UtcNow };
            WaferProbe[] probes;
            lock (_probeGate) probes = _probes.ToArray();
            var completed = _header with
            {
                Capture = timing,
                Probes = probes,
                Statistics = new WaferCaptureAnalyzer().Analyze(ReadSpoolEvents(), timing),
                Events = []
            };
            _artifact = new(completed, EventSpoolPath, CaptureSizeBytes, SizeLimitReached);
            _state = RecorderState.Finalized;
            return _artifact;
        }
        finally { _gate.Release(); }
    }

    private Task AppendSimpleAsync(WaferCaptureEventType type, string operation, string? text,
        string? errorKind, string? errorMessage, CancellationToken cancellationToken)
    {
        var tick = Stopwatch.GetTimestamp();
        return AppendAsync(new WaferCaptureEvent
        {
            Sequence = 0, Type = type, TimestampUtc = DateTimeOffset.UtcNow,
            MonotonicTimestamp = tick, DeltaMicroseconds = null, Operation = operation,
            Text = text, ErrorKind = errorKind, ErrorMessage = errorMessage,
            TransportState = type == WaferCaptureEventType.TransportState ? operation : null
        }, cancellationToken);
    }

    private async Task AppendAsync(WaferCaptureEvent item, CancellationToken cancellationToken)
    {
        WaferCaptureEvent? recorded = null;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_state == RecorderState.Disposed, this);
            if (_state != RecorderState.AcceptingEvents)
                throw new InvalidOperationException("The capture is no longer accepting events.");
            var appendTick = Stopwatch.GetTimestamp();
            item = item with
            {
                Sequence = NextSequence(),
                MonotonicTimestamp = appendTick,
                DeltaMicroseconds = Delta(appendTick),
                ReadChunkIndex = item.Direction == WaferCaptureDirection.Rx ? ++_readChunkIndex : item.ReadChunkIndex,
                GapFromPreviousRxMicroseconds = item.Direction == WaferCaptureDirection.Rx ? RxGap(appendTick) : item.GapFromPreviousRxMicroseconds
            };
            var line = JsonSerializer.Serialize(item, WaferCaptureSerializer.SpoolJsonOptions);
            var addedBytes = Encoding.UTF8.GetByteCount(line) + 1L;
            if (CaptureSizeBytes + addedBytes > _maximumBytes)
            {
                SizeLimitReached = true;
                _state = RecorderState.Stopping;
                throw new CaptureSizeLimitReachedException(_maximumBytes);
            }
            await _writer!.WriteLineAsync(line.AsMemory(), cancellationToken);
            CaptureSizeBytes += addedBytes;
            recorded = item;
        }
        finally { _gate.Release(); }
        if (recorded is not null) NotifyEventRecorded(recorded);
    }

    private long NextSequence() => Interlocked.Increment(ref _sequence);
    private double? Delta(long tick)
    {
        var previous = Interlocked.Exchange(ref _previousTick, tick);
        return previous == 0 ? null : ToMicroseconds(tick - previous);
    }
    private double? RxGap(long tick)
    {
        var previous = Interlocked.Exchange(ref _previousRxTick, tick);
        return previous == 0 ? null : ToMicroseconds(tick - previous);
    }
    private static double ToMicroseconds(long ticks) => ticks * 1_000_000d / Stopwatch.Frequency;

    private void NotifyEventRecorded(WaferCaptureEvent recorded)
    {
        foreach (EventHandler<WaferCaptureEvent> handler in EventRecorded?.GetInvocationList() ?? [])
        {
            try { handler(this, recorded); }
            catch (Exception) { /* A presentation subscriber must never corrupt or stop raw evidence capture. */ }
        }
    }

    private IEnumerable<WaferCaptureEvent> ReadSpoolEvents()
    {
        using var reader = new StreamReader(EventSpoolPath, Encoding.UTF8, true, 64 * 1024);
        while (reader.ReadLine() is { } line)
            yield return JsonSerializer.Deserialize<WaferCaptureEvent>(line, WaferCaptureSerializer.SpoolJsonOptions)
                ?? throw new InvalidDataException("Capture spool contains an empty event.");
    }

    private async Task CloseWriterAsync()
    {
        if (_writer is null) return;
        await _writer.FlushAsync(CancellationToken.None);
        await _writer.DisposeAsync();
        _writer = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_state == RecorderState.Disposed) return;
        Exception? finalizationFailure = null;
        if (_artifact is null)
        {
            try { await StopAsync(CancellationToken.None); }
            catch (Exception exception) { finalizationFailure = exception; }
        }
        await _gate.WaitAsync();
        try
        {
            if (_state == RecorderState.Disposed) return;
            await CloseWriterAsync();
            _state = RecorderState.Disposed;
        }
        finally { _gate.Release(); }
        GC.SuppressFinalize(this);
        if (finalizationFailure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(finalizationFailure).Throw();
    }
}
