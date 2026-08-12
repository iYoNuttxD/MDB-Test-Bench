using System.IO.Ports;
using MdbTestBench.Transport.Abstractions;

namespace MdbTestBench.Transport.Configuration;

public enum TransportKind { Simulated, Serial, WaferMdbRs232 }

public sealed record AppSettings
{
    public TransportKind SelectedTransport { get; init; } = TransportKind.Simulated;
    public bool SimulatedMode { get; init; } = true;
    public string SerialPort { get; init; } = string.Empty;
    public int BaudRate { get; init; } = 115_200;
    public int DataBits { get; init; } = 8;
    public StopBits StopBits { get; init; } = StopBits.One;
    public Parity Parity { get; init; } = Parity.None;
    public PollingMode PollingMode { get; init; } = PollingMode.AdapterManaged;
    public int TimeoutMilliseconds { get; init; } = 2_000;
}
