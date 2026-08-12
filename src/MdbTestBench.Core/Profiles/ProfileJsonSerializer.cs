using System.Text.Json;
using System.Text.Json.Serialization;

namespace MdbTestBench.Core.Profiles;

public sealed class ProfileJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public MdbProfile Deserialize(string json)
    {
        var profile = JsonSerializer.Deserialize<MdbProfile>(json, Options)
            ?? throw new JsonException("Profile JSON did not contain an object.");
        MdbProfileValidator.EnsureValid(profile);
        return profile;
    }

    public string Serialize(MdbProfile profile)
    {
        MdbProfileValidator.EnsureValid(profile);
        return JsonSerializer.Serialize(profile, Options);
    }
}
