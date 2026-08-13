using MdbTestBench.App.ViewModels;
using MdbTestBench.Core.Logging;

namespace MdbTestBench.App.Tests;

public sealed class AutomaticViewModelTests
{
    [Fact]
    public void SelectingScenarioUpdatesSummaryTotalAndRaisesNotification()
    {
        using var viewModel = new AutomaticViewModel(new InMemoryMdbLogSink(), () => true);
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        viewModel.Selected = viewModel.Scenarios.Single(item => item.Name == "L1 - Approved Vend");

        Assert.Equal(8, viewModel.Total);
        Assert.Contains(nameof(AutomaticViewModel.Total), changes);
    }
}
