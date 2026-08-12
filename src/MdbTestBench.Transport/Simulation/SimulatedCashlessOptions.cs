namespace MdbTestBench.Transport.Simulation;

public sealed record SimulatedCashlessOptions
{
    public TimeSpan ResponseDelay { get; init; } = TimeSpan.FromMilliseconds(10);
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public bool ApproveVends { get; init; } = true;
}
