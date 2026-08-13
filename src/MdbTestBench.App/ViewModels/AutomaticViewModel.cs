using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using MdbTestBench.Core.Logging;
using MdbTestBench.TestEngine;
using MdbTestBench.TestEngine.Models;
using MdbTestBench.Transport.Simulation;

namespace MdbTestBench.App.ViewModels;

public sealed class AutomaticViewModel : ViewModelBase, IDisposable
{
    private readonly InMemoryMdbLogSink _logs;
    private readonly Func<bool> _isSimulationSelected;
    private ScenarioDisplayViewModel? _selected;
    private bool _isRunning;
    private int _total;
    private int _passes;
    private int _failures;
    private string _summary = "Select a scenario and run it in Simulator mode.";
    private CancellationTokenSource? _cancellation;

    public AutomaticViewModel(InMemoryMdbLogSink logs, Func<bool> isSimulationSelected)
    {
        _logs = logs; _isSimulationSelected = isSimulationSelected;
        foreach (var scenario in ScenarioCatalog.CreateBuiltIn()) Scenarios.Add(new ScenarioDisplayViewModel(scenario));
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
            Summary = "Select a scenario and run it in Simulator mode.";
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
        if (!_isSimulationSelected()) { Summary = "Automatic runs only in Simulator until the Wafer codec is validated."; return; }
        IsRunning = true; Passes = 0; Failures = 0;
        foreach (var step in Selected.Steps) { step.Status = "PENDING"; step.Received = "—"; }
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
            var display = Selected.Steps[index++]; display.Status = result.Passed ? "PASS" : "FAIL";
            display.Received = result.ActualResponse?.ToString() ?? result.Error ?? "No response";
        });
        try
        {
            var result = await runner.RunAsync(Selected.Scenario, _cancellation.Token);
            Passes = result.Steps.Count(step => step.Passed); Failures = result.Steps.Count(step => !step.Passed);
            var effectivePass = result.Status == TestRunStatus.Passed ||
                (Selected.Scenario.SimulatorBehavior == SimulatorBehavior.Timeout && result.Status == TestRunStatus.TimedOut) ||
                (Selected.Scenario.SimulatorBehavior == SimulatorBehavior.UnexpectedResponse && result.Status == TestRunStatus.Failed);
            Selected.Result = result.Status == TestRunStatus.Aborted ? "ABORTED" : effectivePass ? "PASS" : "FAIL";
            Selected.Duration = $"{result.Duration.TotalMilliseconds:0} ms";
            Summary = effectivePass ? $"PASS — {Passes} expected step responses; behavior handled as designed."
                : $"{Selected.Result} — {result.Error ?? "Review expected and received values."}";
        }
        finally
        {
            _cancellation.Dispose(); _cancellation = null; IsRunning = false;
        }
    }

    public void Dispose()
    {
        _cancellation?.Cancel(); _cancellation?.Dispose();
        GC.SuppressFinalize(this);
    }
}
