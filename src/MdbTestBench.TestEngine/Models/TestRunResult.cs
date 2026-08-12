using MdbTestBench.Core.Protocol;

namespace MdbTestBench.TestEngine.Models;

public enum TestRunStatus { Passed, Failed, TimedOut, Aborted }

public sealed record TestStepResult(
    string Name,
    MdbResponseType? ActualResponse,
    MdbResponseType ExpectedResponse,
    bool Passed,
    TimeSpan Duration,
    string? Error = null);

public sealed record TestRunResult(
    string ScenarioId,
    TestRunStatus Status,
    IReadOnlyList<TestStepResult> Steps,
    TimeSpan Duration,
    string? Error = null);
