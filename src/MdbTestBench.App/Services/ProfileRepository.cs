using MdbTestBench.Core.Capabilities;
using MdbTestBench.Core.Profiles;
using MdbTestBench.Core.Protocol;

namespace MdbTestBench.App.Services;

public sealed class ProfileRepository(AppPaths paths, ProfileJsonSerializer serializer)
{
    public IReadOnlyList<MdbProfile> LoadAll()
    {
        paths.EnsureDirectories();
        var profiles = new List<MdbProfile>(CreateBuiltIn());
        foreach (var file in Directory.EnumerateFiles(paths.Profiles, "*.json"))
        {
            try
            {
                var profile = serializer.Deserialize(File.ReadAllText(file));
                profiles.Add(profile with { IsBuiltIn = false });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
            {
                // Invalid custom files remain on disk for user recovery and are not loaded.
            }
        }
        return profiles;
    }

    public async Task SaveCustomAsync(MdbProfile profile, CancellationToken cancellationToken = default)
    {
        if (profile.IsBuiltIn) throw new InvalidOperationException("Built-in profiles cannot be overwritten.");
        paths.EnsureDirectories();
        var file = Path.Combine(paths.Profiles, $"{SanitizeFileName(profile.Id)}.json");
        await File.WriteAllTextAsync(file, serializer.Serialize(profile), cancellationToken);
    }

    public void DeleteCustom(MdbProfile profile)
    {
        if (profile.IsBuiltIn) throw new InvalidOperationException("Built-in profiles cannot be deleted.");
        var file = Path.Combine(paths.Profiles, $"{SanitizeFileName(profile.Id)}.json");
        if (File.Exists(file)) File.Delete(file);
    }

    public async Task<MdbProfile> ImportAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(sourcePath, cancellationToken);
        var imported = serializer.Deserialize(json) with
        {
            Id = Guid.NewGuid().ToString("N"),
            IsBuiltIn = false
        };
        await SaveCustomAsync(imported, cancellationToken);
        return imported;
    }

    public async Task<string> ExportAsync(MdbProfile profile, CancellationToken cancellationToken = default)
    {
        paths.EnsureDirectories();
        var file = Path.Combine(paths.Profiles,
            $"export-{SanitizeFileName(profile.Name)}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.json");
        await File.WriteAllTextAsync(file, serializer.Serialize(profile), cancellationToken);
        return file;
    }

    private static IReadOnlyList<MdbProfile> CreateBuiltIn() =>
    [
        new Level1Profile
        {
            Id = "standard-level1",
            Name = "MDB Level 1",
            Description = "Conservative Level 1 baseline. Capabilities remain explicit.",
            Capabilities = new MdbCapabilities()
        },
        new Level2Profile
        {
            Id = "standard-level2",
            Name = "MDB Level 2",
            Description = "Level 2 baseline; listed capabilities are not implementation claims.",
            Capabilities = new MdbCapabilities { Expansion = CapabilityStatus.NotImplemented }
        },
        new Level3Profile
        {
            Id = "standard-level3",
            Name = "MDB Level 3",
            Description = "Level 3 baseline; validate every capability against the device.",
            Capabilities = new MdbCapabilities
            {
                Expansion = CapabilityStatus.NotImplemented,
                MultiCurrency = CapabilityStatus.NotImplemented
            }
        }
    ];

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "profile" : safe;
    }
}
