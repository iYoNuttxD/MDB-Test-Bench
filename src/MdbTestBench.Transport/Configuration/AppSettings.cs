using System.IO.Ports;
using MdbTestBench.Transport.Abstractions;
using MdbTestBench.Transport.Serial;
using MdbTestBench.Transport.Simulation;

namespace MdbTestBench.Transport.Configuration;

public enum TransportKind { Simulated, WaferMdbRs232 }

public sealed record AppSettings
{
    public TransportKind SelectedTransport { get; init; } = TransportKind.Simulated;
    public SimulatorBehavior SimulatorBehavior { get; init; } = SimulatorBehavior.Normal;
    public string SerialPort { get; init; } = string.Empty;
    public int BaudRate { get; init; } = 9_600;
    public int DataBits { get; init; } = 8;
    public StopBits StopBits { get; init; } = StopBits.One;
    public Parity Parity { get; init; } = Parity.None;
    public PollingMode PollingMode { get; init; } = PollingMode.AdapterManaged;
    public int TimeoutMilliseconds { get; init; } = 2_000;
    public SerialWireFormat WireFormat { get; init; } = SerialWireFormat.BinaryBytes;
    public AsciiHexTerminator AsciiHexTerminator { get; init; } = AsciiHexTerminator.None;
    public string LastProfileId { get; init; } = "standard-level1";
    public double WindowWidth { get; init; } = 1_280;
    public double WindowHeight { get; init; } = 800;
}
