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
}
