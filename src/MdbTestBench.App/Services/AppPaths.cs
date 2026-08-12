namespace MdbTestBench.App.Services;

public sealed class AppPaths
{
    public AppPaths(string? root = null)
    {
        Root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MdbTestBench");
        Profiles = Path.Combine(Root, "profiles");
        Scenarios = Path.Combine(Root, "scenarios");
        Logs = Path.Combine(Root, "logs");
        Exports = Path.Combine(Root, "exports");
        Settings = Path.Combine(Root, "settings.json");
    }

    public string Root { get; }
    public string Profiles { get; }
    public string Scenarios { get; }
    public string Logs { get; }
    public string Exports { get; }
    public string Settings { get; }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Profiles);
        Directory.CreateDirectory(Scenarios);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Exports);
    }
}
