using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MdbTestBench.App.ViewModels;
using MdbTestBench.App.Views;
using MdbTestBench.Transport.Configuration;
using MdbTestBench.App.Services;

namespace MdbTestBench.App;

public sealed partial class App : Application
{
    private readonly AppSettings _settings;
    private readonly AppPaths _paths;

    public App() : this(new AppSettings(), new AppPaths()) { }

    public App(AppSettings settings, AppPaths paths)
    {
        _settings = settings;
        _paths = paths;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(_settings, _paths)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
