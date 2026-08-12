using MdbTestBench.App.Services;
using MdbTestBench.Core.Profiles;

namespace MdbTestBench.App.Tests;

public sealed class ProfileRepositoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"mdb-app-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task ExportDoesNotBecomeAnotherLoadedCustomProfile()
    {
        var paths = new AppPaths(_root);
        var repository = new ProfileRepository(paths, new ProfileJsonSerializer());
        var builtIn = repository.LoadAll()[0];

        var exportPath = await repository.ExportAsync(builtIn);
        var reloaded = repository.LoadAll();

        Assert.StartsWith(Path.GetFullPath(paths.Exports), Path.GetFullPath(exportPath), StringComparison.Ordinal);
        Assert.Equal(3, reloaded.Count);
    }

    [Fact]
    public async Task OversizedImportIsRejectedBeforeDeserialization()
    {
        var paths = new AppPaths(_root);
        paths.EnsureDirectories();
        var source = Path.Combine(_root, "oversized.json");
        await File.WriteAllBytesAsync(source, new byte[ProfileRepository.MaxImportFileBytes + 1]);

        var repository = new ProfileRepository(paths, new ProfileJsonSerializer());

        await Assert.ThrowsAsync<InvalidDataException>(() => repository.ImportAsync(source));
    }

    [Fact]
    public async Task HostileProfileIdCannotEscapeProfilesDirectory()
    {
        var paths = new AppPaths(_root);
        var repository = new ProfileRepository(paths, new ProfileJsonSerializer());
        var profile = new MdbProfile { Id = "../../escape", Name = "Safe custom" };

        await repository.SaveCustomAsync(profile);

        Assert.Single(Directory.EnumerateFiles(paths.Profiles, "*.json"));
        Assert.False(File.Exists(Path.Combine(_root, "escape.json")));
    }

    [Fact]
    public async Task InvalidStoredProfileIsSkippedAndReported()
    {
        var paths = new AppPaths(_root);
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(Path.Combine(paths.Profiles, "invalid.json"), "{ invalid");
        var repository = new ProfileRepository(paths, new ProfileJsonSerializer());

        var profiles = repository.LoadAll();

        Assert.Equal(3, profiles.Count);
        Assert.Single(repository.LoadWarnings);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
