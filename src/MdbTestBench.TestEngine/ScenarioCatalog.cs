using MdbTestBench.Core.Protocol;
using MdbTestBench.TestEngine.Models;
using MdbTestBench.Transport.Simulation;

namespace MdbTestBench.TestEngine;

public static class ScenarioCatalog
{
    public static IReadOnlyList<TestScenario> CreateBuiltIn() =>
    [
        Scenario("l1-initialization", "L1 - Initialization", "Reset, setup and enable.", SimulatorBehavior.Normal,
            Step("RESET", MdbCommandType.Reset, MdbResponseType.Ack),
            Step("POLL JUST RESET", MdbCommandType.Poll, MdbResponseType.JustReset),
            Step("SETUP CONFIG", MdbCommandType.Setup, MdbResponseType.ReaderConfigData, MdbSubcommandType.SetupConfig),
            Step("ENABLE", MdbCommandType.Reader, MdbResponseType.Ack, MdbSubcommandType.Enable)),
        Scenario("l1-approved-vend", "L1 - Approved Vend", "Complete approved vend flow.", SimulatorBehavior.AlwaysApprove,
            FullVend(MdbResponseType.VendApproved).ToArray()),
        Scenario("l1-denied-vend", "L1 - Denied Vend", "Vend denial and session close.", SimulatorBehavior.AlwaysDeny,
            Initialization().Concat([
                Step("WAIT SESSION", MdbCommandType.Poll, MdbResponseType.BeginSession),
                Step("VEND REQUEST", MdbCommandType.Vend, MdbResponseType.VendDenied, MdbSubcommandType.VendRequest),
                Step("SESSION COMPLETE", MdbCommandType.Vend, MdbResponseType.EndSession, MdbSubcommandType.SessionComplete)
            ]).ToArray()),
        Scenario("l1-cancelled-vend", "L1 - Cancelled Vend", "Cancel an active reader session.", SimulatorBehavior.Normal,
            Initialization().Concat([
                Step("WAIT SESSION", MdbCommandType.Poll, MdbResponseType.BeginSession),
                Step("VEND REQUEST", MdbCommandType.Vend, MdbResponseType.Ack, MdbSubcommandType.VendRequest),
                Step("VEND CANCEL", MdbCommandType.Vend, MdbResponseType.VendDenied, MdbSubcommandType.VendCancel),
                Step("SESSION COMPLETE", MdbCommandType.Vend, MdbResponseType.EndSession, MdbSubcommandType.SessionComplete)
            ]).ToArray()),
        Scenario("l1-session-complete", "L1 - Session Complete", "Complete a successful session.", SimulatorBehavior.AlwaysApprove,
            FullVend(MdbResponseType.VendApproved).ToArray()),
        Scenario("timeout-handling", "Timeout Handling", "Verifies that a missing response becomes a controlled failure.", SimulatorBehavior.Timeout,
            Step("RESET WITH TIMEOUT", MdbCommandType.Reset, MdbResponseType.Ack)),
        Scenario("unexpected-response", "Unexpected Response", "Verifies expected-versus-received reporting.", SimulatorBehavior.UnexpectedResponse,
            Step("RESET EXPECTED", MdbCommandType.Reset, MdbResponseType.Ack))
    ];

    private static TestScenario Scenario(
        string id,
        string name,
        string description,
        SimulatorBehavior behavior,
        params TestStep[] steps) => new()
        {
            Id = id,
            Name = name,
            Description = description,
            RequiredProfile = MdbFeatureLevel.Level1,
            SimulatorBehavior = behavior,
            Timeout = behavior == SimulatorBehavior.Timeout ? TimeSpan.FromMilliseconds(300) : TimeSpan.FromSeconds(10),
            Steps = steps
        };

    private static IEnumerable<TestStep> Initialization() =>
    [
        Step("RESET", MdbCommandType.Reset, MdbResponseType.Ack),
        Step("POLL JUST RESET", MdbCommandType.Poll, MdbResponseType.JustReset),
        Step("SETUP", MdbCommandType.Setup, MdbResponseType.ReaderConfigData, MdbSubcommandType.SetupConfig),
        Step("ENABLE", MdbCommandType.Reader, MdbResponseType.Ack, MdbSubcommandType.Enable)
    ];

    private static IEnumerable<TestStep> FullVend(MdbResponseType vendResponse) => Initialization().Concat([
        Step("WAIT SESSION", MdbCommandType.Poll, MdbResponseType.BeginSession),
        Step("VEND REQUEST", MdbCommandType.Vend, vendResponse, MdbSubcommandType.VendRequest),
        Step("VEND SUCCESS", MdbCommandType.Vend, MdbResponseType.Ack, MdbSubcommandType.VendSuccess),
        Step("SESSION COMPLETE", MdbCommandType.Vend, MdbResponseType.EndSession, MdbSubcommandType.SessionComplete)
    ]);

    private static TestStep Step(
        string name,
        MdbCommandType command,
        MdbResponseType response,
        MdbSubcommandType subcommand = MdbSubcommandType.None) => new()
        {
            Name = name,
            Command = command,
            Subcommand = subcommand,
            ExpectedResponse = response
        };
}
