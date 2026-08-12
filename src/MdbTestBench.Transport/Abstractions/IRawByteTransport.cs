namespace MdbTestBench.Transport.Abstractions;

public interface IRawByteTransport : IAsyncDisposable
{
    bool IsConnected { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}
