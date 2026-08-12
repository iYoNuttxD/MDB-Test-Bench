using MdbTestBench.Core.Capabilities;
using MdbTestBench.Core.Profiles;
using MdbTestBench.Core.Protocol;

namespace MdbTestBench.Core.Tests;

public sealed class ProfileValidationTests
{
    [Fact]
    public void CustomProfileRoundTripsAllCapabilityStates()
    {
        var serializer = new ProfileJsonSerializer();
        var profile = new MdbProfile
        {
            Id = "custom-1",
            Name = "Hybrid DUT",
            Description = "Lab profile",
            BaseLevel = MdbFeatureLevel.Level1,
            Capabilities = new MdbCapabilities
            {
                Expansion = CapabilityStatus.Supported,
                Revalue = CapabilityStatus.NotImplemented,
                RemoteVend = CapabilityStatus.Experimental,
                Refund = CapabilityStatus.Unsupported
            }
        };

        var result = serializer.Deserialize(serializer.Serialize(profile));

        Assert.Equal(profile.Id, result.Id);
        Assert.Equal(profile.Name, result.Name);
        Assert.Equal(profile.BaseLevel, result.BaseLevel);
        Assert.Equal(CapabilityStatus.Experimental, result.Capabilities.RemoteVend);
    }

    [Fact]
    public void BlankCustomProfileIsInvalid()
    {
        var errors = MdbProfileValidator.Validate(new MdbProfile { Id = "", Name = "" });
        Assert.Contains(errors, error => error.Contains("ID", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("name", StringComparison.Ordinal));
    }

    [Fact]
    public void NullMembersFromHostileJsonAreRejectedWithoutNullReferenceFailure()
    {
        const string json = """
            { "id": "unsafe", "name": null, "description": null, "capabilities": null, "metadata": null }
            """;

        var exception = Assert.Throws<InvalidDataException>(() => new ProfileJsonSerializer().Deserialize(json));

        Assert.Contains("name", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("capabilities", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
