using MdbTestBench.Core.Capabilities;
using MdbTestBench.Core.Protocol;

namespace MdbTestBench.Core.Profiles;

public record MdbProfile
{
    public required string Name { get; init; }
    public MdbFeatureLevel BaseLevel { get; init; } = MdbFeatureLevel.Custom;
    public MdbCapabilities Capabilities { get; init; } = new();
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record Level1Profile : MdbProfile
{
    public Level1Profile() => BaseLevel = MdbFeatureLevel.Level1;
}

public sealed record Level2Profile : MdbProfile
{
    public Level2Profile() => BaseLevel = MdbFeatureLevel.Level2;
}

public sealed record Level3Profile : MdbProfile
{
    public Level3Profile() => BaseLevel = MdbFeatureLevel.Level3;
}

public sealed record CustomProfile : MdbProfile
{
    public CustomProfile() => BaseLevel = MdbFeatureLevel.Custom;
}
