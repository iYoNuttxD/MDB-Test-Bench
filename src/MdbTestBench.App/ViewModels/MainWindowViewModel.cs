using System.Collections.ObjectModel;
using System.Windows.Input;
using MdbTestBench.Transport.Configuration;

namespace MdbTestBench.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private NavigationItemViewModel _selectedPage;

    public MainWindowViewModel(AppSettings settings)
    {
        Settings = settings;
        Pages = new ObservableCollection<NavigationItemViewModel>
        {
            new("Dashboard", "Visão geral do banco de testes e do estado da conexão."),
            new("Manual", "Envio manual de comandos MDB lógicos."),
            new("Automatic", "Execução de cenários JSON reproduzíveis."),
            new("Profiles", "Perfis de Feature Level e capabilities híbridas."),
            new("Logs", "Eventos TX/RX estruturados com alta resolução temporal."),
            new("Settings", "Transporte, serial, polling ownership e timeouts.")
        };
        _selectedPage = Pages[0];
        NavigateCommand = new RelayCommand(page =>
        {
            if (page is NavigationItemViewModel item) SelectedPage = item;
        });
    }

    public AppSettings Settings { get; }
    public ObservableCollection<NavigationItemViewModel> Pages { get; }
    public ICommand NavigateCommand { get; }

    public NavigationItemViewModel SelectedPage
    {
        get => _selectedPage;
        set => SetProperty(ref _selectedPage, value);
    }

    public string ConnectionStatus => "Disconnected — no port is opened at startup";
}
