using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Profiles;
using MdbTestBench.Transport.Simulation;

namespace MdbTestBench.TestEngine.Models;

public sealed record TestScenario
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public MdbFeatureLevel RequiredProfile { get; init; } = MdbFeatureLevel.Level1;
    public SimulatorBehavior SimulatorBehavior { get; init; } = SimulatorBehavior.Normal;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    public IReadOnlyList<TestStep> Steps { get; init; } = [];
}

public sealed record TestStep
{
    public required string Name { get; init; }
    public required MdbCommandType Command { get; init; }
    public MdbSubcommandType Subcommand { get; init; } = MdbSubcommandType.None;
    public required MdbResponseType ExpectedResponse { get; init; }
    public string? PayloadHex { get; init; }
}
