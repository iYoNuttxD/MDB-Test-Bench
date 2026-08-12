using MdbTestBench.Transport.Configuration;

namespace MdbTestBench.Transport.Tests;

public sealed class SerialConfigurationTests
{
    [Fact]
    public void SafeDefaultsAreValidWhenPortIsSelected()
    {
        var settings = new SerialTransportSettings { PortName = "test-port" };

        settings.Validate();

        Assert.Equal(9_600, settings.BaudRate);
        Assert.Equal(8, settings.DataBits);
        Assert.Equal(4_096, settings.ReadBufferSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65537)]
    public void InvalidReadBufferIsRejected(int size)
    {
        var settings = new SerialTransportSettings { PortName = "test-port", ReadBufferSize = size };

        Assert.Throws<ArgumentOutOfRangeException>(settings.Validate);
    }
}
