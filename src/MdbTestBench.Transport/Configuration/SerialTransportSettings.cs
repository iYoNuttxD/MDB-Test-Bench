using System.IO.Ports;
using MdbTestBench.Transport.Abstractions;

namespace MdbTestBench.Transport.Configuration;

public sealed record SerialTransportSettings
{
    public const int MaxReadBufferSize = 65_536;

    public string PortName { get; init; } = string.Empty;
    public int BaudRate { get; init; } = 9_600;
    public int DataBits { get; init; } = 8;
    public StopBits StopBits { get; init; } = StopBits.One;
    public Parity Parity { get; init; } = Parity.None;
    public PollingMode PollingMode { get; init; } = PollingMode.AdapterManaged;
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public int ReadBufferSize { get; init; } = 4096;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PortName))
            throw new InvalidOperationException("A serial port must be selected before connecting.");
        if (BaudRate <= 0) throw new ArgumentOutOfRangeException(nameof(BaudRate));
        if (DataBits is < 5 or > 8) throw new ArgumentOutOfRangeException(nameof(DataBits));
        if (OperationTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(OperationTimeout));
        if (ReadBufferSize is <= 0 or > MaxReadBufferSize)
            throw new ArgumentOutOfRangeException(nameof(ReadBufferSize),
                $"Read buffer size must be between 1 and {MaxReadBufferSize} bytes.");
        if (!Enum.IsDefined(Parity)) throw new ArgumentOutOfRangeException(nameof(Parity));
        if (!Enum.IsDefined(StopBits) || StopBits == StopBits.None)
            throw new ArgumentOutOfRangeException(nameof(StopBits));
    }
}
