using Avalonia;
using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;
using MdbTestBench.Core.Protocol.Frames;
using MdbTestBench.Transport.Configuration;
using MdbTestBench.App.Services;
using MdbTestBench.Transport.Simulation;
using MdbTestBench.Transport.Capture;
using MdbTestBench.Transport.Serial;
using MdbTestBench.Transport.Abstractions;
using System.Diagnostics;
using System.Globalization;
using MdbTestBench.App.Localization;

namespace MdbTestBench.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
            return RunSmokeTestAsync().GetAwaiter().GetResult();
        if (args.Contains("--discovery-smoke-test", StringComparer.OrdinalIgnoreCase))
            return RunDiscoverySmokeTestAsync(GetOption(args, "--capture-output=")).GetAwaiter().GetResult();

        var paths = new AppPaths();
        paths.EnsureDirectories();
        var settings = new JsonSettingsStore().LoadAsync(paths.Settings).GetAwaiter().GetResult();
        var localization = new LocalizationService();
        localization.SetCulture(localization.ResolveCulture(settings.Language, CultureInfo.CurrentUICulture).Name);
        BuildAvaloniaApp(settings, paths, localization).StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp(AppSettings settings, AppPaths paths, ILocalizationService localization) =>
        AppBuilder.Configure(() => new App(settings, paths, localization))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static async Task<int> RunSmokeTestAsync()
    {
        await using var simulator = new SimulatedCashlessTransport(new SimulatedCashlessOptions
        {
            ResponseDelay = TimeSpan.Zero
        });
        await simulator.ConnectAsync();
        var encoder = new MdbCashlessEncoder();
        var destination = new MdbAddress(0x10, MdbDeviceType.CashlessDevice1);
        var reset = await simulator.ExchangeAsync(MdbFrame.CommandFrame(
            MdbAddress.Vmc,
            destination,
            MdbCommandType.Reset,
            payload: encoder.Encode(new MdbResetCommand())));
        var poll = await simulator.ExchangeAsync(MdbFrame.CommandFrame(
            MdbAddress.Vmc,
            destination,
            MdbCommandType.Poll,
            payload: encoder.Encode(new MdbPollCommand())));
        return reset.Response == MdbResponseType.Ack && poll.Response == MdbResponseType.JustReset ? 0 : 1;
    }

    private static string? GetOption(IEnumerable<string> args, string prefix) =>
        args.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];

    private static async Task<int> RunDiscoverySmokeTestAsync(string? outputPath)
    {
        var directory = Path.Combine(Path.GetTempPath(), "mdb-discovery-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var header = new WaferCaptureDocument
            {
                CaptureId = Guid.NewGuid().ToString("N"), Application = new("MDB Test Bench", "0.1.1"),
                Adapter = new() { Model = "MDB-RS232 PC Adapter", PrintedRevision = "2022061K5" },
                Host = new("Smoke test", "N/A", "N/A", Environment.Version.ToString()),
                Serial = new() { BaudRate = 9600, DataBits = 8, PollingMode = PollingMode.AdapterManaged },
                Capture = new() { CreatedAtUtc = now, StartedAtUtc = now, MonotonicFrequency = Stopwatch.Frequency }
            };
            var recorder = await WaferCaptureRecorder.StartAsync(header, directory);
            await using var controller = new WaferDiscoveryCaptureController(new DiscoverySimulatorTransport(), recorder);
            await controller.StartAsync();
            await controller.AddMarkerAsync("Discovery smoke marker");
            await controller.SendAsync(new byte[] { 0x10 }, new SerialWireFormatOptions(), "SmokeRawTx");
            await Task.Delay(50);
            var artifact = await controller.StopAsync();
            var path = string.IsNullOrWhiteSpace(outputPath)
                ? Path.Combine(directory, "smoke.mdbcap.json")
                : Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException("Capture output path must include a directory."));
            var serializer = new WaferCaptureSerializer();
            await serializer.ExportAsync(artifact, path);
            var imported = await serializer.LoadAsync(path);
            return imported.Events.Any(item => item.Direction == WaferCaptureDirection.Tx && item.GetRawBytes().SequenceEqual(new byte[] { 0x10 })) &&
                   imported.Events.Any(item => item.Direction == WaferCaptureDirection.Rx) &&
                   imported.Events.Any(item => item.Type == WaferCaptureEventType.Marker) ? 0 : 1;
        }
        finally { Directory.Delete(directory, true); }
    }
}
