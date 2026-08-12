namespace MdbTestBench.Core.Capabilities;

public enum CapabilityStatus
{
    Unsupported,
    Supported,
    Experimental,
    NotImplemented
}

public sealed record MdbCapabilities
{
    public CapabilityStatus Expansion { get; init; } = CapabilityStatus.Unsupported;
    public CapabilityStatus Revalue { get; init; } = CapabilityStatus.Unsupported;
    public CapabilityStatus RemoteVend { get; init; } = CapabilityStatus.Unsupported;
    public CapabilityStatus MultiCurrency { get; init; } = CapabilityStatus.Unsupported;
    public CapabilityStatus NegativeVend { get; init; } = CapabilityStatus.Unsupported;
    public CapabilityStatus DataEntry { get; init; } = CapabilityStatus.Unsupported;
    public CapabilityStatus Basket { get; init; } = CapabilityStatus.Unsupported;
    public CapabilityStatus Refund { get; init; } = CapabilityStatus.Unsupported;
    public IReadOnlyDictionary<string, CapabilityStatus> Extensions { get; init; } =
        new Dictionary<string, CapabilityStatus>(StringComparer.OrdinalIgnoreCase);
}
