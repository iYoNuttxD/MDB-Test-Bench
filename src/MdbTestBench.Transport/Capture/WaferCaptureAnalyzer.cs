using System.Globalization;

namespace MdbTestBench.Transport.Capture;

public sealed class WaferCaptureAnalyzer
{
    public WaferCaptureStatistics Analyze(IEnumerable<WaferCaptureEvent> source, WaferCaptureTiming timing)
    {
        long txEvents = 0, rxEvents = 0, txBytes = 0, rxBytes = 0, errors = 0, timeouts = 0, markers = 0;
        long possible = 0, unknown = 0, printable = 0, observedBytes = 0;
        var lengths = new Dictionary<string, long>();
        var prefixes = new Dictionary<string, long>();
        var suffixes = new Dictionary<string, long>();
        var intervals = new List<double>();
        long? previousRxTick = null;
        var cr = 0; var lf = 0; var crlf = 0;
        DateTimeOffset? lastTimestamp = null;

        foreach (var item in source)
        {
            lastTimestamp = item.TimestampUtc;
            if (item.Type == WaferCaptureEventType.Error)
            {
                errors++;
                if (item.ErrorKind?.Contains("Timeout", StringComparison.OrdinalIgnoreCase) == true) timeouts++;
            }
            if (item.Type == WaferCaptureEventType.Marker) markers++;
            if (!item.IsRaw) continue;
            if (item.PossibleMdbInterpretation?.Confidence is MdbInterpretationConfidence.Likely or MdbInterpretationConfidence.Possible) possible++;
            else unknown++;
            if (item.Direction == WaferCaptureDirection.Tx) { txEvents++; txBytes += item.Length; continue; }
            if (item.Direction != WaferCaptureDirection.Rx) continue;
            rxEvents++; rxBytes += item.Length;
            Increment(lengths, item.Length.ToString(CultureInfo.InvariantCulture));
            if (previousRxTick is not null)
                intervals.Add((item.MonotonicTimestamp - previousRxTick.Value) * 1000d / timing.MonotonicFrequency);
            previousRxTick = item.MonotonicTimestamp;
            var bytes = SafeBytes(item);
            if (bytes.Length == 0) continue;
            Increment(prefixes, $"{bytes[0]:X2}");
            Increment(suffixes, $"{bytes[^1]:X2}");
            if (bytes[^1] == 0x0D) cr++;
            if (bytes[^1] == 0x0A) lf++;
            if (bytes.Length >= 2 && bytes[^2] == 0x0D && bytes[^1] == 0x0A) crlf++;
            observedBytes += bytes.Length;
            printable += bytes.LongCount(value => value is >= 0x20 and <= 0x7E || value is 0x09 or 0x0A or 0x0D);
        }

        intervals.RemoveAll(value => value < 0);
        intervals.Sort();
        var median = intervals.Count == 0 ? (double?)null : intervals.Count % 2 == 1
            ? intervals[intervals.Count / 2]
            : (intervals[intervals.Count / 2 - 1] + intervals[intervals.Count / 2]) / 2;
        var periodic = intervals.Count >= 3 && median > 0 &&
            intervals.All(value => Math.Abs(value - median.Value) <= Math.Max(2, median.Value * .20));
        var ratio = observedBytes == 0 ? -1 : printable / (double)observedBytes;

        return new WaferCaptureStatistics
        {
            DurationSeconds = Math.Max(0, ((timing.EndedAtUtc ?? lastTimestamp) - timing.StartedAtUtc)?.TotalSeconds ?? 0),
            TxEvents = txEvents, RxEvents = rxEvents, TxBytes = txBytes, RxBytes = rxBytes,
            Errors = errors, Timeouts = timeouts, Markers = markers,
            PossibleMdbResponses = possible, UnknownRawEvents = unknown,
            MostCommonRxLengths = Top(lengths),
            RepeatedPrefixes = Top(prefixes.Where(item => item.Value > 1).ToDictionary()),
            RepeatedSuffixes = Top(suffixes.Where(item => item.Value > 1).ToDictionary()),
            PossibleCrDelimiter = cr >= 2, PossibleLfDelimiter = lf >= 2, PossibleCrLfDelimiter = crlf >= 2,
            TrafficAppearance = ratio < 0 ? "Unknown" : ratio >= .85 ? "ASCII-looking" : ratio <= .35 ? "Binary-looking" : "Mixed",
            PeriodicRxObservation = new WaferPeriodicObservation
            {
                Detected = periodic, IntervalCount = intervals.Count, MedianIntervalMilliseconds = median,
                MinimumIntervalMilliseconds = intervals.Count == 0 ? null : intervals[0],
                MaximumIntervalMilliseconds = intervals.Count == 0 ? null : intervals[^1]
            }
        };
    }

    private static byte[] SafeBytes(WaferCaptureEvent item)
    {
        try { return item.GetRawBytes(); }
        catch (InvalidDataException) { return []; }
    }

    private static void Increment(Dictionary<string, long> values, string key) =>
        values[key] = values.GetValueOrDefault(key) + 1;

    private static Dictionary<string, long> Top(IDictionary<string, long> source) =>
        source.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.Ordinal)
            .Take(10).ToDictionary(item => item.Key, item => item.Value);
}
