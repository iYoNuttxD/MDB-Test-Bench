using Avalonia;
using MdbTestBench.Transport.Configuration;

namespace MdbTestBench.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var settings = new JsonSettingsStore().LoadAsync(settingsPath).GetAwaiter().GetResult();
        BuildAvaloniaApp(settings).StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp(AppSettings settings) =>
        AppBuilder.Configure(() => new App(settings))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
