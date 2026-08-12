using MdbTestBench.Core.Logging;

namespace MdbTestBench.App.Services;

public sealed class LogExportService(AppPaths paths)
{
    public async Task<(string TextPath, string JsonPath)> ExportSessionAsync(
        IReadOnlyList<MdbLogEntry> entries,
        CancellationToken cancellationToken = default)
    {
        paths.EnsureDirectories();
        var stem = $"mdb-session-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
        var textPath = Path.Combine(paths.Logs, stem + ".txt");
        var jsonPath = Path.Combine(paths.Logs, stem + ".json");
        await File.WriteAllTextAsync(textPath, MdbLogFormatter.ToText(entries), cancellationToken);
        await File.WriteAllTextAsync(jsonPath, MdbLogFormatter.ToJson(entries), cancellationToken);
        return (textPath, jsonPath);
    }
}
