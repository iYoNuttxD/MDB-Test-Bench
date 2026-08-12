namespace MdbTestBench.Transport.Abstractions;

public sealed record RawExchangeResult(ReadOnlyMemory<byte> RequestBytes, ReadOnlyMemory<byte> ResponseBytes, string Description);

public interface IRawCommandTransport
{
    Task<RawExchangeResult> ExchangeRawAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);
}
