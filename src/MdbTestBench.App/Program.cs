using Avalonia;
using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;
using MdbTestBench.Core.Protocol.Frames;
using MdbTestBench.Transport.Configuration;
using MdbTestBench.App.Services;
using MdbTestBench.Transport.Simulation;

namespace MdbTestBench.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
            return RunSmokeTestAsync().GetAwaiter().GetResult();

        var paths = new AppPaths();
        paths.EnsureDirectories();
        var settings = new JsonSettingsStore().LoadAsync(paths.Settings).GetAwaiter().GetResult();
        BuildAvaloniaApp(settings, paths).StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp(AppSettings settings, AppPaths paths) =>
        AppBuilder.Configure(() => new App(settings, paths))
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
}
