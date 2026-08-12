namespace MdbTestBench.Core.Capabilities;

public sealed record MdbCapabilities
{
    public bool Expansion { get; init; }
    public bool RemoteVend { get; init; }
    public bool Revalue { get; init; }
    public bool MultiCurrency { get; init; }
    public IReadOnlyDictionary<string, bool> Extensions { get; init; } =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
}
