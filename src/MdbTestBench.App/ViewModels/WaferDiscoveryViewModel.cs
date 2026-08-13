using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;
using Avalonia.Threading;
using MdbTestBench.App.Services;
using MdbTestBench.Core.Protocol.Encoding;
using MdbTestBench.Transport.Capture;
using MdbTestBench.Transport.Configuration;
using MdbTestBench.Transport.Serial;
using MdbTestBench.Transport.Simulation;
using MdbTestBench.App.Localization;

namespace MdbTestBench.App.ViewModels;

public sealed class WaferDiscoveryViewModel : ViewModelBase, IAsyncDisposable
{
    private const int MaximumVisibleEvents = 10_000;
    private readonly AppPaths _paths;
    private readonly Func<AppSettings> _settings;
    private readonly Func<bool> _workbenchConnected;
    private readonly ILocalizationService _localization;
    private readonly WaferCaptureSerializer _serializer = new();
    private WaferDiscoveryCaptureController? _controller;
    private WaferCaptureArtifact? _artifact;
    private WaferCaptureDocument? _document;
    private string _status;
    private string _adapterModel = "MDB-RS232 PC Adapter";
    private string _printedRevision = "2022061K5";
    private string _notes = string.Empty;
    private string _markerText = string.Empty;
    private string _rawHex = string.Empty;
    private bool _rawConfirmed;
    private string _probeName = string.Empty;
    private string _probeNotes = string.Empty;
    private WaferProbe? _selectedProbe;
    private string _capturePath = string.Empty;
    private string _search = string.Empty;
    private bool _showTx = true;
    private bool _showRx = true;
    private string _displayMode = "HEX + ASCII";
    private bool _isCapturing;
    private long _captureSizeBytes;
    private string _analysis;
    private string _operationMode;

    public WaferDiscoveryViewModel(AppPaths paths, Func<AppSettings> settings, Func<bool> workbenchConnected,
        ILocalizationService? localization = null)
    {
        _paths = paths; _settings = settings; _workbenchConnected = workbenchConnected;
        _localization = localization ?? new LocalizationService();
        _status = _localization.Get("DiscoveryReady"); _analysis = _localization.Get("NoCaptureLoaded");
        _operationMode = _localization.Get("DiscoveryReadyMode");
        StartCommand = new AsyncRelayCommand(_ => StartAsync());
        StopCommand = new AsyncRelayCommand(_ => StopAsync());
        AddMarkerCommand = new AsyncRelayCommand(_ => AddMarkerAsync());
        SendRawCommand = new AsyncRelayCommand(_ => SendRawAsync());
        SaveProbeCommand = new RelayCommand(_ => SaveProbe());
        SendProbeCommand = new AsyncRelayCommand(_ => SendProbeAsync());
        ExportCommand = new AsyncRelayCommand(_ => ExportAsync());
        ExportSummaryCommand = new AsyncRelayCommand(_ => ExportSummaryAsync());
        OpenCommand = new AsyncRelayCommand(_ => OpenAsync());
    }

    public ObservableCollection<WaferCaptureEventViewModel> Events { get; } = [];
    public ObservableCollection<WaferProbe> Probes { get; } = [];
    public IReadOnlyList<string> DisplayModes { get; } = ["HEX", "ASCII", "HEX + ASCII"];
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand AddMarkerCommand { get; }
    public ICommand SendRawCommand { get; }
    public ICommand SaveProbeCommand { get; }
    public ICommand SendProbeCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ExportSummaryCommand { get; }
    public ICommand OpenCommand { get; }

    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string AdapterModel { get => _adapterModel; set => SetProperty(ref _adapterModel, value); }
    public string PrintedRevision { get => _printedRevision; set => SetProperty(ref _printedRevision, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public string MarkerText { get => _markerText; set => SetProperty(ref _markerText, value); }
    public string RawHex { get => _rawHex; set { if (SetProperty(ref _rawHex, value)) { RawConfirmed = false; RaisePropertyChanged(nameof(RawValidation)); } } }
    public bool RawConfirmed { get => _rawConfirmed; set => SetProperty(ref _rawConfirmed, value); }
    public string RawValidation
    {
        get
        {
            var parsed = HexParser.Parse(RawHex);
            return parsed.IsValid
                ? _localization.Format("ValidLogicalBytes", parsed.Bytes.Length, RawAdapterConfiguration)
                : _localization.Get("InvalidHex");
        }
    }
    public string ProbeName { get => _probeName; set => SetProperty(ref _probeName, value); }
    public string ProbeNotes { get => _probeNotes; set => SetProperty(ref _probeNotes, value); }
    public WaferProbe? SelectedProbe { get => _selectedProbe; set => SetProperty(ref _selectedProbe, value); }
    public string CapturePath { get => _capturePath; set => SetProperty(ref _capturePath, value); }
    public string Search { get => _search; set { if (SetProperty(ref _search, value)) ApplyDocument(); } }
    public bool ShowTx { get => _showTx; set { if (SetProperty(ref _showTx, value)) ApplyDocument(); } }
    public bool ShowRx { get => _showRx; set { if (SetProperty(ref _showRx, value)) ApplyDocument(); } }
    public string DisplayMode { get => _displayMode; set { if (SetProperty(ref _displayMode, value)) ApplyDocument(); } }
    public bool IsCapturing { get => _isCapturing; private set => SetProperty(ref _isCapturing, value); }
    public string CaptureSize => string.Format(_localization.CurrentCulture, "{0:N2} MB", _captureSizeBytes / 1024d / 1024d);
    public string Analysis { get => _analysis; private set => SetProperty(ref _analysis, value); }
    public string OperationMode { get => _operationMode; private set => SetProperty(ref _operationMode, value); }
    public string RawAdapterConfiguration
    {
        get
        {
            var settings = _settings();
            var port = settings.SelectedTransport == TransportKind.Simulated ? _localization.Get("VirtualSimulator") :
                string.IsNullOrWhiteSpace(settings.SerialPort) ? _localization.Get("NoPortSelected") : settings.SerialPort;
            return _localization.Format("RawAdapterConfiguration", port, settings.WireFormat, settings.AsciiHexTerminator);
        }
    }

    private async Task StartAsync()
    {
        try
        {
            if (IsCapturing) return;
            if (_workbenchConnected()) throw new InvalidOperationException(_localization.Get("DiscoveryDisconnectWorkbench"));
            await DisposeControllerAsync();
            DeleteArtifactSpool();
            var settings = _settings();
            if (settings.SelectedTransport == TransportKind.WaferMdbRs232 && string.IsNullOrWhiteSpace(settings.SerialPort))
                throw new InvalidOperationException(_localization.Get("DiscoverySelectPort"));
            var now = DateTimeOffset.UtcNow;
            var header = new WaferCaptureDocument
            {
                CaptureId = Guid.NewGuid().ToString("N"),
                Application = new("MDB Test Bench", Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.1"),
                Adapter = new() { Model = EmptyToNull(AdapterModel), PrintedRevision = EmptyToNull(PrintedRevision) },
                Host = new(RuntimeInformation.OSDescription, Environment.OSVersion.VersionString,
                    RuntimeInformation.OSArchitecture.ToString(), Environment.Version.ToString()),
                Serial = new()
                {
                    Port = settings.SelectedTransport == TransportKind.Simulated ? null : settings.SerialPort,
                    BaudRate = settings.BaudRate, DataBits = settings.DataBits, Parity = settings.Parity.ToString(),
                    StopBits = settings.StopBits.ToString(), Handshake = settings.Handshake.ToString(),
                    ReadTimeoutMilliseconds = settings.TimeoutMilliseconds, WriteTimeoutMilliseconds = settings.TimeoutMilliseconds,
                    PollingMode = settings.PollingMode, WireFormat = settings.WireFormat, Terminator = settings.AsciiHexTerminator
                },
                Capture = new() { CreatedAtUtc = now, StartedAtUtc = now, MonotonicFrequency = Stopwatch.Frequency },
                UserNotes = EmptyToNull(Notes), PrivacySafe = true
            };
            var recorder = await WaferCaptureRecorder.StartAsync(header, _paths.CaptureTemp,
                Math.Clamp(settings.CaptureMaximumMegabytes, 1, 1024) * 1024L * 1024);
            foreach (var probe in Probes) recorder.AddProbe(probe);
            var transport = settings.SelectedTransport == TransportKind.Simulated
                ? new DiscoverySimulatorTransport() as MdbTestBench.Transport.Abstractions.IRawByteTransport
                : new SerialTransport(new SerialTransportSettings
                {
                    PortName = settings.SerialPort, BaudRate = settings.BaudRate, DataBits = settings.DataBits,
                    Parity = settings.Parity, StopBits = settings.StopBits, Handshake = settings.Handshake,
                    PollingMode = settings.PollingMode, OperationTimeout = TimeSpan.FromMilliseconds(settings.TimeoutMilliseconds)
                });
            _controller = new(transport, recorder);
            _controller.EventRecorded += OnEvent;
            Events.Clear(); _document = null; _artifact = null; _captureSizeBytes = 0;
            await _controller.StartAsync();
            IsCapturing = true;
            OperationMode = settings.SelectedTransport == TransportKind.Simulated ? _localization.Get("SimulatorCapture") : _localization.Get("HardwareCapture");
            RaisePropertyChanged(nameof(RawAdapterConfiguration));
            Status = settings.SelectedTransport == TransportKind.Simulated
                ? _localization.Get("CapturingSimulation") : _localization.Get("CapturingRawSerial");
        }
        catch (Exception exception) { Status = Friendly(exception); }
    }

    private async Task StopAsync()
    {
        if (_controller is null || !IsCapturing) return;
        try
        {
            _artifact = await _controller.StopAsync();
            _document = _artifact.Header with { Events = Events.Select(item => item.Event).ToArray() };
            _captureSizeBytes = _artifact.CaptureSizeBytes;
            IsCapturing = false;
            SetAnalysis(_document.Statistics);
            Status = _artifact.SizeLimitReached ? _localization.Get("CaptureStoppedSizeLimit") : _localization.Get("CaptureStoppedReady");
            RaisePropertyChanged(nameof(CaptureSize));
        }
        catch (Exception exception) { IsCapturing = false; Status = Friendly(exception); }
    }

    private async Task AddMarkerAsync()
    {
        try
        {
            if (_controller is null || !IsCapturing) throw new InvalidOperationException(_localization.Get("StartCaptureBeforeMarker"));
            await _controller.AddMarkerAsync(MarkerText); MarkerText = string.Empty;
        }
        catch (Exception exception) { Status = Friendly(exception); }
    }

    private async Task SendRawAsync()
    {
        try
        {
            if (_controller is null || !IsCapturing) throw new InvalidOperationException(_localization.Get("StartCaptureBeforeTransmit"));
            if (!RawConfirmed) throw new InvalidOperationException(_localization.Get("ConfirmAdapterBytesError"));
            var parsed = HexParser.Parse(RawHex);
            if (!parsed.IsValid) throw new InvalidDataException(parsed.Error);
            var settings = _settings();
            await _controller.SendAsync(parsed.Bytes, new SerialWireFormatOptions
            { Format = settings.WireFormat, Terminator = settings.AsciiHexTerminator }, "RawAdapterManual");
            RawConfirmed = false; Status = _localization.Get("TxRecorded");
        }
        catch (Exception exception) { Status = Friendly(exception); }
    }

    private void SaveProbe()
    {
        var parsed = HexParser.Parse(RawHex);
        if (string.IsNullOrWhiteSpace(ProbeName) || !parsed.IsValid) { Status = _localization.Get("ProbeNameHexRequired"); return; }
        var settings = _settings();
        var probe = new WaferProbe { Name = ProbeName.Trim(), Hex = parsed.NormalizedHex!, WireFormat = settings.WireFormat,
            Terminator = settings.AsciiHexTerminator, Notes = EmptyToNull(ProbeNotes) };
        Probes.Add(probe); SelectedProbe = probe; _controller?.AddProbe(probe);
        Status = _localization.Get("ProbeSaved");
    }

    private async Task SendProbeAsync()
    {
        if (SelectedProbe is null) { Status = _localization.Get("SelectProbeFirst"); return; }
        RawHex = SelectedProbe.Hex; RawConfirmed = false;
        Status = _localization.Get("ProbeLoaded");
        await Task.CompletedTask;
    }

    private async Task ExportAsync()
    {
        try
        {
            if (IsCapturing) await StopAsync();
            var header = _artifact?.Header ?? _document
                ?? throw new InvalidOperationException(_localization.Get("StopOrOpenBeforeExport"));
            var file = WaferCaptureSerializer.CreateSafeFileName(header.Adapter.PrintedRevision, header.Capture.StartedAtUtc);
            CapturePath = Path.Combine(_paths.Captures, file);
            if (_artifact is not null) await _serializer.ExportAsync(_artifact, CapturePath);
            else await _serializer.ExportDocumentAsync(header, CapturePath);
            Status = _localization.Format("CaptureExported", CapturePath);
        }
        catch (Exception exception) { Status = Friendly(exception); }
    }

    private async Task ExportSummaryAsync()
    {
        try
        {
            if (_document is null) throw new InvalidOperationException(_localization.Get("StopOrOpenFirst"));
            var path = Path.Combine(_paths.Captures, $"capture-{_document.CaptureId}.txt");
            await _serializer.ExportSummaryAsync(_document, path); Status = _localization.Format("SummaryExported", path);
        }
        catch (Exception exception) { Status = Friendly(exception); }
    }

    private async Task OpenAsync()
    {
        try
        {
            if (IsCapturing) throw new InvalidOperationException(_localization.Get("StopCaptureBeforeOpen"));
            if (string.IsNullOrWhiteSpace(CapturePath)) throw new InvalidOperationException(_localization.Get("EnterCapturePath"));
            var loaded = await _serializer.LoadAsync(CapturePath);
            var interpreter = new WaferCaptureInterpreter();
            var events = loaded.Events.Select(item => item.IsRaw && item.Direction is { } direction
                ? item with { PossibleMdbInterpretation = interpreter.Interpret(direction, item.GetRawBytes()) } : item).ToArray();
            var timing = loaded.Capture;
            await DisposeControllerAsync();
            DeleteArtifactSpool();
            _document = loaded with { Events = events, Statistics = new WaferCaptureAnalyzer().Analyze(events, timing) };
            Probes.Clear();
            foreach (var probe in _document.Probes) Probes.Add(probe);
            _captureSizeBytes = new FileInfo(CapturePath).Length;
            ApplyDocument(); SetAnalysis(_document.Statistics);
            RaisePropertyChanged(nameof(CaptureSize));
            OperationMode = _localization.Get("OfflineCaptureAnalysis");
            Status = _localization.Format("CaptureOpenedOffline", _document.CaptureId);
        }
        catch (Exception exception) { Status = Friendly(exception); }
    }

    private void OnEvent(object? sender, WaferCaptureEvent item) => Dispatcher.UIThread.Post(() =>
    {
        if (Matches(item)) Events.Add(new(item, DisplayMode, _localization));
        while (Events.Count > MaximumVisibleEvents) Events.RemoveAt(0);
        _captureSizeBytes = _controller?.CaptureSizeBytes ?? _captureSizeBytes;
        RaisePropertyChanged(nameof(CaptureSize));
    });

    private void ApplyDocument()
    {
        if (_document is null) return;
        Events.Clear();
        foreach (var item in _document.Events.Where(Matches).TakeLast(MaximumVisibleEvents)) Events.Add(new(item, DisplayMode, _localization));
    }

    private bool Matches(WaferCaptureEvent item)
    {
        if (item.Direction == WaferCaptureDirection.Tx && !ShowTx || item.Direction == WaferCaptureDirection.Rx && !ShowRx) return false;
        return string.IsNullOrWhiteSpace(Search) || $"{item.Hex} {item.Text} {item.ErrorMessage} {item.Operation} {item.PossibleMdbInterpretation?.Description}"
            .Contains(Search, StringComparison.OrdinalIgnoreCase);
    }

    private void SetAnalysis(WaferCaptureStatistics stats) => Analysis =
        _localization.Format("AnalysisTotals", stats.TxEvents, stats.TxBytes, stats.RxEvents, stats.RxBytes, stats.Errors, stats.Markers, LocalizeTrafficAppearance(stats.TrafficAppearance)) + "\n" +
        _localization.Format("AnalysisPatterns", FormatCounts(stats.MostCommonRxLengths), FormatCounts(stats.RepeatedPrefixes), FormatCounts(stats.RepeatedSuffixes)) + "\n" +
        _localization.Format("AnalysisDelimiters", stats.PossibleCrDelimiter, stats.PossibleLfDelimiter, stats.PossibleCrLfDelimiter, stats.PossibleMdbResponses, stats.UnknownRawEvents) + "\n" +
        (stats.PeriodicRxObservation.Detected
            ? _localization.Format("PeriodicActivity", stats.PeriodicRxObservation.MedianIntervalMilliseconds)
            : _localization.Get("NoPeriodicActivity"));

    private string LocalizeTrafficAppearance(string value) => value switch
    {
        "ASCII-looking" => _localization.Get("TrafficAsciiLooking"),
        "Binary-looking" => _localization.Get("TrafficBinaryLooking"),
        "Mixed" => _localization.Get("TrafficMixed"),
        _ => _localization.Get("TrafficUnknown")
    };

    private string FormatCounts(IReadOnlyDictionary<string, long> values) => values.Count == 0
        ? _localization.Get("NoneLower") : string.Join(", ", values.Select(item => $"{item.Key}×{item.Value}"));

    private async Task DisposeControllerAsync()
    {
        if (_controller is null) return;
        _controller.EventRecorded -= OnEvent;
        await _controller.DisposeAsync();
        _controller = null;
    }

    private void DeleteArtifactSpool()
    {
        if (_artifact is null) return;
        try
        {
            var fullPath = Path.GetFullPath(_artifact.EventSpoolPath);
            var root = Path.GetFullPath(_paths.CaptureTemp) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(root, StringComparison.Ordinal) && File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        _artifact = null;
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private string Friendly(Exception exception) => exception switch
    {
        InvalidOperationException => exception.Message,
        InvalidDataException or NotSupportedException => _localization.Get("InvalidCaptureData"),
        UnauthorizedAccessException => _localization.Get("DiscoveryPermissionDenied"),
        IOException => _localization.Get("DiscoveryIoError"),
        _ => _localization.Format("DiscoveryError", exception.Message)
    };

    public void RefreshLocalization()
    {
        if (IsCapturing)
        {
            var simulated = _settings().SelectedTransport == TransportKind.Simulated;
            OperationMode = simulated ? _localization.Get("SimulatorCapture") : _localization.Get("HardwareCapture");
            Status = simulated ? _localization.Get("CapturingSimulation") : _localization.Get("CapturingRawSerial");
        }
        else if (_document is null)
        {
            Status = _localization.Get("DiscoveryReady"); Analysis = _localization.Get("NoCaptureLoaded");
            OperationMode = _localization.Get("DiscoveryReadyMode");
        }
        else if (_artifact is null)
        {
            OperationMode = _localization.Get("OfflineCaptureAnalysis");
            Status = _localization.Format("CaptureOpenedOffline", _document.CaptureId);
        }
        else
        {
            var simulated = _settings().SelectedTransport == TransportKind.Simulated;
            OperationMode = simulated ? _localization.Get("SimulatorCapture") : _localization.Get("HardwareCapture");
            Status = _artifact.SizeLimitReached ? _localization.Get("CaptureStoppedSizeLimit") : _localization.Get("CaptureStoppedReady");
        }
        if (_document is not null) SetAnalysis(_document.Statistics);
        RaisePropertyChanged(nameof(RawValidation)); RaisePropertyChanged(nameof(RawAdapterConfiguration)); RaisePropertyChanged(nameof(CaptureSize));
        ApplyDocument();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeControllerAsync();
        DeleteArtifactSpool();
        GC.SuppressFinalize(this);
    }
}

public sealed class WaferCaptureEventViewModel
{
    private readonly ILocalizationService _localization;
    public WaferCaptureEventViewModel(WaferCaptureEvent item, string displayMode, ILocalizationService localization)
    { Event = item; Display = Format(item, displayMode); _localization = localization; }
    public WaferCaptureEvent Event { get; }
    public string Timestamp => Event.TimestampUtc.ToString("T", _localization.CurrentCulture) + Event.TimestampUtc.ToString(".ffffff", CultureInfo.InvariantCulture);
    public string Direction => Event.Direction?.ToString().ToUpperInvariant() ?? Event.Type.ToString().ToUpperInvariant();
    public string Display { get; }
    public string Details => Event.IsRaw
        ? _localization.Format("CaptureEventDetails", Event.Length, Event.DeltaMicroseconds, Event.ReadChunkIndex?.ToString(CultureInfo.InvariantCulture) ?? "—")
        : Event.TransportState switch
        {
            "SerialOpened" => _localization.Get("TransportStateSerialOpened"),
            "SerialClosed" => _localization.Get("TransportStateSerialClosed"),
            _ => Event.Text ?? Event.ErrorMessage ?? Event.Operation
        };
    public string Interpretation => Event.PossibleMdbInterpretation is null ? string.Empty
        : _localization.Format(
            "PossibleMdb",
            Event.PossibleMdbInterpretation.Confidence == MdbInterpretationConfidence.Unknown
                ? _localization.Get("UnknownMdbData")
                : Event.PossibleMdbInterpretation.Description,
            _localization.Get($"Interpretation{Event.PossibleMdbInterpretation.Confidence}"));

    private static string Format(WaferCaptureEvent item, string mode)
    {
        if (!item.IsRaw) return item.Text ?? item.ErrorMessage ?? item.Operation;
        var bytes = item.GetRawBytes();
        var ascii = new string(bytes.Select(value => value is >= 0x20 and <= 0x7E ? (char)value : '.').ToArray());
        return mode switch { "ASCII" => ascii, "HEX" => item.Hex ?? string.Empty, _ => $"{item.Hex}    {ascii}" };
    }
}
