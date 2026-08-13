using MdbTestBench.Core.Capabilities;
using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;

namespace MdbTestBench.Core.Tests;

public sealed class MdbFeatureLevelTests
{
    [Fact]
    public void CommandsExposeTheirMinimumFeatureLevel()
    {
        Assert.Equal(MdbFeatureLevel.Level1, new MdbVendRequestCommand(1, 1).MinimumFeatureLevel);
        Assert.Equal(MdbFeatureLevel.Level2, new MdbRevalueRequestCommand(1).MinimumFeatureLevel);
        Assert.Equal(MdbFeatureLevel.Level3, new MdbRevalueRequestExpandedCommand(1).MinimumFeatureLevel);
        Assert.Equal(MdbFeatureLevel.Level3,
            new MdbExpansionEnableOptionsCommand(MdbLevel3Options.None).MinimumFeatureLevel);
    }

    [Fact]
    public void Level3DoesNotImplicitlyEnableEveryCapability()
    {
        var result = MdbLevel3CapabilityMapper.Apply(new MdbCapabilities(),
            MdbLevel3Options.RemoteVend | MdbLevel3Options.ThirtyTwoBitMonetary);

        Assert.Equal(CapabilityStatus.Supported, result.RemoteVend);
        Assert.Equal(CapabilityStatus.Unsupported, result.MultiCurrency);
        Assert.Equal(CapabilityStatus.Unsupported, result.Basket);
        Assert.Equal(CapabilityStatus.Supported, result.Extensions["ThirtyTwoBitMonetary"]);
    }
}
