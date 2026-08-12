using MdbTestBench.Transport.Abstractions;
using MdbTestBench.Transport.Configuration;

namespace MdbTestBench.Transport.Serial;

public sealed class SerialDiagnosticTransport : IRawCommandTransport, IAsyncDisposable
{
    public const int MaxDiagnosticPayloadBytes = 4_096;
    private readonly IRawByteTransport _serial;
    private readonly SerialWireFormatOptions _wireOptions;
    private readonly TimeSpan _timeout;
    private readonly int _receiveBufferSize;

    public SerialDiagnosticTransport(
        IRawByteTransport serial,
        SerialWireFormatOptions wireOptions,
        TimeSpan timeout,
        int receiveBufferSize = 4_096)
    {
        ArgumentNullException.ThrowIfNull(serial);
        ArgumentNullException.ThrowIfNull(wireOptions);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(receiveBufferSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(receiveBufferSize, SerialTransportSettings.MaxReadBufferSize);
        _serial = serial;
        _wireOptions = wireOptions;
        _timeout = timeout;
        _receiveBufferSize = receiveBufferSize;
    }

    public async Task<RawExchangeResult> ExchangeRawAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        if (payload.Length > MaxDiagnosticPayloadBytes)
            throw new TransportException(TransportError.InvalidData,
                $"Raw payload exceeds the {MaxDiagnosticPayloadBytes}-byte transport limit.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_timeout);
        var bytesOnWire = SerialWireFormatter.Encode(payload.Span, _wireOptions);
        try
        {
            await _serial.WriteAsync(bytesOnWire, deadline.Token);
            var response = new byte[_receiveBufferSize];
            var count = await _serial.ReadAsync(response, deadline.Token);
            return new RawExchangeResult(bytesOnWire, response.AsMemory(0, count),
                $"Adapter debug exchange using {_wireOptions.Format}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TransportException(TransportError.Timeout,
                $"No serial response was received within {_timeout.TotalMilliseconds:0} ms.");
        }
    }

    public ValueTask DisposeAsync() => _serial.DisposeAsync();
}
