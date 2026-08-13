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

namespace MdbTestBench.App.ViewModels;

public sealed class WaferDiscoveryViewModel : ViewModelBase, IAsyncDisposable
{
    private const int MaximumVisibleEvents = 10_000;
    private readonly AppPaths _paths;
    private readonly Func<AppSettings> _settings;
    private readonly Func<bool> _workbenchConnected;
    private readonly WaferCaptureSerializer _serializer = new();
    private WaferDiscoveryCaptureController? _controller;
    private WaferCaptureArtifact? _artifact;
    private WaferCaptureDocument? _document;
    private string _status = "Ready — no port opens automatically.";
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
    private string _analysis = "No capture loaded.";

    public WaferDiscoveryViewModel(AppPaths paths, Func<AppSettings> settings, Func<bool> workbenchConnected)
    {
        _paths = paths; _settings = settings; _workbenchConnected = workbenchConnected;
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
    public string RawValidation { get { var parsed = HexParser.Parse(RawHex); return parsed.IsValid ? $"Valid · {parsed.Bytes.Length} byte(s)" : parsed.Error ?? "Invalid"; } }
    public string ProbeName { get => _probeName; set => SetProperty(ref _probeName, value); }
    public string ProbeNotes { get => _probeNotes; set => SetProperty(ref _probeNotes, value); }
    public WaferProbe? SelectedProbe { get => _selectedProbe; set => SetProperty(ref _selectedProbe, value); }
    public string CapturePath { get => _capturePath; set => SetProperty(ref _capturePath, value); }
    public string Search { get => _search; set { if (SetProperty(ref _search, value)) ApplyDocument(); } }
    public bool ShowTx { get => _showTx; set { if (SetProperty(ref _showTx, value)) ApplyDocument(); } }
    public bool ShowRx { get => _showRx; set { if (SetProperty(ref _showRx, value)) ApplyDocument(); } }
    public string DisplayMode { get => _displayMode; set { if (SetProperty(ref _displayMode, value)) ApplyDocument(); } }
    public bool IsCapturing { get => _isCapturing; private set => SetProperty(ref _isCapturing, value); }
    public string CaptureSize => $"{_captureSizeBytes / 1024d / 1024d:0.00} MB";
    public string Analysis { get => _analysis; private set => SetProperty(ref _analysis, value); }

    private async Task StartAsync()
    {
        try
        {
            if (IsCapturing) return;
            if (_workbenchConnected()) throw new InvalidOperationException("Disconnect the main workbench session before Discovery uses the transport.");
            await DisposeControllerAsync();
            DeleteArtifactSpool();
            var settings = _settings();
            if (settings.SelectedTransport == TransportKind.WaferMdbRs232 && string.IsNullOrWhiteSpace(settings.SerialPort))
                throw new InvalidOperationException("Select a serial port in Settings first.");
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
            Status = settings.SelectedTransport == TransportKind.Simulated
                ? "● Capturing — SIMULATION (no hardware)" : "● Capturing raw serial chunks";
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
            Status = _artifact.SizeLimitReached ? "Capture stopped at configured size limit." : "Capture stopped. Ready to export.";
            RaisePropertyChanged(nameof(CaptureSize));
        }
        catch (Exception exception) { IsCapturing = false; Status = Friendly(exception); }
    }

    private async Task AddMarkerAsync()
    {
        try
        {
            if (_controller is null || !IsCapturing) throw new InvalidOperationException("Start capture before adding a marker.");
            await _controller.AddMarkerAsync(MarkerText); MarkerText = string.Empty;
        }
        catch (Exception exception) { Status = Friendly(exception); }
    }

    private async Task SendRawAsync()
    {
        try
        {
            if (_controller is null || !IsCapturing) throw new InvalidOperationException("Start capture before transmitting.");
            if (!RawConfirmed) throw new InvalidOperationException("Confirm the exact adapter bytes before transmitting.");
            var parsed = HexParser.Parse(RawHex);
            if (!parsed.IsValid) throw new InvalidDataException(parsed.Error);
            var settings = _settings();
            await _controller.SendAsync(parsed.Bytes, new SerialWireFormatOptions
            { Format = settings.WireFormat, Terminator = settings.AsciiHexTerminator }, "RawAdapterManual");
            RawConfirmed = false; Status = "Exact on-wire TX bytes recorded in capture.";
        }
        catch (Exception exception) { Status = Friendly(exception); }
    }

    private void SaveProbe()
    {
        var parsed = HexParser.Parse(RawHex);
        if (string.IsNullOrWhiteSpace(ProbeName) || !parsed.IsValid) { Status = "Probe name and valid HEX bytes are required."; return; }
        var settings = _settings();
        var probe = new WaferProbe { Name = ProbeName.Trim(), Hex = parsed.NormalizedHex!, WireFormat = settings.WireFormat,
            Terminator = settings.AsciiHexTerminator, Notes = EmptyToNull(ProbeNotes) };
        Probes.Add(probe); SelectedProbe = probe; _controller?.AddProbe(probe);
        Status = "Probe saved; it will never execute automatically.";
    }

    private async Task SendProbeAsync()
    {
        if (SelectedProbe is null) { Status = "Select a saved probe first."; return; }
        RawHex = SelectedProbe.Hex; RawConfirmed = false;
        Status = "Probe loaded. Review it and check confirmation before Send Raw Adapter.";
        await Task.CompletedTask;
    }

    private async Task ExportAsync()
    {
        try
        {
            if (IsCapturing) await StopAsync();
            if (_artifact is null) throw new InvalidOperationException("Stop a new capture before exporting it.");
            var file = WaferCaptureSerializer.CreateSafeFileName(_artifact.Header.Adapter.PrintedRevision, _artifact.Header.Capture.StartedAtUtc);
            CapturePath = Path.Combine(_paths.Captures, file);
            await _serializer.ExportAsync(_artifact, CapturePath);
            Status = $"Exported privacy-safe raw evidence. Review before sharing: {CapturePath}";
        }
        catch (Exception exception) { Status = Friendly(exception); }
    }

    private async Task ExportSummaryAsync()
    {
        try
        {
            if (_document is null) throw new InvalidOperationException("Stop or open a capture first.");
            var path = Path.Combine(_paths.Captures, $"capture-{_document.CaptureId}.txt");
            await _serializer.ExportSummaryAsync(_document, path); Status = $"Human-readable summary exported: {path}";
        }
        catch (Exception exception) { Status = Friendly(exception); }
    }

    private async Task OpenAsync()
    {
        try
        {
            if (IsCapturing) throw new InvalidOperationException("Stop the active capture before opening a file.");
            if (string.IsNullOrWhiteSpace(CapturePath)) throw new InvalidOperationException("Enter a .mdbcap.json path.");
            var loaded = await _serializer.LoadAsync(CapturePath);
            var interpreter = new WaferCaptureInterpreter();
            var events = loaded.Events.Select(item => item.IsRaw && item.Direction is { } direction
                ? item with { PossibleMdbInterpretation = interpreter.Interpret(direction, item.GetRawBytes()) } : item).ToArray();
            var timing = loaded.Capture;
            _document = loaded with { Events = events, Statistics = new WaferCaptureAnalyzer().Analyze(events, timing) };
            ApplyDocument(); SetAnalysis(_document.Statistics);
            Status = $"Capture opened offline: {_document.CaptureId}. No bytes were retransmitted.";
        }
        catch (Exception exception) { Status = Friendly(exception); }
    }

    private void OnEvent(object? sender, WaferCaptureEvent item) => Dispatcher.UIThread.Post(() =>
    {
        if (Matches(item)) Events.Add(new(item, DisplayMode));
        while (Events.Count > MaximumVisibleEvents) Events.RemoveAt(0);
        _captureSizeBytes = _controller?.CaptureSizeBytes ?? _captureSizeBytes;
        RaisePropertyChanged(nameof(CaptureSize));
    });

    private void ApplyDocument()
    {
        if (_document is null) return;
        Events.Clear();
        foreach (var item in _document.Events.Where(Matches).TakeLast(MaximumVisibleEvents)) Events.Add(new(item, DisplayMode));
    }

    private bool Matches(WaferCaptureEvent item)
    {
        if (item.Direction == WaferCaptureDirection.Tx && !ShowTx || item.Direction == WaferCaptureDirection.Rx && !ShowRx) return false;
        return string.IsNullOrWhiteSpace(Search) || $"{item.Hex} {item.Text} {item.ErrorMessage} {item.Operation} {item.PossibleMdbInterpretation?.Description}"
            .Contains(Search, StringComparison.OrdinalIgnoreCase);
    }

    private void SetAnalysis(WaferCaptureStatistics stats) => Analysis =
        $"TX {stats.TxEvents} events / {stats.TxBytes} bytes · RX {stats.RxEvents} events / {stats.RxBytes} bytes · " +
        $"Errors {stats.Errors} · Markers {stats.Markers} · {stats.TrafficAppearance}\n" +
        $"RX lengths: {FormatCounts(stats.MostCommonRxLengths)} · prefixes: {FormatCounts(stats.RepeatedPrefixes)} · suffixes: {FormatCounts(stats.RepeatedSuffixes)}\n" +
        $"Possible delimiters: CR={stats.PossibleCrDelimiter}, LF={stats.PossibleLfDelimiter}, CRLF={stats.PossibleCrLfDelimiter} · " +
        $"possible MDB {stats.PossibleMdbResponses}, unknown {stats.UnknownRawEvents}\n" +
        (stats.PeriodicRxObservation.Detected
            ? $"Periodic RX activity observed: median {stats.PeriodicRxObservation.MedianIntervalMilliseconds:0.###} ms. Possible adapter-managed behavior; observation only."
            : "No stable periodic RX activity observed. This does not determine POLL ownership.");

    private static string FormatCounts(IReadOnlyDictionary<string, long> values) => values.Count == 0
        ? "none" : string.Join(", ", values.Select(item => $"{item.Key}×{item.Value}"));

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
    private static string Friendly(Exception exception) => exception switch
    {
        InvalidDataException or InvalidOperationException or NotSupportedException => exception.Message,
        UnauthorizedAccessException => "Permission denied while opening the selected serial port.",
        IOException => "Serial or capture file I/O failed. Check the adapter, permissions and path.",
        _ => $"Discovery error: {exception.Message}"
    };

    public async ValueTask DisposeAsync()
    {
        await DisposeControllerAsync();
        DeleteArtifactSpool();
        GC.SuppressFinalize(this);
    }
}

public sealed class WaferCaptureEventViewModel
{
    public WaferCaptureEventViewModel(WaferCaptureEvent item, string displayMode) { Event = item; Display = Format(item, displayMode); }
    public WaferCaptureEvent Event { get; }
    public string Timestamp => Event.TimestampUtc.ToString("HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
    public string Direction => Event.Direction?.ToString().ToUpperInvariant() ?? Event.Type.ToString().ToUpperInvariant();
    public string Display { get; }
    public string Details => Event.IsRaw
        ? $"LEN {Event.Length} · Δ {Event.DeltaMicroseconds:0.###} µs · chunk {Event.ReadChunkIndex?.ToString(CultureInfo.InvariantCulture) ?? "—"}"
        : Event.Text ?? Event.ErrorMessage ?? Event.Operation;
    public string Interpretation => Event.PossibleMdbInterpretation is null ? string.Empty
        : $"Possible MDB: {Event.PossibleMdbInterpretation.Description} · {Event.PossibleMdbInterpretation.Confidence}";

    private static string Format(WaferCaptureEvent item, string mode)
    {
        if (!item.IsRaw) return item.Text ?? item.ErrorMessage ?? item.Operation;
        var bytes = item.GetRawBytes();
        var ascii = new string(bytes.Select(value => value is >= 0x20 and <= 0x7E ? (char)value : '.').ToArray());
        return mode switch { "ASCII" => ascii, "HEX" => item.Hex ?? string.Empty, _ => $"{item.Hex}    {ascii}" };
    }
}
