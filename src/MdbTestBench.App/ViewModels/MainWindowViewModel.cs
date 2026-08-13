using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows.Input;
using MdbTestBench.App.Services;
using MdbTestBench.Core.Logging;
using MdbTestBench.Core.Profiles;
using MdbTestBench.Core.Protocol;
using MdbTestBench.Transport.Abstractions;
using MdbTestBench.Transport.Configuration;
using MdbTestBench.Transport.Serial;
using MdbTestBench.Transport.Simulation;

namespace MdbTestBench.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly AppPaths _paths;
    private readonly JsonSettingsStore _settingsStore = new();
    private readonly ISerialPortDiscovery _portDiscovery;
    private readonly InMemoryMdbLogSink _logs = new();
    private readonly WorkbenchSession _session;
    private NavigationItemViewModel _selectedPage;
    private TransportKind _selectedTransport;
    private SimulatorBehavior _selectedSimulatorBehavior;
    private string? _selectedPort;
    private int _selectedBaudRate;
    private int _selectedDataBits;
    private Parity _selectedParity;
    private StopBits _selectedStopBits;
    private Handshake _selectedHandshake;
    private PollingMode _selectedPollingMode;
    private SerialWireFormat _selectedWireFormat;
    private AsciiHexTerminator _selectedTerminator;
    private int _timeoutMilliseconds;
    private int _captureMaximumMegabytes;
    private bool _isConnected;
    private string _connectionStatus = "Disconnected";
    private string _lastError = "None";
    private double _windowWidth;
    private double _windowHeight;

    public MainWindowViewModel(AppSettings settings, AppPaths? paths = null, ISerialPortDiscovery? portDiscovery = null)
    {
        _paths = paths ?? new AppPaths();
        _paths.EnsureDirectories();
        _portDiscovery = portDiscovery ?? new SerialPortDiscovery();
        _session = new WorkbenchSession(_logs);

        _selectedTransport = settings.SelectedTransport;
        _selectedSimulatorBehavior = settings.SimulatorBehavior;
        _selectedBaudRate = settings.BaudRate;
        _selectedDataBits = settings.DataBits;
        _selectedParity = settings.Parity;
        _selectedStopBits = settings.StopBits;
        _selectedHandshake = settings.Handshake;
        _selectedPollingMode = settings.PollingMode;
        _selectedWireFormat = settings.WireFormat;
        _selectedTerminator = settings.AsciiHexTerminator;
        _timeoutMilliseconds = settings.TimeoutMilliseconds;
        _captureMaximumMegabytes = settings.CaptureMaximumMegabytes;
        _windowWidth = settings.WindowWidth;
        _windowHeight = settings.WindowHeight;

        Pages = new ObservableCollection<NavigationItemViewModel>
        {
            new("Dashboard", "Connection, device, session and live traffic."),
            new("Manual", "Structured MDB actions and advanced raw diagnostics."),
            new("Automatic", "Asynchronous repeatable simulator scenarios."),
            new("Profiles", "Feature levels and independently declared capabilities."),
            new("Logs", "Filter, inspect, copy and export session traffic."),
            new("Wafer Discovery", "Preserve, analyze, export and reopen raw adapter evidence."),
            new("Settings", "Transport, serial and adapter debug configuration.")
        };
        _selectedPage = Pages[0];
        RefreshPorts();
        _selectedPort = Ports.Contains(settings.SerialPort) ? settings.SerialPort : null;

        Profiles = new ProfilesViewModel(new ProfileRepository(_paths, new ProfileJsonSerializer()), settings.LastProfileId);
        Logs = new LogsViewModel(_logs, new LogExportService(_paths));
        Automatic = new AutomaticViewModel(_logs, () => IsSimulationSelected);
        Manual = new ManualViewModel(_session, () => IsConnected, () => IsHardwareSelected,
            () => Profiles.Selected?.BaseLevel ?? MdbFeatureLevel.Level1, RefreshSessionProperties);
        Profiles.SelectionChanged += OnProfileSelectionChanged;
        Discovery = new WaferDiscoveryViewModel(_paths, BuildSettings, () => IsConnected);

        NavigateCommand = new RelayCommand(page => { if (page is NavigationItemViewModel item) SelectedPage = item; });
        RefreshPortsCommand = new RelayCommand(_ => RefreshPorts());
        ConnectCommand = new AsyncRelayCommand(_ => ConnectAsync());
        DisconnectCommand = new AsyncRelayCommand(_ => DisconnectAsync());
        SaveSettingsCommand = new AsyncRelayCommand(_ => SaveSettingsAsync());
    }

    public ObservableCollection<NavigationItemViewModel> Pages { get; } = [];
    public ObservableCollection<string> Ports { get; } = [];
    public LogsViewModel Logs { get; }
    public ProfilesViewModel Profiles { get; }
    public AutomaticViewModel Automatic { get; }
    public ManualViewModel Manual { get; }
    public WaferDiscoveryViewModel Discovery { get; }
    public IReadOnlyList<ProtocolSupportViewModel> ProtocolSupport { get; } =
    [
        new("Level 1", "Implemented", "Implemented", "Implemented", "Pending Wafer validation"),
        new("Level 2", "Partial · Revalue", "Partial · typed responses", "Partial", "Pending Wafer validation"),
        new("Level 3", "Partial / Experimental", "Partial / capability-gated", "Partial", "Pending Wafer validation")
    ];

    public IReadOnlyList<string> TransportLabels { get; } = ["Simulator", "Serial / Wafer"];
    public IReadOnlyList<SimulatorBehavior> SimulatorBehaviorOptions { get; } = Enum.GetValues<SimulatorBehavior>();
    public IReadOnlyList<int> BaudRateOptions { get; } = [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200];
    public IReadOnlyList<int> DataBitsOptions { get; } = [5, 6, 7, 8];
    public IReadOnlyList<Parity> ParityOptions { get; } = Enum.GetValues<Parity>();
    public IReadOnlyList<StopBits> StopBitsOptions { get; } = [StopBits.One, StopBits.OnePointFive, StopBits.Two];
    public IReadOnlyList<Handshake> HandshakeOptions { get; } = Enum.GetValues<Handshake>();
    public IReadOnlyList<PollingMode> PollingModeOptions { get; } = Enum.GetValues<PollingMode>();
    public IReadOnlyList<SerialWireFormat> WireFormatOptions { get; } = Enum.GetValues<SerialWireFormat>();
    public IReadOnlyList<AsciiHexTerminator> TerminatorOptions { get; } = Enum.GetValues<AsciiHexTerminator>();

    public ICommand NavigateCommand { get; }
    public ICommand RefreshPortsCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand SaveSettingsCommand { get; }

    public NavigationItemViewModel SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (!SetProperty(ref _selectedPage, value)) return;
            RaisePageVisibility();
        }
    }

    public bool IsDashboardVisible => SelectedPage.Title == "Dashboard";
    public bool IsManualVisible => SelectedPage.Title == "Manual";
    public bool IsAutomaticVisible => SelectedPage.Title == "Automatic";
    public bool IsProfilesVisible => SelectedPage.Title == "Profiles";
    public bool IsLogsVisible => SelectedPage.Title == "Logs";
    public bool IsWaferDiscoveryVisible => SelectedPage.Title == "Wafer Discovery";
    public bool IsSettingsVisible => SelectedPage.Title == "Settings";

    public TransportKind SelectedTransport
    {
        get => _selectedTransport;
        set
        {
            if (!SetProperty(ref _selectedTransport, value)) return;
            RaisePropertyChanged(nameof(SelectedTransportLabel));
            RaisePropertyChanged(nameof(IsSimulationSelected));
            RaisePropertyChanged(nameof(IsHardwareSelected));
            RaisePropertyChanged(nameof(ShowSimulationBanner));
            RaisePropertyChanged(nameof(TransportDisplay));
            RaisePropertyChanged(nameof(PortDisplay));
        }
    }
    public string SelectedTransportLabel
    {
        get => SelectedTransport == TransportKind.Simulated ? "Simulator" : "Serial / Wafer";
        set
        {
            SelectedTransport = value == "Serial / Wafer" ? TransportKind.WaferMdbRs232 : TransportKind.Simulated;
            RaisePropertyChanged();
        }
    }
    public SimulatorBehavior SelectedSimulatorBehavior { get => _selectedSimulatorBehavior; set => SetProperty(ref _selectedSimulatorBehavior, value); }
    public string? SelectedPort { get => _selectedPort; set { if (SetProperty(ref _selectedPort, value)) RaisePropertyChanged(nameof(PortDisplay)); } }
    public int SelectedBaudRate { get => _selectedBaudRate; set => SetProperty(ref _selectedBaudRate, value); }
    public int SelectedDataBits { get => _selectedDataBits; set => SetProperty(ref _selectedDataBits, value); }
    public Parity SelectedParity { get => _selectedParity; set => SetProperty(ref _selectedParity, value); }
    public StopBits SelectedStopBits { get => _selectedStopBits; set => SetProperty(ref _selectedStopBits, value); }
    public Handshake SelectedHandshake { get => _selectedHandshake; set => SetProperty(ref _selectedHandshake, value); }
    public PollingMode SelectedPollingMode { get => _selectedPollingMode; set { if (SetProperty(ref _selectedPollingMode, value)) RaisePropertyChanged(nameof(PollingDisplay)); } }
    public SerialWireFormat SelectedWireFormat { get => _selectedWireFormat; set { if (SetProperty(ref _selectedWireFormat, value)) RaisePropertyChanged(nameof(IsAsciiHex)); } }
    public AsciiHexTerminator SelectedTerminator { get => _selectedTerminator; set => SetProperty(ref _selectedTerminator, value); }
    public int TimeoutMilliseconds { get => _timeoutMilliseconds; set => SetProperty(ref _timeoutMilliseconds, value); }
    public int CaptureMaximumMegabytes { get => _captureMaximumMegabytes; set => SetProperty(ref _captureMaximumMegabytes, value); }
    public bool IsSimulationSelected => SelectedTransport == TransportKind.Simulated;
    public bool IsHardwareSelected => !IsSimulationSelected;
    public bool ShowSimulationBanner => IsConnected ? _session.IsSimulation : IsSimulationSelected;
    public bool CanChangeTransport => !IsConnected;
    public bool IsAsciiHex => SelectedWireFormat == SerialWireFormat.AsciiHex;
    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (!SetProperty(ref _isConnected, value)) return;
            RaisePropertyChanged(nameof(ShowSimulationBanner));
            RaisePropertyChanged(nameof(CanChangeTransport));
            RaisePropertyChanged(nameof(TransportDisplay));
            RaisePropertyChanged(nameof(PortDisplay));
        }
    }
    public string ConnectionStatus { get => _connectionStatus; private set => SetProperty(ref _connectionStatus, value); }
    public string ConnectionColor => IsConnected ? "#41C97A" : "#8D99AE";
    public string TransportDisplay => IsConnected
        ? _session.IsSimulation ? "Simulator" : "Serial / Wafer"
        : SelectedTransport == TransportKind.Simulated ? "Simulator" : "Serial / Wafer";
    public string PortDisplay => IsConnected && _session.IsSimulation || !IsConnected && IsSimulationSelected
        ? "Virtual" : SelectedPort ?? "None selected";
    public string PollingDisplay => SelectedPollingMode.ToString();
    public string CurrentState => _session.State;
    public string LastError { get => _lastError; private set => SetProperty(ref _lastError, value); }
    public string ActiveProfileName => Profiles.Name;
    public string ActiveFeatureLevel => Profiles.Level;
    public string DetectedFeatureLevel => IsConnected && _session.IsSimulation
        ? $"{ActiveFeatureLevel} (simulated)"
        : "Unconfirmed";

    public double WindowWidth { get => _windowWidth; set => SetProperty(ref _windowWidth, value); }
    public double WindowHeight { get => _windowHeight; set => SetProperty(ref _windowHeight, value); }

    private async Task ConnectAsync()
    {
        try
        {
            if (IsHardwareSelected && string.IsNullOrWhiteSpace(SelectedPort))
                throw new InvalidOperationException("Select an available serial port before connecting.");
            await _session.ConnectAsync(BuildSettings());
            IsConnected = true;
            ConnectionStatus = IsSimulationSelected ? "Connected — SIMULATION" : "Connected — adapter debug only";
            LastError = "None";
            RefreshSessionProperties();
        }
        catch (Exception exception)
        {
            IsConnected = false;
            ConnectionStatus = "Connection failed";
            LastError = FriendlyError(exception);
        }
        RaisePropertyChanged(nameof(ConnectionColor));
    }

    private async Task DisconnectAsync()
    {
        try { await _session.DisconnectAsync(); }
        catch (Exception exception) { LastError = FriendlyError(exception); }
        IsConnected = false;
        ConnectionStatus = "Disconnected";
        RaisePropertyChanged(nameof(ConnectionColor));
        RefreshSessionProperties();
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await _settingsStore.SaveAsync(_paths.Settings, BuildSettings());
            ConnectionStatus = IsConnected ? ConnectionStatus : $"Settings saved to {_paths.Settings}";
        }
        catch (Exception exception) { LastError = FriendlyError(exception); }
    }

    public async Task SaveWindowStateAsync(double width, double height)
    {
        WindowWidth = width;
        WindowHeight = height;
        await SaveSettingsAsync();
    }

    private void RefreshPorts()
    {
        try
        {
            var current = SelectedPort;
            Ports.Clear();
            foreach (var port in _portDiscovery.GetAvailablePorts()) Ports.Add(port);
            SelectedPort = current is not null && Ports.Contains(current) ? current : null;
            if (Ports.Count == 0 && IsHardwareSelected) ConnectionStatus = "No serial ports detected";
        }
        catch (Exception exception) { LastError = FriendlyError(exception); }
    }

    private AppSettings BuildSettings() => new()
    {
        SelectedTransport = SelectedTransport,
        SimulatorBehavior = SelectedSimulatorBehavior,
        SerialPort = SelectedPort ?? string.Empty,
        BaudRate = SelectedBaudRate,
        DataBits = SelectedDataBits,
        Parity = SelectedParity,
        StopBits = SelectedStopBits,
        Handshake = SelectedHandshake,
        PollingMode = SelectedPollingMode,
        TimeoutMilliseconds = Math.Clamp(TimeoutMilliseconds, 50, 120_000),
        WireFormat = SelectedWireFormat,
        AsciiHexTerminator = SelectedTerminator,
        CaptureMaximumMegabytes = Math.Clamp(CaptureMaximumMegabytes, 1, 1024),
        LastProfileId = Profiles.Selected?.Id ?? "standard-level1",
        WindowWidth = WindowWidth,
        WindowHeight = WindowHeight
    };

    private void RefreshSessionProperties()
    {
        Manual.Refresh();
        RaisePropertyChanged(nameof(CurrentState));
        RaisePropertyChanged(nameof(PortDisplay));
        RaisePropertyChanged(nameof(TransportDisplay));
        RaisePropertyChanged(nameof(DetectedFeatureLevel));
    }

    private void OnProfileSelectionChanged(object? sender, EventArgs e)
    {
        Manual.Refresh();
        RaisePropertyChanged(nameof(ActiveProfileName));
        RaisePropertyChanged(nameof(ActiveFeatureLevel));
        RaisePropertyChanged(nameof(DetectedFeatureLevel));
    }

    private void RaisePageVisibility()
    {
        RaisePropertyChanged(nameof(IsDashboardVisible));
        RaisePropertyChanged(nameof(IsManualVisible));
        RaisePropertyChanged(nameof(IsAutomaticVisible));
        RaisePropertyChanged(nameof(IsProfilesVisible));
        RaisePropertyChanged(nameof(IsLogsVisible));
        RaisePropertyChanged(nameof(IsWaferDiscoveryVisible));
        RaisePropertyChanged(nameof(IsSettingsVisible));
    }

    private static string FriendlyError(Exception exception) => exception switch
    {
        TransportException transport => transport.Message,
        UnauthorizedAccessException => "Permission denied. Check serial device permissions.",
        TimeoutException => "The operation timed out. Check connection and timing settings.",
        OperationCanceledException => "Operation cancelled by the user.",
        InvalidDataException => exception.Message,
        InvalidOperationException => exception.Message,
        IOException => "An I/O error occurred. Check the adapter and cable.",
        _ => "Unexpected error: " + exception.Message
    };

    public async ValueTask DisposeAsync()
    {
        Automatic.Dispose();
        Logs.Dispose();
        Profiles.SelectionChanged -= OnProfileSelectionChanged;
        await Discovery.DisposeAsync();
        await _session.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
