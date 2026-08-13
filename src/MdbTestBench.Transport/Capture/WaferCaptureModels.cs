using System.Text.Json.Serialization;
using MdbTestBench.Core.Protocol;
using MdbTestBench.Transport.Abstractions;
using MdbTestBench.Transport.Serial;

namespace MdbTestBench.Transport.Capture;

public static class WaferCaptureFormat
{
    public const string Name = "MDBTestBenchCapture";
    public const int Version = 1;
    public const long DefaultMaximumBytes = 100L * 1024 * 1024;
    public const int DefaultMaximumImportedEvents = 1_000_000;
}

public enum WaferCaptureEventType { Raw, Marker, Error, TransportState }
public enum WaferCaptureDirection { Tx, Rx }
public enum MdbInterpretationConfidence { Confirmed, Likely, Possible, Unknown }

public sealed record WaferCaptureApplication(string Name, string Version);

public sealed record WaferCaptureAdapter
{
    public string? Model { get; init; }
    public string? PrintedRevision { get; init; }
    public string? UsbVendorId { get; init; }
    public string? UsbProductId { get; init; }
    public string? Manufacturer { get; init; }
    public string? Product { get; init; }
    public string? SerialNumber { get; init; }
    public string? Driver { get; init; }
}

public sealed record WaferCaptureHost(
    string OperatingSystem,
    string OSVersion,
    string Architecture,
    string DotNetVersion);

public sealed record WaferCaptureSerial
{
    public string? Port { get; init; }
    public int BaudRate { get; init; }
    public int DataBits { get; init; }
    public string Parity { get; init; } = "None";
    public string StopBits { get; init; } = "One";
    public string Handshake { get; init; } = "None";
    public int ReadTimeoutMilliseconds { get; init; }
    public int WriteTimeoutMilliseconds { get; init; }
    public PollingMode PollingMode { get; init; }
    public SerialWireFormat WireFormat { get; init; }
    public AsciiHexTerminator Terminator { get; init; }
}

public sealed record WaferCaptureTiming
{
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? EndedAtUtc { get; init; }
    public long MonotonicFrequency { get; init; }
    public string ResolutionNote { get; init; } =
        "Chunk timestamps use Stopwatch monotonic ticks at application read/write boundaries; per-byte arrival timing is unavailable from System.IO.Ports.";
}

public sealed record MdbCaptureInterpretation(
    string Description,
    MdbInterpretationConfidence Confidence,
    string? Kind = null);

public sealed record WaferCaptureEvent
{
    public long Sequence { get; init; }
    public WaferCaptureEventType Type { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
    public long MonotonicTimestamp { get; init; }
    public double? DeltaMicroseconds { get; init; }
    public WaferCaptureDirection? Direction { get; init; }
    public string? Hex { get; init; }
    public string? Base64 { get; init; }
    public int Length { get; init; }
    public string Operation { get; init; } = string.Empty;
    public long? ReadChunkIndex { get; init; }
    public DateTimeOffset? FirstByteTimestampUtc { get; init; }
    public DateTimeOffset? LastByteTimestampUtc { get; init; }
    public double? OperationDurationMicroseconds { get; init; }
    public double? GapFromPreviousRxMicroseconds { get; init; }
    public IReadOnlyList<double>? InterByteTimingMicroseconds { get; init; }
    public string? TransportState { get; init; }
    public string? Text { get; init; }
    public string? ErrorKind { get; init; }
    public string? ErrorMessage { get; init; }
    public MdbCaptureInterpretation? PossibleMdbInterpretation { get; init; }

    [JsonIgnore]
    public bool IsRaw => Type == WaferCaptureEventType.Raw;

    public byte[] GetRawBytes()
    {
        if (!IsRaw) return [];
        if (string.IsNullOrWhiteSpace(Hex) || string.IsNullOrWhiteSpace(Base64))
            throw new InvalidDataException($"Raw event {Sequence} is missing HEX or Base64 data.");
        byte[] fromBase64;
        try { fromBase64 = Convert.FromBase64String(Base64); }
        catch (FormatException exception) { throw new InvalidDataException($"Raw event {Sequence} has invalid Base64.", exception); }
        var compactHex = string.Concat(Hex.Where(character => !char.IsWhiteSpace(character)));
        byte[] fromHex;
        try { fromHex = Convert.FromHexString(compactHex); }
        catch (FormatException exception) { throw new InvalidDataException($"Raw event {Sequence} has invalid HEX.", exception); }
        if (!fromHex.AsSpan().SequenceEqual(fromBase64))
            throw new InvalidDataException($"Raw event {Sequence} HEX and Base64 values differ.");
        if (Length != fromHex.Length)
            throw new InvalidDataException($"Raw event {Sequence} length does not match its bytes.");
        return fromHex;
    }

    public static WaferCaptureEvent Raw(
        long sequence,
        DateTimeOffset timestampUtc,
        long monotonicTimestamp,
        double? deltaMicroseconds,
        WaferCaptureDirection direction,
        ReadOnlySpan<byte> bytes,
        string operation,
        long? readChunkIndex,
        double? operationDurationMicroseconds,
        double? gapFromPreviousRxMicroseconds,
        string? transportState,
        MdbCaptureInterpretation? interpretation = null) => new()
        {
            Sequence = sequence,
            Type = WaferCaptureEventType.Raw,
            TimestampUtc = timestampUtc,
            MonotonicTimestamp = monotonicTimestamp,
            DeltaMicroseconds = deltaMicroseconds,
            Direction = direction,
            Hex = string.Join(' ', Convert.ToHexString(bytes).Chunk(2).Select(pair => new string(pair))),
            Base64 = Convert.ToBase64String(bytes),
            Length = bytes.Length,
            Operation = operation,
            ReadChunkIndex = readChunkIndex,
            LastByteTimestampUtc = timestampUtc,
            OperationDurationMicroseconds = operationDurationMicroseconds,
            GapFromPreviousRxMicroseconds = gapFromPreviousRxMicroseconds,
            InterByteTimingMicroseconds = null,
            TransportState = transportState,
            PossibleMdbInterpretation = interpretation
        };
}

public sealed record WaferProbe
{
    public required string Name { get; init; }
    public required string Hex { get; init; }
    public SerialWireFormat WireFormat { get; init; }
    public AsciiHexTerminator Terminator { get; init; }
    public string? Notes { get; init; }
}

public sealed record WaferPeriodicObservation
{
    public bool Detected { get; init; }
    public int IntervalCount { get; init; }
    public double? MedianIntervalMilliseconds { get; init; }
    public double? MinimumIntervalMilliseconds { get; init; }
    public double? MaximumIntervalMilliseconds { get; init; }
    public string Classification { get; init; } = "Observation only; periodicity does not prove MDB POLL ownership.";
}

public sealed record WaferCaptureStatistics
{
    public double DurationSeconds { get; init; }
    public long TxEvents { get; init; }
    public long RxEvents { get; init; }
    public long TxBytes { get; init; }
    public long RxBytes { get; init; }
    public long Errors { get; init; }
    public long Timeouts { get; init; }
    public long Markers { get; init; }
    public long PossibleMdbResponses { get; init; }
    public long UnknownRawEvents { get; init; }
    public IReadOnlyDictionary<string, long> MostCommonRxLengths { get; init; } = new Dictionary<string, long>();
    public IReadOnlyDictionary<string, long> RepeatedPrefixes { get; init; } = new Dictionary<string, long>();
    public IReadOnlyDictionary<string, long> RepeatedSuffixes { get; init; } = new Dictionary<string, long>();
    public bool PossibleCrDelimiter { get; init; }
    public bool PossibleLfDelimiter { get; init; }
    public bool PossibleCrLfDelimiter { get; init; }
    public string TrafficAppearance { get; init; } = "Unknown";
    public WaferPeriodicObservation PeriodicRxObservation { get; init; } = new();
}

public sealed record WaferCaptureDocument
{
    public string Format { get; init; } = WaferCaptureFormat.Name;
    public int FormatVersion { get; init; } = WaferCaptureFormat.Version;
    public bool PrivacySafe { get; init; } = true;
    public required string CaptureId { get; init; }
    public required WaferCaptureApplication Application { get; init; }
    public required WaferCaptureAdapter Adapter { get; init; }
    public required WaferCaptureHost Host { get; init; }
    public required WaferCaptureSerial Serial { get; init; }
    public required WaferCaptureTiming Capture { get; init; }
    public string? UserNotes { get; init; }
    public IReadOnlyList<WaferProbe> Probes { get; init; } = [];
    public WaferCaptureStatistics Statistics { get; init; } = new();
    public IReadOnlyList<WaferCaptureEvent> Events { get; init; } = [];
}

public sealed record WaferCaptureArtifact(
    WaferCaptureDocument Header,
    string EventSpoolPath,
    long CaptureSizeBytes,
    bool SizeLimitReached);
