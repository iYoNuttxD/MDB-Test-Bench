using System.Threading.Channels;
using MdbTestBench.Transport.Abstractions;

namespace MdbTestBench.Transport.Simulation;

public sealed class DiscoverySimulatorTransport : IRawByteTransport
{
    private readonly Channel<byte[]> _chunks = Channel.CreateUnbounded<byte[]>();
    private bool _disposed;
    public bool IsConnected { get; private set; }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        IsConnected = true;
        _chunks.Writer.TryWrite([0x00]);
        _chunks.Writer.TryWrite([0x03]);
        _chunks.Writer.TryWrite([0xFF, 0xFE]);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsConnected = false;
        return Task.CompletedTask;
    }

    public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        cancellationToken.ThrowIfCancellationRequested();
        _chunks.Writer.TryWrite([0x00]);
        return Task.CompletedTask;
    }

    public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        var chunk = await _chunks.Reader.ReadAsync(cancellationToken);
        if (chunk.Length > buffer.Length) throw new InvalidOperationException("Read buffer is too small for the simulated chunk.");
        chunk.CopyTo(buffer);
        return chunk.Length;
    }

    private void EnsureConnected()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsConnected) throw new TransportException(TransportError.Disconnected, "Discovery simulator is not connected.");
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        IsConnected = false;
        _disposed = true;
        _chunks.Writer.TryComplete();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
