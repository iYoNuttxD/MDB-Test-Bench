using MdbTestBench.Core.Logging;
using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Frames;
using MdbTestBench.Transport.Abstractions;
using MdbTestBench.Transport.Configuration;
using MdbTestBench.Transport.Serial;
using MdbTestBench.Transport.Simulation;

namespace MdbTestBench.App.Services;

public sealed class WorkbenchSession(InMemoryMdbLogSink logs) : IAsyncDisposable
{
    private SimulatedCashlessTransport? _logicalTransport;
    private IRawCommandTransport? _rawTransport;
    private SerialTransport? _serial;

    public bool IsConnected => _logicalTransport?.IsConnected == true || _serial?.IsConnected == true;
    public bool IsSimulation => _logicalTransport is SimulatedCashlessTransport;
    public string State => _logicalTransport is SimulatedCashlessTransport simulator
        ? simulator.State.ToString()
        : IsConnected ? "Connected / codec unverified" : "Disconnected";

    public bool CanSend(MdbFrame frame) =>
        _logicalTransport is SimulatedCashlessTransport simulator && simulator.CanExchange(frame);

    public async Task ConnectAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync(cancellationToken);
        if (settings.SelectedTransport == TransportKind.Simulated)
        {
            var simulator = new SimulatedCashlessTransport(new SimulatedCashlessOptions
            {
                Behavior = settings.SimulatorBehavior,
                OperationTimeout = TimeSpan.FromMilliseconds(settings.TimeoutMilliseconds)
            }, settings.PollingMode);
            await simulator.ConnectAsync(cancellationToken);
            _logicalTransport = simulator;
            _rawTransport = simulator;
            await WriteStatusAsync("Simulator connected", MdbLogSeverity.Information, cancellationToken);
            return;
        }

        var serialSettings = new SerialTransportSettings
        {
            PortName = settings.SerialPort,
            BaudRate = settings.BaudRate,
            DataBits = settings.DataBits,
            Parity = settings.Parity,
            StopBits = settings.StopBits,
            PollingMode = settings.PollingMode,
            OperationTimeout = TimeSpan.FromMilliseconds(settings.TimeoutMilliseconds)
        };
        var serial = new SerialTransport(serialSettings);
        await serial.ConnectAsync(cancellationToken);
        _serial = serial;
        _rawTransport = new SerialDiagnosticTransport(serial, new SerialWireFormatOptions
        {
            Format = settings.WireFormat,
            Terminator = settings.AsciiHexTerminator
        }, serialSettings.OperationTimeout);
        await WriteStatusAsync($"Serial adapter connected on {settings.SerialPort}; logical Wafer codec unavailable",
            MdbLogSeverity.Warning, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_logicalTransport is not null)
        {
            await _logicalTransport.DisconnectAsync(cancellationToken);
            await _logicalTransport.DisposeAsync();
        }
        else if (_serial is not null)
        {
            await _serial.DisconnectAsync(cancellationToken);
            await _serial.DisposeAsync();
        }
        _logicalTransport = null;
        _rawTransport = null;
        _serial = null;
    }

    public async Task<MdbFrame> ExchangeAsync(MdbFrame request, CancellationToken cancellationToken = default)
    {
        if (_logicalTransport is null)
            throw new InvalidOperationException("Structured MDB commands require the Simulator until a validated Wafer codec is available.");
        await LogFrameAsync(request, MdbLogSeverity.Information, cancellationToken);
        try
        {
            var response = await _logicalTransport.ExchangeAsync(request, cancellationToken);
            await LogFrameAsync(response, response.Response == MdbResponseType.Unknown
                ? MdbLogSeverity.Warning : MdbLogSeverity.Information, cancellationToken);
            return response;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await WriteStatusAsync(exception.Message, MdbLogSeverity.Error, cancellationToken);
            throw;
        }
    }

    public async Task<RawExchangeResult> ExchangeRawAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        if (_rawTransport is null) throw new InvalidOperationException("Connect a transport before sending raw data.");
        await logs.WriteAsync(new MdbLogEntry(DateTimeOffset.UtcNow, MdbDirection.Tx, "VMC", "Adapter",
            "RAW", "Advanced / Adapter Debug", payload, MdbLogSeverity.Warning), cancellationToken);
        try
        {
            var result = await _rawTransport.ExchangeRawAsync(payload, cancellationToken);
            await logs.WriteAsync(new MdbLogEntry(DateTimeOffset.UtcNow, MdbDirection.Rx, "Adapter", "VMC",
                "RAW", result.Description, result.ResponseBytes, MdbLogSeverity.Warning), cancellationToken);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await WriteStatusAsync(exception.Message, MdbLogSeverity.Error, cancellationToken);
            throw;
        }
    }

    private ValueTask LogFrameAsync(MdbFrame frame, MdbLogSeverity severity, CancellationToken token) =>
        logs.WriteAsync(new MdbLogEntry(frame.Timestamp, frame.Direction, frame.Source.ToString(),
            frame.Destination.ToString(), frame.Subcommand == MdbSubcommandType.None
                ? frame.Command.ToString() : $"{frame.Command} {frame.Subcommand}",
            frame.InterpretedPayload ?? frame.Response?.ToString() ?? "Logical MDB command",
            frame.RawPayload, severity), token);

    private ValueTask WriteStatusAsync(string description, MdbLogSeverity severity, CancellationToken token) =>
        logs.WriteAsync(new MdbLogEntry(DateTimeOffset.UtcNow, MdbDirection.Rx, "Transport", "Application",
            "STATUS", description, ReadOnlyMemory<byte>.Empty, severity), token);

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        GC.SuppressFinalize(this);
    }
}
