using MdbTestBench.Core.Protocol.Frames;

namespace MdbTestBench.Transport.Abstractions;

public interface IMdbTransport : IAsyncDisposable
{
    bool IsConnected { get; }
    TransportCapabilities Capabilities { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<MdbFrame> ExchangeAsync(MdbFrame request, CancellationToken cancellationToken = default);
}
