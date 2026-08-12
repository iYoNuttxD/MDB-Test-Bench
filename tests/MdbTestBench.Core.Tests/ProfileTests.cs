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
            Capabilities = new MdbCapabilities { Expansion = true, RemoteVend = true }
        };

        Assert.Equal(MdbFeatureLevel.Level1, profile.BaseLevel);
        Assert.True(profile.Capabilities.Expansion);
        Assert.True(profile.Capabilities.RemoteVend);
        Assert.False(profile.Capabilities.Revalue);
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
            Capabilities = new MdbCapabilities { Expansion = true }
        };

        var json = JsonSerializer.Serialize(profile, options);
        var result = JsonSerializer.Deserialize<MdbProfile>(json, options);

        Assert.NotNull(result);
        Assert.Equal(profile.Name, result.Name);
        Assert.Equal(MdbFeatureLevel.Level2, result.BaseLevel);
        Assert.True(result.Capabilities.Expansion);
    }
}
