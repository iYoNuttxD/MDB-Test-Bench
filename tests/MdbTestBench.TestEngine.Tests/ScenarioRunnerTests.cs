using MdbTestBench.Core.Protocol;
using MdbTestBench.TestEngine.Models;
using MdbTestBench.Transport.Simulation;
using MdbTestBench.Transport.Abstractions;
using MdbTestBench.Core.Protocol.Frames;

namespace MdbTestBench.TestEngine.Tests;

public sealed class ScenarioRunnerTests
{
    [Fact]
    public async Task ValidScenario_Passes()
    {
        await using var transport = new SimulatedCashlessTransport();
        var runner = new ScenarioRunner(transport);

        var result = await runner.RunAsync(CreateBasicScenario());

        Assert.Equal(TestRunStatus.Passed, result.Status);
        Assert.All(result.Steps, step => Assert.True(step.Passed));
    }

    [Fact]
    public async Task InvalidExpectedResponse_FailsFast()
    {
        await using var transport = new SimulatedCashlessTransport();
        var runner = new ScenarioRunner(transport);
        var scenario = new TestScenario
        {
            Id = "invalid",
            Name = "Invalid expectation",
            Steps = [new TestStep { Name = "Reset", Command = MdbCommandType.Reset, ExpectedResponse = MdbResponseType.Ack }]
        };

        var result = await runner.RunAsync(scenario);

        Assert.Equal(TestRunStatus.Failed, result.Status);
        Assert.False(result.Steps[0].Passed);
    }

    [Fact]
    public async Task Scenario_ReportsCancellation()
    {
        await using var transport = new SimulatedCashlessTransport(
            new SimulatedCashlessOptions { ResponseDelay = TimeSpan.FromSeconds(1) });
        var runner = new ScenarioRunner(transport);
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        var result = await runner.RunAsync(CreateBasicScenario(), source.Token);

        Assert.Equal(TestRunStatus.Aborted, result.Status);
    }

    [Fact]
    public async Task TransportFailureBecomesControlledFailedResult()
    {
        await using var transport = new FailingTransport();

        var result = await new ScenarioRunner(transport).RunAsync(CreateBasicScenario());

        Assert.Equal(TestRunStatus.Failed, result.Status);
        Assert.Contains("serial read failed", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static TestScenario CreateBasicScenario() => new()
    {
        Id = "basic-approved-vend",
        Name = "Basic approved vend",
        Steps =
        [
            new() { Name = "Reset", Command = MdbCommandType.Reset, ExpectedResponse = MdbResponseType.JustReset },
            new() { Name = "Setup", Command = MdbCommandType.Setup, ExpectedResponse = MdbResponseType.ReaderConfigData },
            new() { Name = "Enable", Command = MdbCommandType.Reader, Subcommand = MdbSubcommandType.Enable, ExpectedResponse = MdbResponseType.Ack },
            new() { Name = "Begin session", Command = MdbCommandType.Poll, ExpectedResponse = MdbResponseType.BeginSession },
            new() { Name = "Vend request", Command = MdbCommandType.Vend, Subcommand = MdbSubcommandType.VendRequest, ExpectedResponse = MdbResponseType.VendApproved },
            new() { Name = "Vend success", Command = MdbCommandType.Vend, Subcommand = MdbSubcommandType.VendSuccess, ExpectedResponse = MdbResponseType.Ack },
            new() { Name = "Session complete", Command = MdbCommandType.Vend, Subcommand = MdbSubcommandType.SessionComplete, ExpectedResponse = MdbResponseType.EndSession }
        ]
    };

    private sealed class FailingTransport : IMdbTransport
    {
        public bool IsConnected { get; private set; }
        public TransportCapabilities Capabilities { get; } = new()
        {
            Name = "failing test transport",
            PollingMode = PollingMode.HostManaged
        };
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }
        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            return Task.CompletedTask;
        }
        public Task<MdbFrame> ExchangeAsync(MdbFrame request, CancellationToken cancellationToken = default) =>
            throw new TransportException(TransportError.ReadFailure, "Serial read failed safely.");
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
