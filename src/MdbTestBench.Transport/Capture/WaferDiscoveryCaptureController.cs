using System.Diagnostics;
using MdbTestBench.Transport.Abstractions;
using MdbTestBench.Transport.Serial;

namespace MdbTestBench.Transport.Capture;

public sealed class WaferDiscoveryCaptureController : IAsyncDisposable
{
    private readonly IRawByteTransport _transport;
    private readonly WaferCaptureRecorder _recorder;
    private readonly CancellationTokenSource _readCancellation = new();
    private readonly SemaphoreSlim _stopGate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly SemaphoreSlim _disposeGate = new(1, 1);
    private Task? _readTask;
    private bool _started;
    private bool _stopped;
    private bool _finalized;
    private bool _disconnectAttempted;
    private bool _disposed;
    private WaferCaptureArtifact? _artifact;

    public WaferDiscoveryCaptureController(IRawByteTransport transport, WaferCaptureRecorder recorder)
    {
        _transport = transport;
        _recorder = recorder;
    }

    public event EventHandler<WaferCaptureEvent>? EventRecorded
    {
        add => _recorder.EventRecorded += value;
        remove => _recorder.EventRecorded -= value;
    }

    public bool IsCapturing => _started && !_stopped;
    public long CaptureSizeBytes => _recorder.CaptureSizeBytes;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started) throw new InvalidOperationException("Capture has already started.");
        try
        {
            await _transport.ConnectAsync(cancellationToken);
            _started = true;
            await _recorder.RecordTransportStateAsync("SerialOpened", cancellationToken);
            _readTask = ReadLoopAsync(_readCancellation.Token);
        }
        catch (Exception exception)
        {
            await TryRecordErrorAsync(Classify(exception), SafeMessage(exception), CancellationToken.None);
            throw;
        }
    }

    public async Task SendAsync(ReadOnlyMemory<byte> logicalBytes, SerialWireFormatOptions options, string operation,
        CancellationToken cancellationToken = default)
    {
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            if (!IsCapturing) throw new InvalidOperationException("Start capture before transmitting.");
            var onWire = SerialWireFormatter.Encode(logicalBytes.Span, options);
            var started = Stopwatch.GetTimestamp();
            await _transport.WriteAsync(onWire, cancellationToken);
            await _recorder.RecordRawAsync(WaferCaptureDirection.Tx, onWire, operation, started,
                Stopwatch.GetTimestamp(), "Open", cancellationToken);
        }
        catch (Exception exception)
        {
            await TryRecordErrorAsync(Classify(exception), SafeMessage(exception), CancellationToken.None);
            throw;
        }
        finally { _writeGate.Release(); }
    }

    public Task AddMarkerAsync(string text, CancellationToken cancellationToken = default) => IsCapturing
        ? _recorder.AddMarkerAsync(text, cancellationToken)
        : throw new InvalidOperationException("Start capture before adding a marker.");

    public void AddProbe(WaferProbe probe) => _recorder.AddProbe(probe);

    public async Task<WaferCaptureArtifact> StopAsync(CancellationToken cancellationToken = default)
    {
        await _stopGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_started) throw new InvalidOperationException("Capture has not started.");
            if (_finalized) return _artifact!;
            cancellationToken.ThrowIfCancellationRequested();
            _stopped = true;
            _readCancellation.Cancel();
            if (_readTask is not null)
            {
                try { await _readTask; }
                catch (OperationCanceledException) { }
            }
            await _writeGate.WaitAsync(CancellationToken.None);
            try
            {
                if (!_disconnectAttempted)
                {
                    _disconnectAttempted = true;
                    try { await _transport.DisconnectAsync(CancellationToken.None); }
                    catch (Exception exception) { await TryRecordErrorAsync(Classify(exception), SafeMessage(exception), CancellationToken.None); }
                }
            }
            finally { _writeGate.Release(); }
            try { await _recorder.RecordTransportStateAsync("SerialClosed", CancellationToken.None); }
            catch (InvalidOperationException) when (_recorder.SizeLimitReached) { }
            _artifact = await _recorder.StopAsync(CancellationToken.None);
            _finalized = true;
            return _artifact;
        }
        finally { _stopGate.Release(); }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (!cancellationToken.IsCancellationRequested && !_stopped)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                var count = await _transport.ReadAsync(buffer, cancellationToken);
                await _recorder.RecordRawAsync(WaferCaptureDirection.Rx, buffer.AsMemory(0, count), "SerialReadChunk",
                    started, Stopwatch.GetTimestamp(), "Open", cancellationToken);
            }
            catch (CaptureSizeLimitReachedException exception)
            {
                await TryRecordErrorAsync("CaptureSizeLimit", exception.Message, CancellationToken.None);
                _stopped = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                await TryRecordErrorAsync(Classify(exception), SafeMessage(exception), CancellationToken.None);
                _stopped = true;
            }
        }
    }

    private async Task TryRecordErrorAsync(string kind, string message, CancellationToken cancellationToken)
    {
        try { await _recorder.RecordErrorAsync(kind, message, cancellationToken); }
        catch (Exception exception) when (exception is InvalidOperationException or CaptureSizeLimitReachedException or ObjectDisposedException) { }
    }

    private static string Classify(Exception exception) => exception switch
    {
        TimeoutException => "Timeout",
        UnauthorizedAccessException => "UnauthorizedAccess",
        InvalidDataException => "InvalidData",
        TransportException transport => transport.Error.ToString(),
        IOException => "IOException",
        OperationCanceledException => "UserCancelled",
        _ => exception.GetType().Name
    };

    private static string SafeMessage(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "Permission denied while accessing the serial adapter.",
        TransportException transport => transport.Message,
        IOException => "Serial I/O failed; check the adapter and cable.",
        _ => exception.Message
    };

    public async ValueTask DisposeAsync()
    {
        await _disposeGate.WaitAsync();
        try
        {
            if (_disposed) return;
            if (_started && !_finalized)
            {
                try { await StopAsync(); }
                catch (Exception) { }
            }
            _readCancellation.Dispose();
            await _transport.DisposeAsync();
            await _recorder.DisposeAsync();
            _disposed = true;
        }
        finally { _disposeGate.Release(); }
        GC.SuppressFinalize(this);
    }
}
