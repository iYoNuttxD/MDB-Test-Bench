using System.Text.Json;
using System.Text.Json.Serialization;
using MdbTestBench.Core.Capabilities;
using MdbTestBench.Core.Profiles;
using MdbTestBench.Core.Protocol;

namespace MdbTestBench.Core.Tests;

public sealed class ProfileTests
{
    [Fact]
    public void HybridProfile_KeepsBaseLevelSeparateFromCapabilities()
    {
        var profile = new Level1Profile
        {
            Name = "Hybrid machine",
            Capabilities = new MdbCapabilities
            {
                Expansion = CapabilityStatus.Supported,
                RemoteVend = CapabilityStatus.Experimental
            }
        };

        Assert.Equal(MdbFeatureLevel.Level1, profile.BaseLevel);
        Assert.Equal(CapabilityStatus.Supported, profile.Capabilities.Expansion);
        Assert.Equal(CapabilityStatus.Experimental, profile.Capabilities.RemoteVend);
        Assert.Equal(CapabilityStatus.Unsupported, profile.Capabilities.Revalue);
    }

    [Fact]
    public void Profile_RoundTripsAsJson()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };
        var profile = new MdbProfile
        {
            Name = "Standard L2",
            BaseLevel = MdbFeatureLevel.Level2,
            Capabilities = new MdbCapabilities { Expansion = CapabilityStatus.Supported }
        };

        var json = JsonSerializer.Serialize(profile, options);
        var result = JsonSerializer.Deserialize<MdbProfile>(json, options);

        Assert.NotNull(result);
        Assert.Equal(profile.Name, result.Name);
        Assert.Equal(MdbFeatureLevel.Level2, result.BaseLevel);
        Assert.Equal(CapabilityStatus.Supported, result.Capabilities.Expansion);
    }
}
