using MdbTestBench.Transport.Abstractions;

namespace MdbTestBench.Transport.Serial;

public sealed class SerialDiagnosticTransport(
    IRawByteTransport serial,
    SerialWireFormatOptions wireOptions,
    TimeSpan timeout,
    int receiveBufferSize = 4_096) : IRawCommandTransport, IAsyncDisposable
{
    public async Task<RawExchangeResult> ExchangeRawAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var bytesOnWire = SerialWireFormatter.Encode(payload.Span, wireOptions);
        try
        {
            await serial.WriteAsync(bytesOnWire, deadline.Token);
            var response = new byte[receiveBufferSize];
            var count = await serial.ReadAsync(response, deadline.Token);
            return new RawExchangeResult(bytesOnWire, response.AsMemory(0, count),
                $"Adapter debug exchange using {wireOptions.Format}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TransportException(TransportError.Timeout,
                $"No serial response was received within {timeout.TotalMilliseconds:0} ms.");
        }
    }

    public ValueTask DisposeAsync() => serial.DisposeAsync();
}
