using MdbTestBench.TestEngine;
using MdbTestBench.Transport.Simulation;
using MdbTestBench.TestEngine.Models;

namespace MdbTestBench.TestEngine.Tests;

public sealed class ScenarioCatalogTests
{
    [Fact]
    public void CatalogContainsAllV01SimulatorScenarios()
    {
        var scenarios = ScenarioCatalog.CreateBuiltIn();
        Assert.Equal(7, scenarios.Count);
        Assert.Contains(scenarios, scenario => scenario.Name == "L1 - Approved Vend");
        Assert.Contains(scenarios, scenario => scenario.SimulatorBehavior == SimulatorBehavior.Timeout);
        Assert.All(scenarios, scenario => Assert.NotEmpty(scenario.Steps));
    }

    [Fact]
    public async Task EveryCatalogScenarioProducesItsDesignedOutcome()
    {
        foreach (var scenario in ScenarioCatalog.CreateBuiltIn())
        {
            await using var simulator = new SimulatedCashlessTransport(new SimulatedCashlessOptions
            {
                Behavior = scenario.SimulatorBehavior,
                OperationTimeout = scenario.SimulatorBehavior == SimulatorBehavior.Timeout
                    ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(1)
            });
            var result = await new ScenarioRunner(simulator).RunAsync(scenario);

            if (scenario.SimulatorBehavior == SimulatorBehavior.Timeout)
                Assert.Equal(TestRunStatus.TimedOut, result.Status);
            else if (scenario.SimulatorBehavior == SimulatorBehavior.UnexpectedResponse)
                Assert.Equal(TestRunStatus.Failed, result.Status);
            else
                Assert.Equal(TestRunStatus.Passed, result.Status);
        }
    }
}
