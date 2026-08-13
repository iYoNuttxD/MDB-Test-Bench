using MdbTestBench.App.Services;
using MdbTestBench.App.ViewModels;
using MdbTestBench.Core.Logging;
using MdbTestBench.Core.Protocol;

namespace MdbTestBench.App.Tests;

public sealed class ManualViewModelTests
{
    [Fact]
    public async Task HardwareModeShowsMdbPreviewButDisablesBothSendPaths()
    {
        await using var session = new WorkbenchSession(new InMemoryMdbLogSink());
        var viewModel = new ManualViewModel(session, () => true, () => true,
            () => MdbFeatureLevel.Level1, () => { });

        Assert.Contains("MDB:", viewModel.Preview, StringComparison.Ordinal);
        Assert.False(viewModel.CanUseStructured);
        Assert.False(viewModel.CanUseRaw);
    }
}
