using System.IO.Ports;
using MdbTestBench.Transport.Abstractions;
using MdbTestBench.Transport.Configuration;

namespace MdbTestBench.Transport.Serial;

public sealed class SerialTransport(SerialTransportSettings settings) : IRawByteTransport
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SerialPort? _port;
    private bool _disposed;

    public bool IsConnected => _port?.IsOpen == true;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        settings.Validate();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected) return;
            cancellationToken.ThrowIfCancellationRequested();
            var port = new SerialPort(settings.PortName, settings.BaudRate, settings.Parity,
                settings.DataBits, settings.StopBits)
            {
                ReadTimeout = (int)settings.OperationTimeout.TotalMilliseconds,
                WriteTimeout = (int)settings.OperationTimeout.TotalMilliseconds,
                ReadBufferSize = settings.ReadBufferSize
            };
            port.Open();
            _port = port;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_port is null) return;
            if (_port.IsOpen) _port.Close();
            _port.Dispose();
            _port = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        var port = GetOpenPort();
        await port.BaseStream.WriteAsync(data, cancellationToken);
        await port.BaseStream.FlushAsync(cancellationToken);
    }

    public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var port = GetOpenPort();
        return await port.BaseStream.ReadAsync(buffer, cancellationToken);
    }

    private SerialPort GetOpenPort() =>
        IsConnected ? _port! : throw new InvalidOperationException("The serial transport is not connected.");

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await DisconnectAsync();
        _gate.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
