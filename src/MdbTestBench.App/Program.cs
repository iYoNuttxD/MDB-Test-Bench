using Avalonia;
using MdbTestBench.Transport.Configuration;
using MdbTestBench.App.Services;

namespace MdbTestBench.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var paths = new AppPaths();
        paths.EnsureDirectories();
        AppSettings settings;
        try { settings = new JsonSettingsStore().LoadAsync(paths.Settings).GetAwaiter().GetResult(); }
        catch { settings = new AppSettings(); }
        BuildAvaloniaApp(settings, paths).StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp(AppSettings settings, AppPaths paths) =>
        AppBuilder.Configure(() => new App(settings, paths))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
