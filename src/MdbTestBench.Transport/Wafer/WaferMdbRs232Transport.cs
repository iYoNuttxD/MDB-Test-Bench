using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;
using MdbTestBench.Core.Protocol.Frames;
using MdbTestBench.Transport.Abstractions;

namespace MdbTestBench.Transport.Wafer;

public sealed class WaferMdbRs232Transport : IMdbTransport
{
    public const int MaxReceiveBufferSize = 65_536;
    private readonly IRawByteTransport _serialTransport;
    private readonly IWaferProtocolCodec _codec;
    private readonly IMdbCashlessDecoder _mdbDecoder;
    private readonly TimeSpan _timeout;
    private readonly int _receiveBufferSize;
    private readonly SemaphoreSlim _exchangeGate = new(1, 1);
    private bool _disposed;

    public WaferMdbRs232Transport(
        IRawByteTransport serialTransport,
        IWaferProtocolCodec codec,
        PollingMode pollingMode,
        TimeSpan timeout,
        int receiveBufferSize = 4_096,
        IMdbCashlessDecoder? mdbDecoder = null)
    {
        ArgumentNullException.ThrowIfNull(serialTransport);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(receiveBufferSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(receiveBufferSize, MaxReceiveBufferSize);
        _serialTransport = serialTransport;
        _codec = codec;
        _mdbDecoder = mdbDecoder ?? new MdbCashlessDecoder();
        _timeout = timeout;
        _receiveBufferSize = receiveBufferSize;
        Capabilities = new TransportCapabilities
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
    }

    public bool IsConnected => _serialTransport.IsConnected;

    public TransportCapabilities Capabilities { get; }

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        _serialTransport.ConnectAsync(cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
        _serialTransport.DisconnectAsync(cancellationToken);

    public async Task<MdbFrame> ExchangeAsync(
        MdbFrame request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsConnected) throw new InvalidOperationException("The Wafer transport is not connected.");
        if (request.RawBytes.IsEmpty)
            throw new InvalidOperationException("Wafer exchange requires a standard MDB block produced by IMdbCashlessEncoder.");
        await _exchangeGate.WaitAsync(cancellationToken);
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_timeout);
            var token = timeoutSource.Token;
            await _serialTransport.WriteAsync(_codec.EncodeMdbBlock(request.RawBytes), token);
            var buffer = new byte[_receiveBufferSize];
            var count = await _serialTransport.ReadAsync(buffer, token);
            var mdbBytes = _codec.DecodeMdbBlock(buffer.AsMemory(0, count));
            var decoded = _mdbDecoder.DecodeResponse(mdbBytes.Span);
            return new MdbFrame(
                DateTimeOffset.UtcNow,
                MdbDirection.Rx,
                request.Destination,
                request.Source,
                request.Command,
                request.Subcommand,
                decoded.ResponseType,
                mdbBytes,
                decoded is MdbMalformedCashlessResponse malformed ? malformed.Error : decoded.GetType().Name,
                CashlessDevice: request.CashlessDevice);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Wafer exchange exceeded {_timeout}.");
        }
        finally
        {
            _exchangeGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _exchangeGate.WaitAsync();
        try
        {
            if (_disposed) return;
            await _serialTransport.DisposeAsync();
            _disposed = true;
        }
        finally
        {
            _exchangeGate.Release();
        }
        GC.SuppressFinalize(this);
    }
}
