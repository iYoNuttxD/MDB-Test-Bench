namespace MdbTestBench.Transport.Simulation;

public enum SimulatorBehavior
{
    Normal,
    AlwaysApprove,
    AlwaysDeny,
    Timeout,
    MalformedResponse,
    UnexpectedResponse
}

public sealed record SimulatedCashlessOptions
{
    public TimeSpan ResponseDelay { get; init; } = TimeSpan.FromMilliseconds(35);
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public SimulatorBehavior Behavior { get; init; } = SimulatorBehavior.Normal;
}
