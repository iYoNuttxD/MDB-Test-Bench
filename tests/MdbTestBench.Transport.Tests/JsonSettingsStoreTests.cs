using MdbTestBench.Transport.Abstractions;
using MdbTestBench.Transport.Configuration;

namespace MdbTestBench.Transport.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task Settings_RoundTripWithStringEnums()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mdb-settings-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonSettingsStore();
            var expected = new AppSettings
            {
                SelectedTransport = TransportKind.WaferMdbRs232,
                SerialPort = "test-port",
                PollingMode = PollingMode.AdapterManaged
            };

            await store.SaveAsync(path, expected);
            var actual = await store.LoadAsync(path);

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task InvalidJsonFallsBackToSafeDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mdb-settings-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, "{ invalid json");

            var settings = await new JsonSettingsStore().LoadAsync(path);

            Assert.Equal(TransportKind.Simulated, settings.SelectedTransport);
            Assert.Equal(9_600, settings.BaudRate);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task OversizedSettingsFallBackWithoutAllocatingJsonGraph()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mdb-settings-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllBytesAsync(path, new byte[JsonSettingsStore.MaxSettingsFileBytes + 1]);

            var settings = await new JsonSettingsStore().LoadAsync(path);

            Assert.Equal(new AppSettings(), settings);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task SyntacticallyValidButUnsafeValuesAreNormalized()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mdb-settings-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, """
                {
                  "serialPort": null,
                  "baudRate": -1,
                  "dataBits": 99,
                  "timeoutMilliseconds": 2147483647,
                  "windowWidth": -100,
                  "windowHeight": 999999
                }
                """);

            var settings = await new JsonSettingsStore().LoadAsync(path);

            Assert.Equal(string.Empty, settings.SerialPort);
            Assert.Equal(9_600, settings.BaudRate);
            Assert.Equal(8, settings.DataBits);
            Assert.Equal(2_000, settings.TimeoutMilliseconds);
            Assert.Equal(100, settings.CaptureMaximumMegabytes);
            Assert.Equal(1_280, settings.WindowWidth);
            Assert.Equal(800, settings.WindowHeight);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
