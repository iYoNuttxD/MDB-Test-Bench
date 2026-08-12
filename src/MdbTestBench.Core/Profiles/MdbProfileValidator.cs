namespace MdbTestBench.Core.Profiles;

public static class MdbProfileValidator
{
    public static IReadOnlyList<string> Validate(MdbProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(profile.Id)) errors.Add("Profile ID is required.");
        if (string.IsNullOrWhiteSpace(profile.Name)) errors.Add("Profile name is required.");
        else if (profile.Name.Length > 100) errors.Add("Profile name must contain at most 100 characters.");
        if (profile.Description is null) errors.Add("Profile description cannot be null.");
        else if (profile.Description.Length > 1_000)
            errors.Add("Profile description must contain at most 1000 characters.");
        if (profile.Capabilities is null) errors.Add("Profile capabilities are required.");
        if (profile.Metadata is null) errors.Add("Profile metadata cannot be null.");
        return errors;
    }

    public static void EnsureValid(MdbProfile profile)
    {
        var errors = Validate(profile);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(" ", errors));
    }
}
