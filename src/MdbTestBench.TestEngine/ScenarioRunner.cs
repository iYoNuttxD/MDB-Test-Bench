using System.Diagnostics;
using MdbTestBench.Core.Logging;
using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Commands;
using MdbTestBench.Core.Protocol.Cashless;
using MdbTestBench.Core.Protocol.Frames;
using MdbTestBench.Core.Protocol.Encoding;
using MdbTestBench.TestEngine.Models;
using MdbTestBench.Transport.Abstractions;

namespace MdbTestBench.TestEngine;

public sealed class ScenarioRunner(
    IMdbTransport transport,
    IMdbLogSink? logSink = null,
    IMdbCashlessEncoder? encoder = null)
{
    private static readonly MdbAddress VmcAddress = MdbAddress.Vmc;
    private static readonly MdbAddress CashlessAddress = new(0x10, MdbDeviceType.CashlessDevice1);
    private readonly IMdbLogSink _logSink = logSink ?? new NullMdbLogSink();
    private readonly IMdbCashlessEncoder _encoder = encoder ?? new MdbCashlessEncoder();
    public event EventHandler<TestStepResult>? StepCompleted;

    public async Task<TestRunResult> RunAsync(
        TestScenario scenario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var runStopwatch = Stopwatch.StartNew();
        var results = new List<TestStepResult>();

        var validationErrors = TestScenarioValidator.Validate(scenario);
        if (validationErrors.Count > 0)
            return new TestRunResult(scenario.Id ?? string.Empty, TestRunStatus.Failed, results,
                runStopwatch.Elapsed, string.Join(" ", validationErrors));

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(scenario.Timeout);

        try
        {
            if (!transport.IsConnected) await transport.ConnectAsync(timeoutSource.Token);

            foreach (var step in scenario.Steps)
            {
                var stepStopwatch = Stopwatch.StartNew();
                var request = BuildRequest(step, scenario.RequiredProfile);

                await WriteLogAsync(request, "Scenario request", timeoutSource.Token);
                try
                {
                    var response = await transport.ExchangeAsync(request, timeoutSource.Token);
                    await WriteLogAsync(response, response.InterpretedPayload ?? "Scenario response", timeoutSource.Token);
                    var passed = response.Response == step.ExpectedResponse;
                    results.Add(new TestStepResult(step.Name, response.Response,
                        step.ExpectedResponse, passed, stepStopwatch.Elapsed,
                        passed ? null : $"Expected {step.ExpectedResponse}, received {response.Response}."));
                    StepCompleted?.Invoke(this, results[^1]);

                    if (!passed)
                        return new TestRunResult(scenario.Id, TestRunStatus.Failed, results,
                            runStopwatch.Elapsed, results[^1].Error);
                }
                catch (InvalidOperationException exception)
                {
                    results.Add(new TestStepResult(step.Name, null, step.ExpectedResponse, false,
                        stepStopwatch.Elapsed, exception.Message));
                    StepCompleted?.Invoke(this, results[^1]);
                    return new TestRunResult(scenario.Id, TestRunStatus.Failed, results,
                        runStopwatch.Elapsed, exception.Message);
                }
                catch (TimeoutException exception)
                {
                    results.Add(new TestStepResult(step.Name, null, step.ExpectedResponse, false,
                        stepStopwatch.Elapsed, exception.Message));
                    StepCompleted?.Invoke(this, results[^1]);
                    return new TestRunResult(scenario.Id, TestRunStatus.TimedOut, results,
                        runStopwatch.Elapsed, exception.Message);
                }
            }

            return new TestRunResult(scenario.Id, TestRunStatus.Passed, results, runStopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new TestRunResult(scenario.Id, TestRunStatus.Aborted, results,
                runStopwatch.Elapsed, "Scenario cancelled.");
        }
        catch (OperationCanceledException)
        {
            return new TestRunResult(scenario.Id, TestRunStatus.TimedOut, results,
                runStopwatch.Elapsed, $"Scenario exceeded {scenario.Timeout}.");
        }
        catch (TimeoutException exception)
        {
            return new TestRunResult(scenario.Id, TestRunStatus.TimedOut, results,
                runStopwatch.Elapsed, exception.Message);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            return new TestRunResult(scenario.Id, TestRunStatus.Failed, results,
                runStopwatch.Elapsed, exception.Message);
        }
        catch (TransportException exception)
        {
            return new TestRunResult(scenario.Id, TestRunStatus.Failed, results,
                runStopwatch.Elapsed, exception.Message);
        }
    }

    private async ValueTask WriteLogAsync(
        MdbFrame frame,
        string description,
        CancellationToken cancellationToken) =>
        await _logSink.WriteAsync(new MdbLogEntry(
            frame.Timestamp,
            frame.Direction,
            frame.Source.ToString(),
            frame.Destination.ToString(),
            frame.Subcommand == MdbSubcommandType.None
                ? frame.Command.ToString()
                : $"{frame.Command}/{frame.Subcommand}",
            description,
            frame.RawPayload,
            MdbLogSeverity.Information), cancellationToken);

    private static ReadOnlyMemory<byte> ParseHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return ReadOnlyMemory<byte>.Empty;
        var result = HexParser.Parse(value);
        if (!result.IsValid) throw new InvalidDataException(result.Error);
        return result.Bytes;
    }

    private MdbFrame BuildRequest(TestStep step, MdbFeatureLevel featureLevel)
    {
        if (!string.IsNullOrWhiteSpace(step.PayloadHex))
            return MdbFrame.CommandFrame(VmcAddress, CashlessAddress, step.Command, step.Subcommand,
                ParseHex(step.PayloadHex));

        var kind = (step.Command, step.Subcommand) switch
        {
            (MdbCommandType.Reset, _) => ManualCommandKind.Reset,
            (MdbCommandType.Poll, _) => ManualCommandKind.WaitSession,
            (MdbCommandType.Setup, MdbSubcommandType.SetupMaxMinPrices) => ManualCommandKind.SetupMaxMinPrices,
            (MdbCommandType.Setup, _) => ManualCommandKind.SetupConfig,
            (MdbCommandType.Reader, MdbSubcommandType.Enable) => ManualCommandKind.ReaderEnable,
            (MdbCommandType.Reader, MdbSubcommandType.Disable) => ManualCommandKind.ReaderDisable,
            (MdbCommandType.Reader, MdbSubcommandType.Cancel) => ManualCommandKind.ReaderCancel,
            (MdbCommandType.Vend, MdbSubcommandType.VendRequest) => ManualCommandKind.VendRequest,
            (MdbCommandType.Vend, MdbSubcommandType.VendCancel) => ManualCommandKind.VendCancel,
            (MdbCommandType.Vend, MdbSubcommandType.VendSuccess) => ManualCommandKind.VendSuccess,
            (MdbCommandType.Vend, MdbSubcommandType.VendFailure) => ManualCommandKind.VendFailure,
            (MdbCommandType.Vend, MdbSubcommandType.SessionComplete) => ManualCommandKind.SessionComplete,
            (MdbCommandType.Vend, MdbSubcommandType.CashSale) => ManualCommandKind.CashSale,
            (MdbCommandType.Revalue, MdbSubcommandType.RevalueRequest) => ManualCommandKind.RevalueRequest,
            (MdbCommandType.Revalue, MdbSubcommandType.RevalueLimitRequest) => ManualCommandKind.RevalueLimitRequest,
            _ => throw new NotSupportedException($"Scenario command {step.Command}/{step.Subcommand} has no standard MDB encoder mapping.")
        };
        return ManualCommandBuilder.Build(new ManualCommandInput(
            kind,
            kind is ManualCommandKind.VendRequest or ManualCommandKind.CashSale ? 5.00m : null,
            kind is ManualCommandKind.VendRequest or ManualCommandKind.VendSuccess or ManualCommandKind.CashSale ? 1 : null,
            kind == ManualCommandKind.RevalueRequest ? 1.00m : null,
            FeatureLevel: featureLevel), _encoder).Frame;
    }
}
