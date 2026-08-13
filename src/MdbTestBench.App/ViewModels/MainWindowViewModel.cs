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
using MdbTestBench.App.Localization;
using System.Globalization;
using System.Runtime.InteropServices;

namespace MdbTestBench.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly AppPaths _paths;
    private readonly JsonSettingsStore _settingsStore = new();
    private readonly ISerialPortDiscovery _portDiscovery;
    private readonly InMemoryMdbLogSink _logs = new();
    private readonly WorkbenchSession _session;
    private readonly ILocalizationService _localization;
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
    private string _connectionStatus;
    private string _lastError;
    private bool _hasLastError;
    private LocalizedOption<string> _selectedLanguageOption;
    private double _windowWidth;
    private double _windowHeight;

    public MainWindowViewModel(AppSettings settings, AppPaths? paths = null, ISerialPortDiscovery? portDiscovery = null,
        ILocalizationService? localization = null)
    {
        _localization = localization ?? new LocalizationService();
        if (localization is null)
            _localization.SetCulture(_localization.ResolveCulture(settings.Language, CultureInfo.CurrentUICulture).Name);
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
        _connectionStatus = _localization.Get("StatusDisconnected");
        _lastError = _localization.Get("None");

        Pages = new ObservableCollection<NavigationItemViewModel>
        {
            CreatePage("dashboard"), CreatePage("manual"), CreatePage("automatic"), CreatePage("profiles"),
            CreatePage("logs"), CreatePage("discovery"), CreatePage("settings")
        };
        _selectedPage = Pages[0];
        LanguageOptions = BuildLanguageOptions();
        _selectedLanguageOption = LanguageOptions.Single(item => item.Value == _localization.CurrentCulture.Name);
        TransportOptions.Add(new(TransportKind.Simulated, _localization.Get("Simulator")));
        TransportOptions.Add(new(TransportKind.WaferMdbRs232, _localization.Get("SerialWafer")));
        foreach (var behavior in Enum.GetValues<SimulatorBehavior>())
            SimulatorBehaviorOptions.Add(new(behavior, _localization.Get("SimulatorBehavior" + behavior)));
        foreach (var value in Enum.GetValues<Parity>()) ParityOptions.Add(new(value, _localization.Get("SerialParity" + value)));
        foreach (var value in new[] { StopBits.One, StopBits.OnePointFive, StopBits.Two }) StopBitsOptions.Add(new(value, _localization.Get("SerialStopBits" + value)));
        foreach (var value in Enum.GetValues<Handshake>()) HandshakeOptions.Add(new(value, _localization.Get("SerialHandshake" + value)));
        foreach (var value in Enum.GetValues<PollingMode>()) PollingModeOptions.Add(new(value, _localization.Get("Polling" + value)));
        foreach (var value in Enum.GetValues<SerialWireFormat>()) WireFormatOptions.Add(new(value, _localization.Get("WireFormat" + value)));
        foreach (var value in Enum.GetValues<AsciiHexTerminator>()) TerminatorOptions.Add(new(value, _localization.Get("Terminator" + value)));
        RefreshPorts();
        _selectedPort = Ports.Contains(settings.SerialPort) ? settings.SerialPort : null;

        Profiles = new ProfilesViewModel(new ProfileRepository(_paths, new ProfileJsonSerializer()), settings.LastProfileId, _localization);
        Logs = new LogsViewModel(_logs, new LogExportService(_paths), localization: _localization);
        Automatic = new AutomaticViewModel(_logs, () => IsSimulationSelected, _localization);
        Manual = new ManualViewModel(_session, () => IsConnected, () => IsHardwareSelected,
            () => Profiles.Selected?.BaseLevel ?? MdbFeatureLevel.Level1, RefreshSessionProperties, _localization);
        Profiles.SelectionChanged += OnProfileSelectionChanged;
        Discovery = new WaferDiscoveryViewModel(_paths, BuildSettings, () => IsConnected, _localization);
        ProtocolSupport = new ObservableCollection<ProtocolSupportViewModel>();
        RelocalizeProtocolSupport();
        _localization.CultureChanged += OnCultureChanged;

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
    public ObservableCollection<ProtocolSupportViewModel> ProtocolSupport { get; }

    public ObservableCollection<LocalizedOption<TransportKind>> TransportOptions { get; } = [];
    public IReadOnlyList<LocalizedOption<string>> LanguageOptions { get; }
    public ObservableCollection<LocalizedOption<SimulatorBehavior>> SimulatorBehaviorOptions { get; } = [];
    public IReadOnlyList<int> BaudRateOptions { get; } = [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200];
    public IReadOnlyList<int> DataBitsOptions { get; } = [5, 6, 7, 8];
    public ObservableCollection<LocalizedOption<Parity>> ParityOptions { get; } = [];
    public ObservableCollection<LocalizedOption<StopBits>> StopBitsOptions { get; } = [];
    public ObservableCollection<LocalizedOption<Handshake>> HandshakeOptions { get; } = [];
    public ObservableCollection<LocalizedOption<PollingMode>> PollingModeOptions { get; } = [];
    public ObservableCollection<LocalizedOption<SerialWireFormat>> WireFormatOptions { get; } = [];
    public ObservableCollection<LocalizedOption<AsciiHexTerminator>> TerminatorOptions { get; } = [];

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

    public bool IsDashboardVisible => SelectedPage.Id == "dashboard";
    public bool IsManualVisible => SelectedPage.Id == "manual";
    public bool IsAutomaticVisible => SelectedPage.Id == "automatic";
    public bool IsProfilesVisible => SelectedPage.Id == "profiles";
    public bool IsLogsVisible => SelectedPage.Id == "logs";
    public bool IsWaferDiscoveryVisible => SelectedPage.Id == "discovery";
    public bool IsSettingsVisible => SelectedPage.Id == "settings";

    public TransportKind SelectedTransport
    {
        get => _selectedTransport;
        set
        {
            if (!SetProperty(ref _selectedTransport, value)) return;
            RaisePropertyChanged(nameof(SelectedTransportOption));
            RaisePropertyChanged(nameof(IsSimulationSelected));
            RaisePropertyChanged(nameof(IsHardwareSelected));
            RaisePropertyChanged(nameof(ShowSimulationBanner));
            RaisePropertyChanged(nameof(TransportDisplay));
            RaisePropertyChanged(nameof(PortDisplay));
        }
    }
    public LocalizedOption<TransportKind> SelectedTransportOption
    {
        get => TransportOptions.Single(item => item.Value == SelectedTransport);
        set
        {
            if (value is not null) SelectedTransport = value.Value;
        }
    }
    public LocalizedOption<string> SelectedLanguageOption
    {
        get => _selectedLanguageOption;
        set
        {
            if (value is null || !SetProperty(ref _selectedLanguageOption, value)) return;
            _localization.SetCulture(value.Value);
            _ = SaveSettingsAsync(showStatus: false);
        }
    }
    public LocalizedOption<SimulatorBehavior> SelectedSimulatorBehaviorOption
    {
        get => SimulatorBehaviorOptions.Single(item => item.Value == _selectedSimulatorBehavior);
        set { if (value is not null && SetProperty(ref _selectedSimulatorBehavior, value.Value, nameof(SelectedSimulatorBehaviorOption))) RaisePropertyChanged(nameof(SelectedSimulatorBehavior)); }
    }
    public SimulatorBehavior SelectedSimulatorBehavior { get => _selectedSimulatorBehavior; set { if (SetProperty(ref _selectedSimulatorBehavior, value)) RaisePropertyChanged(nameof(SelectedSimulatorBehaviorOption)); } }
    public string? SelectedPort { get => _selectedPort; set { if (SetProperty(ref _selectedPort, value)) RaisePropertyChanged(nameof(PortDisplay)); } }
    public int SelectedBaudRate { get => _selectedBaudRate; set => SetProperty(ref _selectedBaudRate, value); }
    public int SelectedDataBits { get => _selectedDataBits; set => SetProperty(ref _selectedDataBits, value); }
    public LocalizedOption<Parity> SelectedParityOption { get => ParityOptions.Single(item => item.Value == _selectedParity); set { if (value is not null) _selectedParity = value.Value; } }
    public LocalizedOption<StopBits> SelectedStopBitsOption { get => StopBitsOptions.Single(item => item.Value == _selectedStopBits); set { if (value is not null) _selectedStopBits = value.Value; } }
    public LocalizedOption<Handshake> SelectedHandshakeOption { get => HandshakeOptions.Single(item => item.Value == _selectedHandshake); set { if (value is not null) _selectedHandshake = value.Value; } }
    public LocalizedOption<PollingMode> SelectedPollingModeOption { get => PollingModeOptions.Single(item => item.Value == _selectedPollingMode); set { if (value is not null) SelectedPollingMode = value.Value; } }
    public LocalizedOption<SerialWireFormat> SelectedWireFormatOption { get => WireFormatOptions.Single(item => item.Value == _selectedWireFormat); set { if (value is not null) SelectedWireFormat = value.Value; } }
    public LocalizedOption<AsciiHexTerminator> SelectedTerminatorOption { get => TerminatorOptions.Single(item => item.Value == _selectedTerminator); set { if (value is not null) _selectedTerminator = value.Value; } }
    public Parity SelectedParity { get => _selectedParity; set { if (SetProperty(ref _selectedParity, value)) RaisePropertyChanged(nameof(SelectedParityOption)); } }
    public StopBits SelectedStopBits { get => _selectedStopBits; set { if (SetProperty(ref _selectedStopBits, value)) RaisePropertyChanged(nameof(SelectedStopBitsOption)); } }
    public Handshake SelectedHandshake { get => _selectedHandshake; set { if (SetProperty(ref _selectedHandshake, value)) RaisePropertyChanged(nameof(SelectedHandshakeOption)); } }
    public PollingMode SelectedPollingMode { get => _selectedPollingMode; set { if (SetProperty(ref _selectedPollingMode, value)) { RaisePropertyChanged(nameof(PollingDisplay)); RaisePropertyChanged(nameof(SelectedPollingModeOption)); } } }
    public SerialWireFormat SelectedWireFormat { get => _selectedWireFormat; set { if (SetProperty(ref _selectedWireFormat, value)) { RaisePropertyChanged(nameof(IsAsciiHex)); RaisePropertyChanged(nameof(SelectedWireFormatOption)); } } }
    public AsciiHexTerminator SelectedTerminator { get => _selectedTerminator; set { if (SetProperty(ref _selectedTerminator, value)) RaisePropertyChanged(nameof(SelectedTerminatorOption)); } }
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
            RaisePropertyChanged(nameof(SelectedTransportOption));
        }
    }
    public string ConnectionStatus { get => _connectionStatus; private set => SetProperty(ref _connectionStatus, value); }
    public string ConnectionColor => IsConnected ? "#41C97A" : "#8D99AE";
    public string TransportDisplay => IsConnected
        ? _session.IsSimulation ? _localization.Get("Simulator") : _localization.Get("SerialWafer")
        : SelectedTransport == TransportKind.Simulated ? _localization.Get("Simulator") : _localization.Get("SerialWafer");
    public string PortDisplay => IsConnected && _session.IsSimulation || !IsConnected && IsSimulationSelected
        ? _localization.Get("Virtual") : SelectedPort ?? _localization.Get("NoneSelected");
    public string PollingDisplay => _localization.Get("Polling" + SelectedPollingMode);
    public string CurrentState => _localization.Get("State" + _session.State.Replace(" / codec unverified", "CodecUnverified", StringComparison.Ordinal));
    public string LastError { get => _lastError; private set => SetProperty(ref _lastError, value); }
    public string ActiveProfileName => Profiles.Name;
    public string ActiveFeatureLevel => Profiles.Level;
    public string DetectedFeatureLevel => IsConnected && _session.IsSimulation
        ? _localization.Format("SimulatedValue", ActiveFeatureLevel)
        : _localization.Get("Unconfirmed");
    public string AboutProduct => _localization.Format("AboutProduct", "0.1.1");
    public string AboutRuntime => _localization.Format("AboutRuntime", RuntimeInformation.FrameworkDescription);
    public string AboutSystem => _localization.Format("AboutSystem", RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture);

    public double WindowWidth { get => _windowWidth; set => SetProperty(ref _windowWidth, value); }
    public double WindowHeight { get => _windowHeight; set => SetProperty(ref _windowHeight, value); }

    private async Task ConnectAsync()
    {
        try
        {
            if (IsHardwareSelected && string.IsNullOrWhiteSpace(SelectedPort))
                throw new InvalidOperationException(_localization.Get("ErrorSelectPort"));
            await _session.ConnectAsync(BuildSettings());
            IsConnected = true;
            ConnectionStatus = IsSimulationSelected ? _localization.Get("StatusConnectedSimulation") : _localization.Get("StatusConnectedAdapterDebug");
            LastError = _localization.Get("None");
            _hasLastError = false;
            RefreshSessionProperties();
        }
        catch (Exception exception)
        {
            IsConnected = false;
            ConnectionStatus = _localization.Get("StatusConnectionFailed");
            LastError = FriendlyError(exception);
            _hasLastError = true;
        }
        RaisePropertyChanged(nameof(ConnectionColor));
    }

    private async Task DisconnectAsync()
    {
        try { await _session.DisconnectAsync(); }
        catch (Exception exception) { LastError = FriendlyError(exception); _hasLastError = true; }
        IsConnected = false;
        ConnectionStatus = _localization.Get("StatusDisconnected");
        RaisePropertyChanged(nameof(ConnectionColor));
        RefreshSessionProperties();
    }

    private async Task SaveSettingsAsync(bool showStatus = true)
    {
        try
        {
            await _settingsStore.SaveAsync(_paths.Settings, BuildSettings());
            if (showStatus && !IsConnected) ConnectionStatus = _localization.Format("StatusSettingsSaved", _paths.Settings);
        }
        catch (Exception exception) { LastError = FriendlyError(exception); _hasLastError = true; }
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
            if (Ports.Count == 0 && IsHardwareSelected) ConnectionStatus = _localization.Get("StatusNoPorts");
        }
        catch (Exception exception) { LastError = FriendlyError(exception); _hasLastError = true; }
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
        WindowHeight = WindowHeight,
        Language = _localization.CurrentCulture.Name
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

    private string FriendlyError(Exception exception) => exception switch
    {
        TransportException transport => transport.Message,
        UnauthorizedAccessException => _localization.Get("ErrorPermissionDenied"),
        TimeoutException => _localization.Get("ErrorTimeout"),
        OperationCanceledException => _localization.Get("ErrorCancelled"),
        InvalidDataException => exception.Message,
        InvalidOperationException => exception.Message,
        IOException => _localization.Get("ErrorIo"),
        _ => _localization.Format("ErrorUnexpected", exception.Message)
    };

    private NavigationItemViewModel CreatePage(string id) => new(id,
        _localization.Get("Nav" + char.ToUpperInvariant(id[0]) + id[1..]),
        _localization.Get("Nav" + char.ToUpperInvariant(id[0]) + id[1..] + "Description"));

    private IReadOnlyList<LocalizedOption<string>> BuildLanguageOptions() =>
    [
        new(LocalizationService.PortugueseCultureName, _localization.Get("LanguagePortuguese")),
        new(LocalizationService.EnglishCultureName, _localization.Get("LanguageEnglish"))
    ];

    private void RelocalizeProtocolSupport()
    {
        ProtocolSupport.Clear();
        ProtocolSupport.Add(new("Level 1", _localization.Get("Implemented"), _localization.Get("Implemented"), _localization.Get("Implemented"), _localization.Get("PendingWaferValidation")));
        ProtocolSupport.Add(new("Level 2", _localization.Get("PartialRevalue"), _localization.Get("PartialTypedResponses"), _localization.Get("Partial"), _localization.Get("PendingWaferValidation")));
        ProtocolSupport.Add(new("Level 3", _localization.Get("PartialExperimental"), _localization.Get("PartialCapabilityGated"), _localization.Get("Partial"), _localization.Get("PendingWaferValidation")));
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        foreach (var page in Pages)
        {
            var key = "Nav" + char.ToUpperInvariant(page.Id[0]) + page.Id[1..];
            page.Relocalize(_localization.Get(key), _localization.Get(key + "Description"));
        }
        TransportOptions[0].Relocalize(_localization.Get("Simulator"));
        TransportOptions[1].Relocalize(_localization.Get("SerialWafer"));
        LanguageOptions[0].Relocalize(_localization.Get("LanguagePortuguese"));
        LanguageOptions[1].Relocalize(_localization.Get("LanguageEnglish"));
        foreach (var option in SimulatorBehaviorOptions)
            option.Relocalize(_localization.Get("SimulatorBehavior" + option.Value));
        foreach (var option in ParityOptions) option.Relocalize(_localization.Get("SerialParity" + option.Value));
        foreach (var option in StopBitsOptions) option.Relocalize(_localization.Get("SerialStopBits" + option.Value));
        foreach (var option in HandshakeOptions) option.Relocalize(_localization.Get("SerialHandshake" + option.Value));
        foreach (var option in PollingModeOptions) option.Relocalize(_localization.Get("Polling" + option.Value));
        foreach (var option in WireFormatOptions) option.Relocalize(_localization.Get("WireFormat" + option.Value));
        foreach (var option in TerminatorOptions) option.Relocalize(_localization.Get("Terminator" + option.Value));
        RelocalizeProtocolSupport();
        Profiles.RefreshLocalization(); Logs.RefreshLocalization(); Automatic.RefreshLocalization(); Manual.RefreshLocalization(); Discovery.RefreshLocalization();
        RaisePropertyChanged(nameof(SelectedTransportOption)); RaisePropertyChanged(nameof(TransportDisplay)); RaisePropertyChanged(nameof(PortDisplay));
        RaisePropertyChanged(nameof(PollingDisplay)); RaisePropertyChanged(nameof(CurrentState)); RaisePropertyChanged(nameof(DetectedFeatureLevel));
        RaisePropertyChanged(nameof(AboutProduct)); RaisePropertyChanged(nameof(AboutRuntime)); RaisePropertyChanged(nameof(AboutSystem));
        if (!IsConnected) ConnectionStatus = _localization.Get("StatusDisconnected");
        if (!_hasLastError) LastError = _localization.Get("None");
    }

    public async ValueTask DisposeAsync()
    {
        Automatic.Dispose();
        Logs.Dispose();
        Profiles.SelectionChanged -= OnProfileSelectionChanged;
        _localization.CultureChanged -= OnCultureChanged;
        await Discovery.DisposeAsync();
        await _session.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
