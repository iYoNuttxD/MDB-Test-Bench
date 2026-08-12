namespace MdbTestBench.Transport.Abstractions;

public sealed record TransportCapabilities
{
    public required string Name { get; init; }
    public IReadOnlySet<PollingMode> SupportedPollingModes { get; init; } =
        new HashSet<PollingMode>();
    public PollingMode PollingMode { get; init; }
    public bool RequiresPhysicalHardware { get; init; }
}
