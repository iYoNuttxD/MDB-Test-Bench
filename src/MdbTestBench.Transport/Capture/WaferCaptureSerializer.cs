using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MdbTestBench.Transport.Capture;

public sealed class WaferCaptureSerializer
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    internal static readonly JsonSerializerOptions SpoolJsonOptions = new(JsonOptions) { WriteIndented = false };

    public async Task ExportAsync(WaferCaptureArtifact artifact, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        EnsureSafeOutputPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteString("format", artifact.Header.Format);
        writer.WriteNumber("formatVersion", artifact.Header.FormatVersion);
        writer.WriteBoolean("privacySafe", artifact.Header.PrivacySafe);
        writer.WriteString("captureId", artifact.Header.CaptureId);
        Write(writer, "application", artifact.Header.Application);
        Write(writer, "adapter", artifact.Header.Adapter);
        Write(writer, "host", artifact.Header.Host);
        Write(writer, "serial", artifact.Header.Serial);
        Write(writer, "capture", artifact.Header.Capture);
        if (artifact.Header.UserNotes is not null) writer.WriteString("userNotes", artifact.Header.UserNotes);
        Write(writer, "probes", artifact.Header.Probes);
        Write(writer, "statistics", artifact.Header.Statistics);
        writer.WriteStartArray("events");
        using var reader = new StreamReader(artifact.EventSpoolPath, Encoding.UTF8, true, 64 * 1024);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            using var json = JsonDocument.Parse(line);
            json.RootElement.WriteTo(writer);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken);
    }

    public async Task<WaferCaptureDocument> LoadAsync(string path,
        long maximumBytes = WaferCaptureFormat.DefaultMaximumBytes,
        int maximumEvents = WaferCaptureFormat.DefaultMaximumImportedEvents,
        CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("Capture file was not found.", path);
        if (info.Length > maximumBytes) throw new InvalidDataException($"Capture exceeds the {maximumBytes}-byte import limit.");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        WaferCaptureDocument document;
        try { document = (await JsonSerializer.DeserializeAsync<WaferCaptureDocument>(stream, JsonOptions, cancellationToken))
            ?? throw new InvalidDataException("Capture JSON is empty."); }
        catch (JsonException exception) { throw new InvalidDataException("Capture JSON is invalid.", exception); }
        Validate(document, maximumEvents);
        return document;
    }

    public async Task ExportDocumentAsync(WaferCaptureDocument document, string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        Validate(document, WaferCaptureFormat.DefaultMaximumImportedEvents);
        EnsureSafeOutputPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await JsonSerializer.SerializeAsync(stream, document with { PrivacySafe = true }, JsonOptions, cancellationToken);
    }

    public async Task ExportSummaryAsync(WaferCaptureDocument document, string path, CancellationToken cancellationToken = default)
    {
        EnsureSafeOutputPath(path);
        var stats = document.Statistics;
        var lines = new[]
        {
            "MDB Test Bench Capture Summary", $"Capture: {document.CaptureId}",
            $"Adapter: {document.Adapter.Model ?? "Unknown"} / {document.Adapter.PrintedRevision ?? "Unknown"}",
            $"Started UTC: {document.Capture.StartedAtUtc:O}", $"Ended UTC: {document.Capture.EndedAtUtc:O}",
            $"Duration: {stats.DurationSeconds:0.000} s", $"TX: {stats.TxEvents} events / {stats.TxBytes} bytes",
            $"RX: {stats.RxEvents} events / {stats.RxBytes} bytes", $"Errors: {stats.Errors}",
            $"Timeouts: {stats.Timeouts}", $"Markers: {stats.Markers}",
            $"Most common RX lengths: {FormatCounts(stats.MostCommonRxLengths)}",
            $"Repeated RX prefixes: {FormatCounts(stats.RepeatedPrefixes)}",
            $"Repeated RX suffixes: {FormatCounts(stats.RepeatedSuffixes)}",
            $"Possible MDB responses: {stats.PossibleMdbResponses}", $"Unknown raw events: {stats.UnknownRawEvents}",
            $"Traffic: {stats.TrafficAppearance}",
            $"Periodic RX: {(stats.PeriodicRxObservation.Detected ? "observed" : "not observed")} (does not prove MDB POLL ownership)"
        };
        await File.WriteAllLinesAsync(path, lines, new UTF8Encoding(false), cancellationToken);
    }

    public static string CreateSafeFileName(string? revision, DateTimeOffset timestamp)
    {
        var safe = string.Concat((revision ?? "unknown").Where(character => char.IsLetterOrDigit(character) || character is '-' or '_'));
        if (safe.Length == 0) safe = "unknown";
        return $"wafer-{safe}-{timestamp.UtcDateTime:yyyy-MM-ddTHHmmss}.mdbcap.json";
    }

    public static void Validate(WaferCaptureDocument document, int maximumEvents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumEvents);
        if (document.Application is null || document.Adapter is null || document.Host is null ||
            document.Serial is null || document.Capture is null || document.Probes is null ||
            document.Statistics is null || document.Events is null)
            throw new InvalidDataException("Capture is missing a required schema section.");
        if (!string.Equals(document.Format, WaferCaptureFormat.Name, StringComparison.Ordinal))
            throw new InvalidDataException("File is not an MDB Test Bench capture.");
        if (document.FormatVersion != WaferCaptureFormat.Version)
            throw new NotSupportedException($"Capture format version {document.FormatVersion} is not supported.");
        if (string.IsNullOrWhiteSpace(document.CaptureId)) throw new InvalidDataException("Capture ID is required.");
        if (string.IsNullOrWhiteSpace(document.Application.Name) || string.IsNullOrWhiteSpace(document.Application.Version))
            throw new InvalidDataException("Capture application name and version are required.");
        if (document.Serial.BaudRate <= 0 || document.Serial.DataBits is < 5 or > 8 ||
            document.Serial.ReadTimeoutMilliseconds < 0 || document.Serial.WriteTimeoutMilliseconds < 0)
            throw new InvalidDataException("Capture serial configuration is invalid.");
        if (document.Events.Count > maximumEvents) throw new InvalidDataException("Capture contains too many events.");
        if (document.Capture.MonotonicFrequency <= 0) throw new InvalidDataException("Monotonic clock frequency is invalid.");
        if (document.Capture.CreatedAtUtc == default || document.Capture.StartedAtUtc == default)
            throw new InvalidDataException("Capture creation and start timestamps are required.");
        if (document.Capture.EndedAtUtc < document.Capture.StartedAtUtc) throw new InvalidDataException("Capture end precedes start.");
        long previousSequence = 0;
        long previousMonotonicTimestamp = -1;
        foreach (var item in document.Events)
        {
            if (item.Sequence <= previousSequence) throw new InvalidDataException("Event sequence is not strictly increasing.");
            previousSequence = item.Sequence;
            if (item.TimestampUtc == default) throw new InvalidDataException($"Event {item.Sequence} has no UTC timestamp.");
            if (item.MonotonicTimestamp < 0) throw new InvalidDataException($"Event {item.Sequence} has an invalid monotonic timestamp.");
            if (item.MonotonicTimestamp < previousMonotonicTimestamp)
                throw new InvalidDataException($"Event {item.Sequence} has a regressing monotonic timestamp.");
            previousMonotonicTimestamp = item.MonotonicTimestamp;
            if (string.IsNullOrWhiteSpace(item.Operation))
                throw new InvalidDataException($"Event {item.Sequence} has no operation.");
            if (item.DeltaMicroseconds < 0 || item.OperationDurationMicroseconds < 0 ||
                item.GapFromPreviousRxMicroseconds < 0 || item.ReadChunkIndex <= 0)
                throw new InvalidDataException($"Event {item.Sequence} has invalid timing metadata.");
            if (item.IsRaw)
            {
                if (item.Direction is null) throw new InvalidDataException($"Raw event {item.Sequence} has no direction.");
                _ = item.GetRawBytes();
            }
            else if (item.Length != 0 || item.Hex is not null || item.Base64 is not null)
                throw new InvalidDataException($"Non-raw event {item.Sequence} contains raw byte fields.");
        }
    }

    private static void Write<T>(Utf8JsonWriter writer, string propertyName, T value)
    {
        writer.WritePropertyName(propertyName);
        JsonSerializer.Serialize(writer, value, JsonOptions);
    }

    private static string FormatCounts(IReadOnlyDictionary<string, long> values) => values.Count == 0
        ? "none" : string.Join(", ", values.Select(item => $"{item.Key} x {item.Value}"));

    private static void EnsureSafeOutputPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.GetFileName(path) is "." or "..")
            throw new ArgumentException("A file path is required.", nameof(path));
    }
}
