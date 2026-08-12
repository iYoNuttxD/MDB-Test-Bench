using MdbTestBench.Transport.Serial;

namespace MdbTestBench.Transport.Tests;

public sealed class SerialPortDiscoveryTests
{
    private static readonly string[] DiscoveredPorts = ["/dev/ttyUSB0", "COM3", "/dev/ttyUSB0", ""];
    private static readonly string[] ExpectedPorts = ["/dev/ttyUSB0", "COM3"];

    [Fact]
    public void DiscoveryDoesNotHardcodeOrDuplicatePortNames()
    {
        var discovery = new SerialPortDiscovery(() => DiscoveredPorts);
        Assert.Equal(ExpectedPorts, discovery.GetAvailablePorts());
    }
}
