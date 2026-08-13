using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MdbTestBench.App.Localization;
using MdbTestBench.App.ViewModels;
using MdbTestBench.Core.Logging;
using MdbTestBench.Core.Protocol;

namespace MdbTestBench.App.Tests;

public sealed partial class LocalizationTests
{
    [Fact]
    public void EnglishAndPortugueseResourcesContainExactlyTheSameKeys()
    {
        var localization = new LocalizationService();
        var english = localization.GetResourceKeys(CultureInfo.GetCultureInfo("en-US"));
        var portuguese = localization.GetResourceKeys(CultureInfo.GetCultureInfo("pt-BR"));

        Assert.NotEmpty(english);
        Assert.True(english.SetEquals(portuguese),
            $"Only en-US: {string.Join(", ", english.Except(portuguese))}; only pt-BR: {string.Join(", ", portuguese.Except(english))}");
    }

    [Theory]
    [InlineData(null, "pt-PT", "pt-BR")]
    [InlineData(null, "pt-AO", "pt-BR")]
    [InlineData(null, "fr-FR", "en-US")]
    [InlineData("pt-BR", "en-US", "pt-BR")]
    [InlineData("en-US", "pt-BR", "en-US")]
    [InlineData("unknown", "de-DE", "en-US")]
    [InlineData("unknown", "pt-BR", "en-US")]
    public void CultureResolutionUsesPersistedPreferenceThenDocumentedFallback(
        string? preference, string systemCulture, string expected)
    {
        var localization = new LocalizationService();
        Assert.Equal(expected, localization.ResolveCulture(preference, CultureInfo.GetCultureInfo(systemCulture)).Name);
    }

    [Fact]
    public void BothCulturesExposeTranslatedUserText()
    {
        var localization = new LocalizationService();
        localization.SetCulture("en-US");
        Assert.Equal("Settings", localization.Get("NavSettings"));
        localization.SetCulture("pt-BR");
        Assert.Equal("Configurações", localization.Get("NavSettings"));
    }

    [Fact]
    public void LogPresentationIsLocalizedWithoutMutatingMachineData()
    {
        var localization = new LocalizationService();
        var entry = new MdbLogEntry(DateTimeOffset.UtcNow, MdbDirection.Rx, "Transport", "Application",
            "STATUS", "Simulator connected", ReadOnlyMemory<byte>.Empty, MdbLogSeverity.Information);

        localization.SetCulture("pt-BR");
        Assert.Equal("Simulador conectado", new LogEntryViewModel(entry, localization).Description);
        Assert.Equal("Simulator connected", entry.DecodedDescription);
        localization.SetCulture("en-US");
        Assert.Equal("Simulator connected", new LogEntryViewModel(entry, localization).Description);
    }

    [Fact]
    public void ViewsDoNotContainLiteralUserFacingAttributes()
    {
        var root = FindRepositoryRoot();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "src", "MdbTestBench.App", "Views"), "*.axaml"))
        {
            var document = XDocument.Load(path);
            foreach (var element in document.Descendants().Where(item => item.Name.LocalName != "Run"))
            foreach (var attribute in element.Attributes().Where(item =>
                         item.Name.LocalName is "Text" or "Content" or "Header" or "PlaceholderText" or "Title"))
            {
                if (!UserTextRegex().IsMatch(attribute.Value)) continue;
                Assert.True(attribute.Value.StartsWith("{Binding", StringComparison.Ordinal) ||
                            attribute.Value.StartsWith("{DynamicResource", StringComparison.Ordinal),
                    $"Hardcoded user-facing text in {Path.GetFileName(path)}: {attribute.Name.LocalName}=\"{attribute.Value}\"");
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MDBTestBench.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    [GeneratedRegex("[A-Za-zÀ-ÿ]")]
    private static partial Regex UserTextRegex();
}
