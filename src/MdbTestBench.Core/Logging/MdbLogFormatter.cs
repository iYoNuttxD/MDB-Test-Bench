using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace MdbTestBench.Core.Logging;

public static class MdbLogFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ToText(IEnumerable<MdbLogEntry> entries) => string.Join(
        Environment.NewLine,
        entries.Select(entry =>
            $"{entry.Timestamp:HH:mm:ss.ffffff} {entry.Direction,-2} {entry.Command,-24} {entry.DecodedDescription} RAW: {FormatHex(entry.RawData.Span)} [{entry.Severity}]"));

    public static string ToJson(IEnumerable<MdbLogEntry> entries) =>
        JsonSerializer.Serialize(entries.Select(entry => new
        {
            entry.Timestamp,
            entry.Direction,
            entry.Source,
            entry.Destination,
            entry.Command,
            entry.DecodedDescription,
            RawData = FormatHex(entry.RawData.Span),
            entry.Severity
        }), JsonOptions);

    public static string FormatHex(ReadOnlySpan<byte> bytes) =>
        string.Join(' ', bytes.ToArray().Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
}
