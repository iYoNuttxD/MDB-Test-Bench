using MdbTestBench.Core.Logging;
using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;
using MdbTestBench.Core.Vmc;
using MdbTestBench.TestEngine.Models;
using MdbTestBench.Transport.Simulation;

namespace MdbTestBench.TestEngine.Tests;

public sealed class SimulatedEndToEndTests
{
    [Theory]
    [InlineData("l1-initialization")]
    [InlineData("l1-approved-vend")]
    [InlineData("l1-denied-vend")]
    [InlineData("l1-cancelled-vend")]
    [InlineData("l1-session-complete")]
    public async Task CoreSimulatorTransportAndEngineCompleteNominalFlows(string scenarioId)
    {
        var scenario = ScenarioCatalog.CreateBuiltIn().Single(item => item.Id == scenarioId);
        var (result, state, logs) = await RunAsync(scenario, scenario.SimulatorBehavior);

        Assert.Equal(TestRunStatus.Passed, result.Status);
        Assert.Equal(VmcState.Enabled, state);
        Assert.NotEmpty(logs.Snapshot());
        Assert.All(logs.Snapshot().Where(entry => entry.Direction == MdbDirection.Tx),
            entry => Assert.False(entry.RawData.IsEmpty));
    }

    [Fact]
    public async Task TimeoutIsReportedWithoutEscapingTheEngine()
    {
        var scenario = ScenarioCatalog.CreateBuiltIn().Single(item => item.Id == "timeout-handling");
        var (result, state, _) = await RunAsync(scenario, SimulatorBehavior.Timeout,
            operationTimeout: TimeSpan.FromMilliseconds(40));

        Assert.Equal(TestRunStatus.TimedOut, result.Status);
        Assert.Equal(VmcState.Connected, state);
    }

    [Fact]
    public async Task MalformedResponseIsAControlledFailure()
    {
        var scenario = ResetScenario("malformed");
        var (result, state, _) = await RunAsync(scenario, SimulatorBehavior.MalformedResponse);

        Assert.Equal(TestRunStatus.Failed, result.Status);
        Assert.Equal(MdbResponseType.Unknown, result.Steps[0].ActualResponse);
        Assert.Equal(VmcState.Reset, state);
    }

    [Fact]
    public async Task UnexpectedResponseIsAControlledFailure()
    {
        var scenario = ScenarioCatalog.CreateBuiltIn().Single(item => item.Id == "unexpected-response");
        var (result, state, _) = await RunAsync(scenario, SimulatorBehavior.UnexpectedResponse);

        Assert.Equal(TestRunStatus.Failed, result.Status);
        Assert.Equal(MdbResponseType.Unknown, result.Steps[0].ActualResponse);
        Assert.Equal(VmcState.Connected, state);
    }

    [Fact]
    public async Task CallerCancellationAbortsTheIntegratedRun()
    {
        var vmc = new VmcSimulator();
        await using var transport = new SimulatedCashlessTransport(new SimulatedCashlessOptions
        {
            ResponseDelay = TimeSpan.FromSeconds(2),
            OperationTimeout = TimeSpan.FromSeconds(5)
        }, vmcSimulator: vmc);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        var result = await new ScenarioRunner(transport).RunAsync(ResetScenario("cancel"), cancellation.Token);

        Assert.Equal(TestRunStatus.Aborted, result.Status);
        Assert.Equal(VmcState.Connected, vmc.State);
    }

    [Fact]
    public async Task ApprovedVendUsesRealEncoderAndDecoderInThePrimaryScenarioPath()
    {
        var encoder = new CountingEncoder();
        var decoder = new CountingDecoder();
        var scenario = ScenarioCatalog.CreateBuiltIn().Single(item => item.Id == "l1-approved-vend");
        var vmc = new VmcSimulator(new VmcStateMachine());
        var logs = new InMemoryMdbLogSink();
        await using var transport = new SimulatedCashlessTransport(new SimulatedCashlessOptions
        {
            Behavior = SimulatorBehavior.AlwaysApprove,
            ResponseDelay = TimeSpan.Zero
        }, vmcSimulator: vmc, decoder: decoder);

        var result = await new ScenarioRunner(transport, logs, encoder).RunAsync(scenario);

        Assert.Equal(TestRunStatus.Passed, result.Status);
        Assert.Equal(result.Steps.Count, encoder.EncodeCount);
        Assert.Equal(result.Steps.Count, decoder.CommandCount);
        Assert.Equal(result.Steps.Count, decoder.ResponseCount);
        Assert.Equal(VmcState.Enabled, vmc.State);
        Assert.Contains(logs.Snapshot(), entry => entry.Direction == MdbDirection.Tx && !entry.RawData.IsEmpty);
        Assert.Contains(logs.Snapshot(), entry => entry.Direction == MdbDirection.Rx && !entry.RawData.IsEmpty);
    }

    private static async Task<(TestRunResult Result, VmcState State, InMemoryMdbLogSink Logs)> RunAsync(
        TestScenario scenario,
        SimulatorBehavior behavior,
        TimeSpan? operationTimeout = null)
    {
        var vmc = new VmcSimulator(new VmcStateMachine());
        var logs = new InMemoryMdbLogSink();
        await using var transport = new SimulatedCashlessTransport(new SimulatedCashlessOptions
        {
            Behavior = behavior,
            ResponseDelay = TimeSpan.FromMilliseconds(1),
            OperationTimeout = operationTimeout ?? TimeSpan.FromSeconds(1)
        }, vmcSimulator: vmc);
        Assert.Same(vmc, transport.Vmc);

        var result = await new ScenarioRunner(transport, logs).RunAsync(scenario);
        return (result, vmc.State, logs);
    }

    private static TestScenario ResetScenario(string id) => new()
    {
        Id = id,
        Name = id,
        Timeout = TimeSpan.FromSeconds(2),
        Steps =
        [
            new TestStep
            {
                Name = "RESET",
                Command = MdbCommandType.Reset,
                ExpectedResponse = MdbResponseType.Ack
            }
        ]
    };

    private sealed class CountingEncoder : IMdbCashlessEncoder
    {
        private readonly MdbCashlessEncoder _inner = new();
        public int EncodeCount { get; private set; }
        public ReadOnlyMemory<byte> Encode(MdbCashlessCommand command)
        {
            EncodeCount++;
            return _inner.Encode(command);
        }
    }

    private sealed class CountingDecoder : IMdbCashlessDecoder
    {
        private readonly MdbCashlessDecoder _inner = new();
        public int CommandCount { get; private set; }
        public int ResponseCount { get; private set; }
        public MdbDecodedCommand DecodeCommand(ReadOnlySpan<byte> block)
        {
            CommandCount++;
            return _inner.DecodeCommand(block);
        }
        public MdbCashlessResponse DecodeResponse(ReadOnlySpan<byte> block, MdbCashlessDecodeOptions? options = null)
        {
            ResponseCount++;
            return _inner.DecodeResponse(block, options);
        }
    }
}
