using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Avalonia;

namespace MdbTestBench.App.Localization;

public sealed class LocalizationService : ILocalizationService
{
    public const string EnglishCultureName = "en-US";
    public const string PortugueseCultureName = "pt-BR";
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Resources =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            [EnglishCultureName] = Load(EnglishCultureName),
            [PortugueseCultureName] = Load(PortugueseCultureName)
        };

    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.GetCultureInfo(EnglishCultureName);
    public event EventHandler? CultureChanged;

    public string GetString(string key) => Resources[CurrentCulture.Name].GetValueOrDefault(key)
        ?? Resources[EnglishCultureName].GetValueOrDefault(key)
        ?? $"[{key}]";

    public string Format(string key, params object?[] arguments) =>
        string.Format(CurrentCulture, GetString(key), arguments);

    public CultureInfo ResolveCulture(string? preference, CultureInfo systemCulture)
    {
        if (string.Equals(preference, PortugueseCultureName, StringComparison.OrdinalIgnoreCase))
            return CultureInfo.GetCultureInfo(PortugueseCultureName);
        if (string.Equals(preference, EnglishCultureName, StringComparison.OrdinalIgnoreCase))
            return CultureInfo.GetCultureInfo(EnglishCultureName);
        if (!string.IsNullOrWhiteSpace(preference))
            return CultureInfo.GetCultureInfo(EnglishCultureName);
        return systemCulture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo(PortugueseCultureName)
            : CultureInfo.GetCultureInfo(EnglishCultureName);
    }

    public void SetCulture(string cultureName)
    {
        var resolved = ResolveCulture(cultureName, CultureInfo.CurrentUICulture);
        CurrentCulture = resolved;
        CultureInfo.CurrentCulture = resolved;
        CultureInfo.CurrentUICulture = resolved;
        CultureInfo.DefaultThreadCurrentCulture = resolved;
        CultureInfo.DefaultThreadCurrentUICulture = resolved;
        ApplyApplicationResources();
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlySet<string> GetResourceKeys(CultureInfo culture)
    {
        var resolved = ResolveCulture(culture.Name, culture);
        return Resources[resolved.Name].Keys.ToHashSet(StringComparer.Ordinal);
    }

    public void ApplyApplicationResources()
    {
        var dictionary = Application.Current?.Resources;
        if (dictionary is null) return;
        foreach (var key in GetResourceKeys(CurrentCulture)) dictionary[key] = GetString(key);
    }

    private static Dictionary<string, string> Load(string cultureName)
    {
        var resourceName = $"MdbTestBench.App.Localization.{cultureName}.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded localization resource '{resourceName}' was not found.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidDataException($"Localization resource '{resourceName}' is invalid.");
    }
}
