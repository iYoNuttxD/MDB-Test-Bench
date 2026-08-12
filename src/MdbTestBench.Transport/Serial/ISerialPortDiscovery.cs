using System.IO.Ports;

namespace MdbTestBench.Transport.Serial;

public interface ISerialPortDiscovery
{
    IReadOnlyList<string> GetAvailablePorts();
}

public sealed class SerialPortDiscovery(Func<string[]>? portProvider = null) : ISerialPortDiscovery
{
    private readonly Func<string[]> _portProvider = portProvider ?? SerialPort.GetPortNames;

    public IReadOnlyList<string> GetAvailablePorts() => _portProvider()
        .Where(port => !string.IsNullOrWhiteSpace(port))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(port => port, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
