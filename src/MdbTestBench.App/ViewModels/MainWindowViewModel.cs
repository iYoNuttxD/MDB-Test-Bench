using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Ports;
using System.Windows.Input;
using Avalonia.Threading;
using MdbTestBench.App.Services;
using MdbTestBench.Core.Capabilities;
using MdbTestBench.Core.Logging;
using MdbTestBench.Core.Profiles;
using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Commands;
using MdbTestBench.Core.Protocol.Encoding;
using MdbTestBench.TestEngine;
using MdbTestBench.TestEngine.Models;
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
    private readonly ProfileRepository _profileRepository;
    private readonly LogExportService _logExport;
    private readonly ClipboardService _clipboard = new();
    private NavigationItemViewModel _selectedPage;
    private TransportKind _selectedTransport;
    private SimulatorBehavior _selectedSimulatorBehavior;
    private string? _selectedPort;
    private int _selectedBaudRate;
    private int _selectedDataBits;
    private Parity _selectedParity;
    private StopBits _selectedStopBits;
    private PollingMode _selectedPollingMode;
    private SerialWireFormat _selectedWireFormat;
    private AsciiHexTerminator _selectedTerminator;
    private int _timeoutMilliseconds;
    private bool _isConnected;
    private string _connectionStatus = "Disconnected";
    private string _lastError = "None";
    private string _lastCommand = "—";
    private string _lastResponse = "—";
    private ManualCommandKind _selectedManualCommand;
    private string _manualPrice = "5.00";
    private string _manualProduct = "1";
    private string _manualValue = string.Empty;
    private string _manualPreview = string.Empty;
    private string _manualMessage = "Ready";
    private string _rawHex = string.Empty;
    private string _rawValidation = "Enter bytes to validate.";
    private bool _rawConfirmed;
    private bool _logsPaused;
    private bool _autoScroll = true;
    private bool _showTx = true;
    private bool _showRx = true;
    private bool _showErrors = true;
    private string _logSearch = string.Empty;
    private LogEntryViewModel? _selectedLog;
    private string _logMessage = "Session logging ready";
    private MdbProfile? _selectedProfile;
    private string _profileMessage = "Built-in profiles are read-only.";
    private string _profileImportPath = string.Empty;
    private ScenarioDisplayViewModel? _selectedScenario;
    private bool _isScenarioRunning;
    private int _scenarioPasses;
    private int _scenarioFailures;
    private string _scenarioSummary = "Select a scenario and run it in Simulator mode.";
    private CancellationTokenSource? _scenarioCancellation;
    private double _windowWidth;
    private double _windowHeight;

    public MainWindowViewModel(AppSettings settings, AppPaths? paths = null, ISerialPortDiscovery? portDiscovery = null)
    {
        _paths = paths ?? new AppPaths();
        _paths.EnsureDirectories();
        _portDiscovery = portDiscovery ?? new SerialPortDiscovery();
        _session = new WorkbenchSession(_logs);
        _profileRepository = new ProfileRepository(_paths, new ProfileJsonSerializer());
        _logExport = new LogExportService(_paths);

        _selectedTransport = settings.SelectedTransport;
        _selectedSimulatorBehavior = settings.SimulatorBehavior;
        _selectedBaudRate = settings.BaudRate;
        _selectedDataBits = settings.DataBits;
        _selectedParity = settings.Parity;
        _selectedStopBits = settings.StopBits;
        _selectedPollingMode = settings.PollingMode;
        _selectedWireFormat = settings.WireFormat;
        _selectedTerminator = settings.AsciiHexTerminator;
        _timeoutMilliseconds = settings.TimeoutMilliseconds;
        _windowWidth = settings.WindowWidth;
        _windowHeight = settings.WindowHeight;

        Pages = new ObservableCollection<NavigationItemViewModel>
        {
            new("Dashboard", "Connection, device, session and live traffic."),
            new("Manual", "Structured MDB actions and advanced raw diagnostics."),
            new("Automatic", "Asynchronous repeatable simulator scenarios."),
            new("Profiles", "Feature levels and independently declared capabilities."),
            new("Logs", "Filter, inspect, copy and export session traffic."),
            new("Settings", "Transport, serial and adapter debug configuration.")
        };
        _selectedPage = Pages[0];
        RefreshPorts();
        _selectedPort = Ports.Contains(settings.SerialPort) ? settings.SerialPort : null;

        foreach (var profile in _profileRepository.LoadAll()) Profiles.Add(profile);
        if (_profileRepository.LoadWarnings.Count > 0)
            _profileMessage = $"{_profileRepository.LoadWarnings.Count} invalid custom profile file(s) were skipped.";
        SelectedProfile = Profiles.FirstOrDefault(profile => profile.Id == settings.LastProfileId) ?? Profiles.FirstOrDefault();
        foreach (var scenario in ScenarioCatalog.CreateBuiltIn()) Scenarios.Add(new ScenarioDisplayViewModel(scenario));
        SelectedScenario = Scenarios.FirstOrDefault();

        NavigateCommand = new RelayCommand(page => { if (page is NavigationItemViewModel item) SelectedPage = item; });
        RefreshPortsCommand = new RelayCommand(_ => RefreshPorts());
        ConnectCommand = new AsyncRelayCommand(_ => ConnectAsync());
        DisconnectCommand = new AsyncRelayCommand(_ => DisconnectAsync());
        SaveSettingsCommand = new AsyncRelayCommand(_ => SaveSettingsAsync());
        SendStructuredCommand = new AsyncRelayCommand(_ => SendStructuredAsync());
        SendRawCommand = new AsyncRelayCommand(_ => SendRawAsync());
        ClearLogsCommand = new RelayCommand(_ => ClearLogs());
        ExportLogsCommand = new AsyncRelayCommand(_ => ExportLogsAsync());
        CopyLogLineCommand = new AsyncRelayCommand(_ => CopyLogLineAsync());
        CopyRawHexCommand = new AsyncRelayCommand(_ => CopyRawHexAsync());
        NewProfileCommand = new RelayCommand(_ => NewProfile());
        DuplicateProfileCommand = new RelayCommand(_ => DuplicateProfile());
        DeleteProfileCommand = new RelayCommand(_ => DeleteProfile());
        SaveProfileCommand = new AsyncRelayCommand(_ => SaveProfileAsync());
        ImportProfileCommand = new AsyncRelayCommand(_ => ImportProfileAsync());
        ExportProfileCommand = new AsyncRelayCommand(_ => ExportProfileAsync());
        RunScenarioCommand = new AsyncRelayCommand(_ => RunScenarioAsync());
        CancelScenarioCommand = new RelayCommand(_ => _scenarioCancellation?.Cancel());

        _logs.EntryAdded += OnLogAdded;
        UpdateManualPreview();
    }

    public ObservableCollection<NavigationItemViewModel> Pages { get; } = [];
    public ObservableCollection<string> Ports { get; } = [];
    public ObservableCollection<LogEntryViewModel> VisibleLogs { get; } = [];
    public ObservableCollection<LogEntryViewModel> LiveTraffic { get; } = [];
    public ObservableCollection<MdbProfile> Profiles { get; } = [];
    public ObservableCollection<ScenarioDisplayViewModel> Scenarios { get; } = [];
    public ProfileEditorViewModel ProfileEditor { get; } = new();

    public IReadOnlyList<string> TransportLabels { get; } = ["Simulator", "Serial / Wafer"];
    public IReadOnlyList<SimulatorBehavior> SimulatorBehaviorOptions { get; } = Enum.GetValues<SimulatorBehavior>();
    public IReadOnlyList<int> BaudRateOptions { get; } = [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200];
    public IReadOnlyList<int> DataBitsOptions { get; } = [5, 6, 7, 8];
    public IReadOnlyList<Parity> ParityOptions { get; } = Enum.GetValues<Parity>();
    public IReadOnlyList<StopBits> StopBitsOptions { get; } = [StopBits.One, StopBits.OnePointFive, StopBits.Two];
    public IReadOnlyList<PollingMode> PollingModeOptions { get; } = Enum.GetValues<PollingMode>();
    public IReadOnlyList<SerialWireFormat> WireFormatOptions { get; } = Enum.GetValues<SerialWireFormat>();
    public IReadOnlyList<AsciiHexTerminator> TerminatorOptions { get; } = Enum.GetValues<AsciiHexTerminator>();
    public IReadOnlyList<ManualCommandKind> ManualCommandOptions { get; } = Enum.GetValues<ManualCommandKind>();
    public IReadOnlyList<MdbFeatureLevel> FeatureLevelOptions { get; } = Enum.GetValues<MdbFeatureLevel>();
    public IReadOnlyList<CapabilityStatus> CapabilityStatusOptions { get; } = Enum.GetValues<CapabilityStatus>();

    public ICommand NavigateCommand { get; }
    public ICommand RefreshPortsCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand SendStructuredCommand { get; }
    public ICommand SendRawCommand { get; }
    public ICommand ClearLogsCommand { get; }
    public ICommand ExportLogsCommand { get; }
    public ICommand CopyLogLineCommand { get; }
    public ICommand CopyRawHexCommand { get; }
    public ICommand NewProfileCommand { get; }
    public ICommand DuplicateProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand ImportProfileCommand { get; }
    public ICommand ExportProfileCommand { get; }
    public ICommand RunScenarioCommand { get; }
    public ICommand CancelScenarioCommand { get; }

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
    public PollingMode SelectedPollingMode { get => _selectedPollingMode; set { if (SetProperty(ref _selectedPollingMode, value)) RaisePropertyChanged(nameof(PollingDisplay)); } }
    public SerialWireFormat SelectedWireFormat { get => _selectedWireFormat; set { if (SetProperty(ref _selectedWireFormat, value)) RaisePropertyChanged(nameof(IsAsciiHex)); } }
    public AsciiHexTerminator SelectedTerminator { get => _selectedTerminator; set => SetProperty(ref _selectedTerminator, value); }
    public int TimeoutMilliseconds { get => _timeoutMilliseconds; set => SetProperty(ref _timeoutMilliseconds, value); }
    public bool IsSimulationSelected => SelectedTransport == TransportKind.Simulated;
    public bool IsHardwareSelected => !IsSimulationSelected;
    public bool ShowSimulationBanner => IsConnected ? _session.IsSimulation : IsSimulationSelected;
    public bool CanChangeTransport => !IsConnected;
    public bool CanUseStructured => IsConnected && _session.IsSimulation;
    public bool IsAsciiHex => SelectedWireFormat == SerialWireFormat.AsciiHex;
    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (!SetProperty(ref _isConnected, value)) return;
            RaisePropertyChanged(nameof(ShowSimulationBanner));
            RaisePropertyChanged(nameof(CanChangeTransport));
            RaisePropertyChanged(nameof(CanUseStructured));
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
    public string LastCommand { get => _lastCommand; private set => SetProperty(ref _lastCommand, value); }
    public string LastResponse { get => _lastResponse; private set => SetProperty(ref _lastResponse, value); }
    public string ActiveProfileName => SelectedProfile?.Name ?? "None";
    public string ActiveFeatureLevel => SelectedProfile?.BaseLevel.ToString() ?? "Unknown";
    public string DetectedFeatureLevel => IsConnected && _session.IsSimulation
        ? $"{ActiveFeatureLevel} (simulated)"
        : "Unconfirmed";

    public ManualCommandKind SelectedManualCommand { get => _selectedManualCommand; set { if (SetProperty(ref _selectedManualCommand, value)) UpdateManualPreview(); } }
    public string ManualPrice { get => _manualPrice; set { if (SetProperty(ref _manualPrice, value)) UpdateManualPreview(); } }
    public string ManualProduct { get => _manualProduct; set { if (SetProperty(ref _manualProduct, value)) UpdateManualPreview(); } }
    public string ManualValue { get => _manualValue; set { if (SetProperty(ref _manualValue, value)) UpdateManualPreview(); } }
    public string ManualPreview { get => _manualPreview; private set => SetProperty(ref _manualPreview, value); }
    public string ManualMessage { get => _manualMessage; private set => SetProperty(ref _manualMessage, value); }
    public string RawHex { get => _rawHex; set { if (SetProperty(ref _rawHex, value)) ValidateRaw(); } }
    public string RawValidation { get => _rawValidation; private set => SetProperty(ref _rawValidation, value); }
    public bool RawConfirmed { get => _rawConfirmed; set => SetProperty(ref _rawConfirmed, value); }

    public bool LogsPaused { get => _logsPaused; set { if (SetProperty(ref _logsPaused, value) && !value) ApplyLogFilters(); } }
    public bool AutoScroll { get => _autoScroll; set => SetProperty(ref _autoScroll, value); }
    public bool ShowTx { get => _showTx; set { if (SetProperty(ref _showTx, value)) ApplyLogFilters(); } }
    public bool ShowRx { get => _showRx; set { if (SetProperty(ref _showRx, value)) ApplyLogFilters(); } }
    public bool ShowErrors { get => _showErrors; set { if (SetProperty(ref _showErrors, value)) ApplyLogFilters(); } }
    public string LogSearch { get => _logSearch; set { if (SetProperty(ref _logSearch, value)) ApplyLogFilters(); } }
    public LogEntryViewModel? SelectedLog { get => _selectedLog; set => SetProperty(ref _selectedLog, value); }
    public string LogMessage { get => _logMessage; private set => SetProperty(ref _logMessage, value); }

    public MdbProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value) || value is null) return;
            ProfileEditor.Load(value);
            RaisePropertyChanged(nameof(ActiveProfileName));
            RaisePropertyChanged(nameof(ActiveFeatureLevel));
            RaisePropertyChanged(nameof(DetectedFeatureLevel));
        }
    }
    public string ProfileMessage { get => _profileMessage; private set => SetProperty(ref _profileMessage, value); }
    public string ProfileImportPath { get => _profileImportPath; set => SetProperty(ref _profileImportPath, value); }

    public ScenarioDisplayViewModel? SelectedScenario { get => _selectedScenario; set => SetProperty(ref _selectedScenario, value); }
    public bool IsScenarioRunning { get => _isScenarioRunning; private set => SetProperty(ref _isScenarioRunning, value); }
    public int ScenarioTotal => SelectedScenario?.Steps.Count ?? 0;
    public int ScenarioPasses { get => _scenarioPasses; private set => SetProperty(ref _scenarioPasses, value); }
    public int ScenarioFailures { get => _scenarioFailures; private set => SetProperty(ref _scenarioFailures, value); }
    public string ScenarioSummary { get => _scenarioSummary; private set => SetProperty(ref _scenarioSummary, value); }
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

    private async Task SendStructuredAsync()
    {
        try
        {
            if (!IsConnected) throw new InvalidOperationException("Connect the Simulator before sending a command.");
            var command = BuildManualCommand();
            if (!_session.CanSend(command.Frame))
                throw new InvalidOperationException($"{command.Trigger} is blocked while the VMC is in {_session.State}.");
            LastCommand = command.Frame.Subcommand == MdbSubcommandType.None
                ? command.Frame.Command.ToString() : $"{command.Frame.Command} {command.Frame.Subcommand}";
            var response = await _session.ExchangeAsync(command.Frame);
            LastResponse = response.Response?.ToString() ?? "Unknown";
            ManualMessage = $"Received {LastResponse}.";
            LastError = "None";
        }
        catch (Exception exception)
        {
            ManualMessage = FriendlyError(exception);
            LastError = ManualMessage;
        }
        RefreshSessionProperties();
    }

    private async Task SendRawAsync()
    {
        try
        {
            if (!RawConfirmed) throw new InvalidOperationException("Confirm the advanced raw payload before sending.");
            var parsed = HexParser.Parse(RawHex);
            if (!parsed.IsValid) throw new InvalidDataException(parsed.Error);
            var result = await _session.ExchangeRawAsync(parsed.Bytes);
            ManualMessage = $"Raw response: {MdbLogFormatter.FormatHex(result.ResponseBytes.Span)}";
            RawConfirmed = false;
        }
        catch (Exception exception)
        {
            ManualMessage = FriendlyError(exception);
            LastError = ManualMessage;
        }
    }

    private async Task RunScenarioAsync()
    {
        if (SelectedScenario is null || IsScenarioRunning) return;
        if (!IsSimulationSelected)
        {
            ScenarioSummary = "Automatic v0.1 runs only in Simulator mode until the Wafer codec is validated.";
            return;
        }

        IsScenarioRunning = true;
        ScenarioPasses = 0;
        ScenarioFailures = 0;
        foreach (var step in SelectedScenario.Steps) { step.Status = "PENDING"; step.Received = "—"; }
        _scenarioCancellation = new CancellationTokenSource();
        await using var simulator = new SimulatedCashlessTransport(new SimulatedCashlessOptions
        {
            Behavior = SelectedScenario.Scenario.SimulatorBehavior,
            OperationTimeout = SelectedScenario.Scenario.SimulatorBehavior == SimulatorBehavior.Timeout
                ? TimeSpan.FromMilliseconds(200) : TimeSpan.FromSeconds(2)
        });
        var runner = new ScenarioRunner(simulator, _logs);
        var index = 0;
        runner.StepCompleted += (_, result) => Dispatcher.UIThread.Post(() =>
        {
            if (index >= SelectedScenario.Steps.Count) return;
            var display = SelectedScenario.Steps[index++];
            display.Status = result.Passed ? "PASS" : "FAIL";
            display.Received = result.ActualResponse?.ToString() ?? result.Error ?? "No response";
        });
        try
        {
            var result = await runner.RunAsync(SelectedScenario.Scenario, _scenarioCancellation.Token);
            ScenarioPasses = result.Steps.Count(step => step.Passed);
            ScenarioFailures = result.Steps.Count(step => !step.Passed);
            var effectivePass = result.Status == TestRunStatus.Passed ||
                (SelectedScenario.Scenario.SimulatorBehavior == SimulatorBehavior.Timeout && result.Status == TestRunStatus.TimedOut) ||
                (SelectedScenario.Scenario.SimulatorBehavior == SimulatorBehavior.UnexpectedResponse && result.Status == TestRunStatus.Failed);
            SelectedScenario.Result = result.Status == TestRunStatus.Aborted ? "ABORTED" : effectivePass ? "PASS" : "FAIL";
            SelectedScenario.Duration = $"{result.Duration.TotalMilliseconds:0} ms";
            ScenarioSummary = effectivePass
                ? $"PASS — {ScenarioPasses} expected step responses; behavior handled as designed."
                : $"{SelectedScenario.Result} — {result.Error ?? "Review expected and received values."}";
        }
        finally
        {
            _scenarioCancellation.Dispose();
            _scenarioCancellation = null;
            IsScenarioRunning = false;
            RaisePropertyChanged(nameof(ScenarioTotal));
        }
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

    private void UpdateManualPreview()
    {
        try
        {
            var command = BuildManualCommand();
            ManualPreview = $"{command.Frame.Command} {command.Frame.Subcommand} · {command.LogicalPayload}";
        }
        catch (Exception exception) { ManualPreview = FriendlyError(exception); }
    }

    private ManualCommandBuildResult BuildManualCommand() => ManualCommandBuilder.Build(new ManualCommandInput(
        SelectedManualCommand,
        ParseNullableDecimal(ManualPrice),
        int.TryParse(ManualProduct, NumberStyles.Integer, CultureInfo.InvariantCulture, out var product) ? product : null,
        ParseNullableDecimal(ManualValue)));

    private static decimal? ParseNullableDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;

    private void ValidateRaw()
    {
        var result = HexParser.Parse(RawHex);
        RawValidation = result.IsValid ? $"Valid · {result.Bytes.Length} byte(s) · {result.NormalizedHex}" : result.Error ?? "Invalid";
        if (!result.IsValid) RawConfirmed = false;
    }

    private void OnLogAdded(object? sender, MdbLogEntry entry) => Dispatcher.UIThread.Post(() =>
    {
        var model = new LogEntryViewModel(entry);
        if (!LogsPaused && MatchesLogFilters(model)) VisibleLogs.Add(model);
        LiveTraffic.Insert(0, model);
        while (LiveTraffic.Count > 8) LiveTraffic.RemoveAt(LiveTraffic.Count - 1);
    });

    private void ApplyLogFilters()
    {
        if (LogsPaused) return;
        VisibleLogs.Clear();
        foreach (var entry in _logs.Snapshot().Select(item => new LogEntryViewModel(item)).Where(MatchesLogFilters))
            VisibleLogs.Add(entry);
    }

    private bool MatchesLogFilters(LogEntryViewModel entry)
    {
        var directionVisible = entry.Entry.Direction == MdbDirection.Tx ? ShowTx : ShowRx;
        if (!directionVisible) return false;
        if (!ShowErrors && entry.IsError) return false;
        return string.IsNullOrWhiteSpace(LogSearch) || entry.Line.Contains(LogSearch, StringComparison.OrdinalIgnoreCase);
    }

    private void ClearLogs()
    {
        _logs.Clear();
        VisibleLogs.Clear();
        LiveTraffic.Clear();
        LogMessage = "Log view and session buffer cleared.";
    }

    private async Task ExportLogsAsync()
    {
        try
        {
            var result = await _logExport.ExportSessionAsync(_logs.Snapshot());
            LogMessage = $"Exported TXT and JSON to {_paths.Logs}";
        }
        catch (Exception exception) { LogMessage = FriendlyError(exception); }
    }

    private async Task CopyLogLineAsync()
    {
        if (SelectedLog is null) { LogMessage = "Select a log row first."; return; }
        await _clipboard.SetTextAsync(SelectedLog.Line);
        LogMessage = "Log line copied.";
    }

    private async Task CopyRawHexAsync()
    {
        if (SelectedLog is null) { LogMessage = "Select a log row first."; return; }
        await _clipboard.SetTextAsync(SelectedLog.RawHex);
        LogMessage = "Raw HEX copied.";
    }

    private void NewProfile()
    {
        var profile = new MdbProfile { Id = Guid.NewGuid().ToString("N"), Name = "New custom profile", BaseLevel = MdbFeatureLevel.Custom };
        Profiles.Add(profile);
        SelectedProfile = profile;
        ProfileMessage = "New custom profile ready to edit.";
    }

    private void DuplicateProfile()
    {
        if (SelectedProfile is null) return;
        var copy = SelectedProfile with { Id = Guid.NewGuid().ToString("N"), Name = SelectedProfile.Name + " Copy", IsBuiltIn = false };
        Profiles.Add(copy);
        SelectedProfile = copy;
        ProfileMessage = "Profile duplicated as a custom profile.";
    }

    private void DeleteProfile()
    {
        if (SelectedProfile is null) return;
        try
        {
            _profileRepository.DeleteCustom(SelectedProfile);
            Profiles.Remove(SelectedProfile);
            SelectedProfile = Profiles.FirstOrDefault();
            ProfileMessage = "Custom profile deleted.";
        }
        catch (Exception exception) { ProfileMessage = FriendlyError(exception); }
    }

    private async Task SaveProfileAsync()
    {
        try
        {
            var updated = ProfileEditor.ToProfile();
            if (updated.IsBuiltIn) throw new InvalidOperationException("Built-in profiles are read-only. Duplicate one to customize it.");
            await _profileRepository.SaveCustomAsync(updated);
            var index = SelectedProfile is null ? -1 : Profiles.IndexOf(SelectedProfile);
            if (index >= 0) Profiles[index] = updated; else Profiles.Add(updated);
            SelectedProfile = updated;
            ProfileMessage = "Custom profile saved.";
        }
        catch (Exception exception) { ProfileMessage = FriendlyError(exception); }
    }

    private async Task ImportProfileAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ProfileImportPath)) throw new InvalidOperationException("Enter the path of a profile JSON file.");
            var imported = await _profileRepository.ImportAsync(ProfileImportPath);
            Profiles.Add(imported);
            SelectedProfile = imported;
            ProfileMessage = "Profile imported as a custom profile.";
        }
        catch (Exception exception) { ProfileMessage = FriendlyError(exception); }
    }

    private async Task ExportProfileAsync()
    {
        try
        {
            if (SelectedProfile is null) throw new InvalidOperationException("Select a profile first.");
            var path = await _profileRepository.ExportAsync(SelectedProfile);
            ProfileMessage = $"Profile exported to {path}";
        }
        catch (Exception exception) { ProfileMessage = FriendlyError(exception); }
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
        PollingMode = SelectedPollingMode,
        TimeoutMilliseconds = Math.Clamp(TimeoutMilliseconds, 50, 120_000),
        WireFormat = SelectedWireFormat,
        AsciiHexTerminator = SelectedTerminator,
        LastProfileId = SelectedProfile?.Id ?? "standard-level1",
        WindowWidth = WindowWidth,
        WindowHeight = WindowHeight
    };

    private void RefreshSessionProperties()
    {
        RaisePropertyChanged(nameof(CurrentState));
        RaisePropertyChanged(nameof(PortDisplay));
        RaisePropertyChanged(nameof(TransportDisplay));
        RaisePropertyChanged(nameof(DetectedFeatureLevel));
    }

    private void RaisePageVisibility()
    {
        RaisePropertyChanged(nameof(IsDashboardVisible));
        RaisePropertyChanged(nameof(IsManualVisible));
        RaisePropertyChanged(nameof(IsAutomaticVisible));
        RaisePropertyChanged(nameof(IsProfilesVisible));
        RaisePropertyChanged(nameof(IsLogsVisible));
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
        _logs.EntryAdded -= OnLogAdded;
        _scenarioCancellation?.Cancel();
        _scenarioCancellation?.Dispose();
        await _session.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
