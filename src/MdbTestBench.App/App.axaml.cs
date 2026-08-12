using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MdbTestBench.App.ViewModels;
using MdbTestBench.App.Views;
using MdbTestBench.Transport.Configuration;

namespace MdbTestBench.App;

public sealed partial class App : Application
{
    private readonly AppSettings _settings;

    public App() : this(new AppSettings()) { }

    public App(AppSettings settings) => _settings = settings;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(_settings)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
