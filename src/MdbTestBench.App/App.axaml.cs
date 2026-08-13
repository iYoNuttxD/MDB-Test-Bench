using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MdbTestBench.App.ViewModels;
using MdbTestBench.App.Views;
using MdbTestBench.Transport.Configuration;
using MdbTestBench.App.Services;
using MdbTestBench.App.Localization;

namespace MdbTestBench.App;

public sealed partial class App : Application
{
    private readonly AppSettings _settings;
    private readonly AppPaths _paths;
    private readonly ILocalizationService _localization;

    public App() : this(new AppSettings(), new AppPaths(), new LocalizationService()) { }

    public App(AppSettings settings, AppPaths paths, ILocalizationService localization)
    {
        _settings = settings;
        _paths = paths;
        _localization = localization;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        if (_localization is LocalizationService service) service.ApplyApplicationResources();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(_settings, _paths, localization: _localization)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
