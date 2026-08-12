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
            try
            {
                port.Open();
                _port = port;
            }
            catch (UnauthorizedAccessException exception)
            {
                port.Dispose();
                throw new TransportException(TransportError.PermissionDenied,
                    $"Permission was denied for serial port '{settings.PortName}'.", exception);
            }
            catch (IOException exception)
            {
                port.Dispose();
                var error = File.Exists(settings.PortName) ? TransportError.PortBusy : TransportError.PortNotFound;
                throw new TransportException(error,
                    $"Serial port '{settings.PortName}' is unavailable or already in use.", exception);
            }
            catch (ArgumentException exception)
            {
                port.Dispose();
                throw new TransportException(TransportError.PortNotFound,
                    $"Serial port '{settings.PortName}' does not exist.", exception);
            }
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
        try
        {
            await port.BaseStream.WriteAsync(data, cancellationToken);
            await port.BaseStream.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            throw new TransportException(TransportError.WriteFailure,
                "The serial write failed. Check the adapter connection.", exception);
        }
    }

    public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var port = GetOpenPort();
        try
        {
            var count = await port.BaseStream.ReadAsync(buffer, cancellationToken);
            if (count == 0) throw new TransportException(TransportError.Disconnected,
                "The serial device disconnected while reading.");
            return count;
        }
        catch (OperationCanceledException) { throw; }
        catch (TransportException) { throw; }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            throw new TransportException(TransportError.ReadFailure,
                "The serial read failed. Check the adapter connection.", exception);
        }
    }

    private SerialPort GetOpenPort() =>
        IsConnected ? _port! : throw new TransportException(TransportError.Disconnected,
            "The serial transport is not connected.");

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await DisconnectAsync();
        _gate.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
