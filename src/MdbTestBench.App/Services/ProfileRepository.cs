using MdbTestBench.Core.Capabilities;
using MdbTestBench.Core.Profiles;
using MdbTestBench.Core.Protocol;

namespace MdbTestBench.App.Services;

public sealed class ProfileRepository(AppPaths paths, ProfileJsonSerializer serializer)
{
    public const long MaxImportFileBytes = 1_048_576;

    public IReadOnlyList<string> LoadWarnings { get; private set; } = [];

    public IReadOnlyList<MdbProfile> LoadAll()
    {
        paths.EnsureDirectories();
        var profiles = new List<MdbProfile>(CreateBuiltIn());
        var warnings = new List<string>();
        foreach (var file in Directory.EnumerateFiles(paths.Profiles, "*.json"))
        {
            try
            {
                if (new FileInfo(file).Length > MaxImportFileBytes)
                    throw new InvalidDataException($"Profile JSON exceeds the {MaxImportFileBytes}-byte limit.");
                var profile = serializer.Deserialize(File.ReadAllText(file));
                profiles.Add(profile with { IsBuiltIn = false });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
            {
                warnings.Add($"Skipped invalid profile '{Path.GetFileName(file)}': {exception.Message}");
            }
        }
        LoadWarnings = warnings;
        return profiles;
    }

    public async Task SaveCustomAsync(MdbProfile profile, CancellationToken cancellationToken = default)
    {
        if (profile.IsBuiltIn) throw new InvalidOperationException("Built-in profiles cannot be overwritten.");
        paths.EnsureDirectories();
        var file = BuildContainedPath(paths.Profiles, $"{SanitizeFileName(profile.Id)}.json");
        await File.WriteAllTextAsync(file, serializer.Serialize(profile), cancellationToken);
    }

    public void DeleteCustom(MdbProfile profile)
    {
        if (profile.IsBuiltIn) throw new InvalidOperationException("Built-in profiles cannot be deleted.");
        var file = BuildContainedPath(paths.Profiles, $"{SanitizeFileName(profile.Id)}.json");
        if (File.Exists(file)) File.Delete(file);
    }

    public async Task<MdbProfile> ImportAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var source = new FileInfo(sourcePath);
        if (!source.Exists) throw new FileNotFoundException("The profile JSON file was not found.", sourcePath);
        if (source.Length > MaxImportFileBytes)
            throw new InvalidDataException($"Profile JSON exceeds the {MaxImportFileBytes}-byte limit.");
        var json = await File.ReadAllTextAsync(source.FullName, cancellationToken);
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
        var profileExports = Path.Combine(paths.Exports, "profiles");
        Directory.CreateDirectory(profileExports);
        var file = BuildContainedPath(profileExports,
            $"{SanitizeFileName(profile.Name)}-{DateTimeOffset.Now:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json");
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
        var safe = new string(value.Select(character =>
            invalid.Contains(character) || character is '/' or '\\' ? '-' : character).ToArray()).Trim('.', ' ');
        return string.IsNullOrWhiteSpace(safe) ? "profile" : safe[..Math.Min(safe.Length, 100)];
    }

    private static string BuildContainedPath(string directory, string fileName)
    {
        var root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root, fileName));
        if (!candidate.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidOperationException("The requested file path is outside the application data directory.");
        return candidate;
    }
}
