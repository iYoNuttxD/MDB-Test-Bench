using System.Globalization;

namespace MdbTestBench.App.Localization;

public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }
    event EventHandler? CultureChanged;
    string GetString(string key);
    string Format(string key, params object?[] arguments);
    CultureInfo ResolveCulture(string? preference, CultureInfo systemCulture);
    void SetCulture(string cultureName);
    IReadOnlySet<string> GetResourceKeys(CultureInfo culture);
}

public static class LocalizationServiceExtensions
{
    public static string Get(this ILocalizationService service, string key) => service.GetString(key);
}
