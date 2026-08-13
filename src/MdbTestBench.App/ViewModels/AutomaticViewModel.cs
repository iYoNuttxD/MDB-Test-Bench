using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using MdbTestBench.Core.Logging;
using MdbTestBench.TestEngine;
using MdbTestBench.TestEngine.Models;
using MdbTestBench.Transport.Simulation;
using MdbTestBench.App.Localization;

namespace MdbTestBench.App.ViewModels;

public sealed class AutomaticViewModel : ViewModelBase, IDisposable
{
    private readonly InMemoryMdbLogSink _logs;
    private readonly Func<bool> _isSimulationSelected;
    private readonly ILocalizationService _localization;
    private ScenarioDisplayViewModel? _selected;
    private bool _isRunning;
    private int _total;
    private int _passes;
    private int _failures;
    private string _summary;
    private CancellationTokenSource? _cancellation;

    public AutomaticViewModel(InMemoryMdbLogSink logs, Func<bool> isSimulationSelected, ILocalizationService? localization = null)
    {
        _logs = logs; _isSimulationSelected = isSimulationSelected; _localization = localization ?? new LocalizationService();
        _summary = _localization.Get("AutomaticReady");
        foreach (var scenario in ScenarioCatalog.CreateBuiltIn()) Scenarios.Add(new ScenarioDisplayViewModel(scenario, _localization));
        Selected = Scenarios.FirstOrDefault();
        RunCommand = new AsyncRelayCommand(_ => RunAsync());
        CancelCommand = new RelayCommand(_ => _cancellation?.Cancel());
    }

    public ObservableCollection<ScenarioDisplayViewModel> Scenarios { get; } = [];
    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ScenarioDisplayViewModel? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            Total = value?.Steps.Count ?? 0;
            Passes = 0;
            Failures = 0;
            Summary = _localization.Get("AutomaticReady");
        }
    }
    public bool IsRunning { get => _isRunning; private set => SetProperty(ref _isRunning, value); }
    public int Total { get => _total; private set => SetProperty(ref _total, value); }
    public int Passes { get => _passes; private set => SetProperty(ref _passes, value); }
    public int Failures { get => _failures; private set => SetProperty(ref _failures, value); }
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }

    private async Task RunAsync()
    {
        if (Selected is null || IsRunning) return;
        if (!_isSimulationSelected()) { Summary = _localization.Get("AutomaticSimulatorOnly"); return; }
        IsRunning = true; Passes = 0; Failures = 0;
        foreach (var step in Selected.Steps) { step.Status = _localization.Get("Pending"); step.Received = "—"; }
        _cancellation = new CancellationTokenSource();
        await using var simulator = new SimulatedCashlessTransport(new SimulatedCashlessOptions
        {
            Behavior = Selected.Scenario.SimulatorBehavior,
            OperationTimeout = Selected.Scenario.SimulatorBehavior == SimulatorBehavior.Timeout
                ? TimeSpan.FromMilliseconds(200) : TimeSpan.FromSeconds(2)
        });
        var runner = new ScenarioRunner(simulator, _logs);
        var index = 0;
        runner.StepCompleted += (_, result) => Dispatcher.UIThread.Post(() =>
        {
            if (index >= Selected.Steps.Count) return;
            var display = Selected.Steps[index++]; display.Status = result.Passed ? _localization.Get("Pass") : _localization.Get("Fail");
            display.Received = result.ActualResponse?.ToString() ?? (result.Error is null ? _localization.Get("NoResponse") : _localization.Get("ControlledError"));
        });
        try
        {
            var result = await runner.RunAsync(Selected.Scenario, _cancellation.Token);
            Passes = result.Steps.Count(step => step.Passed); Failures = result.Steps.Count(step => !step.Passed);
            var effectivePass = result.Status == TestRunStatus.Passed ||
                (Selected.Scenario.SimulatorBehavior == SimulatorBehavior.Timeout && result.Status == TestRunStatus.TimedOut) ||
                (Selected.Scenario.SimulatorBehavior == SimulatorBehavior.UnexpectedResponse && result.Status == TestRunStatus.Failed);
            Selected.Result = result.Status == TestRunStatus.Aborted ? _localization.Get("Aborted") : effectivePass ? _localization.Get("Pass") : _localization.Get("Fail");
            Selected.Duration = string.Format(_localization.CurrentCulture, "{0:0} ms", result.Duration.TotalMilliseconds);
            Summary = effectivePass ? _localization.Format("AutomaticPassSummary", Passes)
                : _localization.Format("AutomaticFailSummary", Selected.Result);
        }
        finally
        {
            _cancellation.Dispose(); _cancellation = null; IsRunning = false;
        }
    }

    public void RefreshLocalization()
    {
        foreach (var scenario in Scenarios) scenario.RefreshLocalization();
        Summary = _localization.Get("AutomaticReady");
    }

    public void Dispose()
    {
        _cancellation?.Cancel(); _cancellation?.Dispose();
        GC.SuppressFinalize(this);
    }
}
