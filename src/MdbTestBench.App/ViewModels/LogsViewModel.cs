using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using MdbTestBench.App.Services;
using MdbTestBench.Core.Logging;
using MdbTestBench.Core.Protocol;

namespace MdbTestBench.App.ViewModels;

public sealed class LogsViewModel : ViewModelBase, IDisposable
{
    private readonly InMemoryMdbLogSink _logs;
    private readonly LogExportService _export;
    private readonly ClipboardService _clipboard = new();
    private bool _paused;
    private bool _autoScroll = true;
    private bool _showTx = true;
    private bool _showRx = true;
    private bool _showErrors = true;
    private string _search = string.Empty;
    private LogEntryViewModel? _selected;
    private string _message = "Application and MDB traffic logging ready. Raw adapter evidence is stored separately in Capture.";

    public LogsViewModel(InMemoryMdbLogSink logs, LogExportService export)
    {
        _logs = logs; _export = export;
        ClearCommand = new RelayCommand(_ => Clear());
        ExportCommand = new AsyncRelayCommand(_ => ExportAsync());
        CopyLineCommand = new AsyncRelayCommand(_ => CopyLineAsync());
        CopyRawHexCommand = new AsyncRelayCommand(_ => CopyRawHexAsync());
        _logs.EntryAdded += OnLogAdded;
    }

    public ObservableCollection<LogEntryViewModel> Visible { get; } = [];
    public ObservableCollection<LogEntryViewModel> LiveTraffic { get; } = [];
    public ICommand ClearCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand CopyLineCommand { get; }
    public ICommand CopyRawHexCommand { get; }
    public bool Paused { get => _paused; set { if (SetProperty(ref _paused, value) && !value) ApplyFilters(); } }
    public bool AutoScroll { get => _autoScroll; set => SetProperty(ref _autoScroll, value); }
    public bool ShowTx { get => _showTx; set { if (SetProperty(ref _showTx, value)) ApplyFilters(); } }
    public bool ShowRx { get => _showRx; set { if (SetProperty(ref _showRx, value)) ApplyFilters(); } }
    public bool ShowErrors { get => _showErrors; set { if (SetProperty(ref _showErrors, value)) ApplyFilters(); } }
    public string Search { get => _search; set { if (SetProperty(ref _search, value)) ApplyFilters(); } }
    public LogEntryViewModel? Selected { get => _selected; set => SetProperty(ref _selected, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    private void OnLogAdded(object? sender, MdbLogEntry entry) => Dispatcher.UIThread.Post(() =>
    {
        var model = new LogEntryViewModel(entry);
        if (!Paused && Matches(model)) Visible.Add(model);
        LiveTraffic.Insert(0, model);
        while (LiveTraffic.Count > 8) LiveTraffic.RemoveAt(LiveTraffic.Count - 1);
    });

    private void ApplyFilters()
    {
        if (Paused) return;
        Visible.Clear();
        foreach (var entry in _logs.Snapshot().Select(item => new LogEntryViewModel(item)).Where(Matches)) Visible.Add(entry);
    }

    private bool Matches(LogEntryViewModel entry)
    {
        var visible = entry.Entry.Direction == MdbDirection.Tx ? ShowTx : ShowRx;
        return visible && (ShowErrors || !entry.IsError) &&
            (string.IsNullOrWhiteSpace(Search) || entry.Line.Contains(Search, StringComparison.OrdinalIgnoreCase));
    }

    private void Clear()
    {
        _logs.Clear(); Visible.Clear(); LiveTraffic.Clear();
        Message = "Application/MDB log cleared. Raw Adapter Capture files are unaffected.";
    }

    private async Task ExportAsync()
    {
        try { await _export.ExportSessionAsync(_logs.Snapshot()); Message = "Application/MDB TXT and JSON logs exported."; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { Message = exception.Message; }
    }

    private async Task CopyLineAsync()
    {
        if (Selected is null) { Message = "Select a log row first."; return; }
        await _clipboard.SetTextAsync(Selected.Line); Message = "Log line copied.";
    }

    private async Task CopyRawHexAsync()
    {
        if (Selected is null) { Message = "Select a log row first."; return; }
        await _clipboard.SetTextAsync(Selected.RawHex); Message = "MDB/log raw HEX copied.";
    }

    public void Dispose()
    {
        _logs.EntryAdded -= OnLogAdded;
        GC.SuppressFinalize(this);
    }
}
