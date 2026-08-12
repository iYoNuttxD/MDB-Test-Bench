using MdbTestBench.Core.Protocol.Frames;
using MdbTestBench.Transport.Abstractions;

namespace MdbTestBench.Transport.Wafer;

public sealed class WaferMdbRs232Transport(
    IRawByteTransport serialTransport,
    IWaferProtocolCodec codec,
    PollingMode pollingMode,
    TimeSpan timeout,
    int receiveBufferSize = 4096) : IMdbTransport
{
    private readonly SemaphoreSlim _exchangeGate = new(1, 1);

    public bool IsConnected => serialTransport.IsConnected;

    public TransportCapabilities Capabilities { get; } = new()
    {
        Name = "Wafer MDB-RS232 (codec required)",
        RequiresPhysicalHardware = true,
        PollingMode = pollingMode,
        SupportedPollingModes = new HashSet<PollingMode>
        {
            PollingMode.AdapterManaged,
            PollingMode.HostManaged
        }
    };

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        serialTransport.ConnectAsync(cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
        serialTransport.DisconnectAsync(cancellationToken);

    public async Task<MdbFrame> ExchangeAsync(
        MdbFrame request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected) throw new InvalidOperationException("The Wafer transport is not connected.");

        await _exchangeGate.WaitAsync(cancellationToken);
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            var token = timeoutSource.Token;
            await serialTransport.WriteAsync(codec.Encode(request), token);
            var buffer = new byte[receiveBufferSize];
            var count = await serialTransport.ReadAsync(buffer, token);
            return codec.Decode(buffer.AsMemory(0, count), request);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Wafer exchange exceeded {timeout}.");
        }
        finally
        {
            _exchangeGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _exchangeGate.Dispose();
        await serialTransport.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
