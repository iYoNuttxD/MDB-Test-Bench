using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using MdbTestBench.Transport.Abstractions;
using MdbTestBench.Transport.Capture;
using MdbTestBench.Transport.Serial;
using MdbTestBench.Transport.Simulation;

namespace MdbTestBench.Transport.Tests;

public sealed class WaferCaptureTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "mdb-capture-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExportImportPreservesEveryRawChunkExactly()
    {
        var recorder = await WaferCaptureRecorder.StartAsync(Header(), _directory);
        await using (recorder)
        {
            var tick = Stopwatch.GetTimestamp();
            await recorder.RecordRawAsync(WaferCaptureDirection.Rx, new byte[] { 0x03 }, "read", tick, tick + 1, "Open");
            await recorder.RecordRawAsync(WaferCaptureDirection.Rx, new byte[] { 0xFF, 0xFE }, "read", tick + 2, tick + 3, "Open");
            await recorder.RecordRawAsync(WaferCaptureDirection.Tx, new byte[] { 0x00, 0x0D }, "write", tick + 4, tick + 5, "Open");
            await recorder.AddMarkerAsync("Cashless LED turned on");
            await recorder.RecordErrorAsync("ReadTimeout", "No data arrived.");
            var artifact = await recorder.StopAsync();
            var path = Path.Combine(_directory, "roundtrip.mdbcap.json");
            var serializer = new WaferCaptureSerializer();
            await serializer.ExportAsync(artifact, path);
            var imported = await serializer.LoadAsync(path);

            var chunks = imported.Events.Where(item => item.IsRaw).Select(item => item.GetRawBytes()).ToArray();
            Assert.Equal(3, chunks.Length);
            Assert.Equal(new byte[] { 0x03 }, chunks[0]);
            Assert.Equal(new byte[] { 0xFF, 0xFE }, chunks[1]);
            Assert.Equal(new byte[] { 0x00, 0x0D }, chunks[2]);
            Assert.Equal([1L, 2L], imported.Events.Where(item => item.Direction == WaferCaptureDirection.Rx)
                .Select(item => item.ReadChunkIndex));
            Assert.Single(imported.Events, item => item.Type == WaferCaptureEventType.Marker);
            Assert.Single(imported.Events, item => item.Type == WaferCaptureEventType.Error);
            Assert.True(imported.PrivacySafe);
            Assert.DoesNotContain(Environment.UserName, await File.ReadAllTextAsync(path), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ControllerCapturesSimulatedRxTxAndMarker()
    {
        var recorder = await WaferCaptureRecorder.StartAsync(Header(), _directory);
        await using var controller = new WaferDiscoveryCaptureController(new DiscoverySimulatorTransport(), recorder);
        await controller.StartAsync();
        await controller.AddMarkerAsync("Powered Wafer");
        await controller.SendAsync(new byte[] { 0x10 }, new SerialWireFormatOptions(), "ManualProbe");
        await Task.Delay(50);
        var artifact = await controller.StopAsync();
        var path = Path.Combine(_directory, "simulator.mdbcap.json");
        var serializer = new WaferCaptureSerializer();
        await serializer.ExportAsync(artifact, path);
        var imported = await serializer.LoadAsync(path);

        Assert.Contains(imported.Events, item => item.Direction == WaferCaptureDirection.Tx && item.GetRawBytes().SequenceEqual(new byte[] { 0x10 }));
        Assert.Contains(imported.Events, item => item.Direction == WaferCaptureDirection.Rx && item.GetRawBytes().SequenceEqual(new byte[] { 0x03 }));
        Assert.Contains(imported.Events, item => item.Type == WaferCaptureEventType.Marker);
        Assert.True(imported.Statistics.RxEvents >= 3);
    }

    [Fact]
    public async Task SizeLimitStopsWithoutUnboundedGrowth()
    {
        var recorder = await WaferCaptureRecorder.StartAsync(Header(), _directory, 1024);
        await using (recorder)
        {
            var tick = Stopwatch.GetTimestamp();
            await Assert.ThrowsAsync<CaptureSizeLimitReachedException>(async () =>
            {
                while (true)
                    await recorder.RecordRawAsync(WaferCaptureDirection.Rx, new byte[128], "read", tick, ++tick, "Open");
            });
            Assert.True(recorder.SizeLimitReached);
            Assert.True(recorder.CaptureSizeBytes <= 1024);
        }
    }

    [Fact]
    public async Task ImportRejectsInvalidJsonUnknownVersionAndMismatchedBytes()
    {
        var serializer = new WaferCaptureSerializer();
        var invalid = Path.Combine(_directory, "invalid.mdbcap.json");
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(invalid, "{");
        await Assert.ThrowsAsync<InvalidDataException>(() => serializer.LoadAsync(invalid));

        var unknown = Header() with { FormatVersion = 2 };
        await File.WriteAllTextAsync(invalid, JsonSerializer.Serialize(unknown, WaferCaptureSerializerForTests.Options));
        await Assert.ThrowsAsync<NotSupportedException>(() => serializer.LoadAsync(invalid));

        var badEvent = WaferCaptureEvent.Raw(1, DateTimeOffset.UtcNow, 1, null, WaferCaptureDirection.Rx,
            new byte[] { 0x03 }, "read", 1, null, null, "Open") with { Base64 = Convert.ToBase64String([0x04]) };
        var mismatched = Header() with { Events = [badEvent] };
        await File.WriteAllTextAsync(invalid, JsonSerializer.Serialize(mismatched, WaferCaptureSerializerForTests.Options));
        await Assert.ThrowsAsync<InvalidDataException>(() => serializer.LoadAsync(invalid));
    }

    [Fact]
    public async Task ImportRejectsRawEventWithoutDirectionAndRegressingMonotonicTime()
    {
        Directory.CreateDirectory(_directory);
        var serializer = new WaferCaptureSerializer();
        var path = Path.Combine(_directory, "invalid-events.mdbcap.json");
        var first = WaferCaptureEvent.Raw(1, DateTimeOffset.UtcNow, 10, null,
            WaferCaptureDirection.Rx, new byte[] { 0x00 }, "read", 1, null, null, "Open") with { Direction = null };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(Header() with { Events = [first] },
            WaferCaptureSerializerForTests.Options));
        await Assert.ThrowsAsync<InvalidDataException>(() => serializer.LoadAsync(path));

        var validFirst = first with { Direction = WaferCaptureDirection.Rx };
        var second = WaferCaptureEvent.Raw(2, DateTimeOffset.UtcNow, 9, null,
            WaferCaptureDirection.Rx, new byte[] { 0x01 }, "read", 2, null, null, "Open");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(Header() with { Events = [validFirst, second] },
            WaferCaptureSerializerForTests.Options));
        await Assert.ThrowsAsync<InvalidDataException>(() => serializer.LoadAsync(path));
    }

    [Fact]
    public void AnalysisIsConservativeAndInterpretationNeverClaimsConfirmed()
    {
        var timing = Header().Capture;
        var events = Enumerable.Range(0, 4).Select(index => WaferCaptureEvent.Raw(index + 1,
            timing.StartedAtUtc.AddMilliseconds(index * 100), index * timing.MonotonicFrequency / 10, null,
            WaferCaptureDirection.Rx, new byte[] { 0x00 }, "read", index + 1, null, null, "Open",
            new WaferCaptureInterpreter().Interpret(WaferCaptureDirection.Rx, [0x00]))).ToArray();
        var statistics = new WaferCaptureAnalyzer().Analyze(events, timing);
        Assert.True(statistics.PeriodicRxObservation.Detected);
        Assert.Contains("does not prove", statistics.PeriodicRxObservation.Classification, StringComparison.OrdinalIgnoreCase);
        Assert.All(events, item => Assert.NotEqual(MdbInterpretationConfidence.Confirmed,
            item.PossibleMdbInterpretation?.Confidence));
    }

    [Fact]
    public async Task ConcurrentEventsAreSerializedWithStrictSequence()
    {
        var recorder = await WaferCaptureRecorder.StartAsync(Header(), _directory);
        await using (recorder)
        {
            var tasks = Enumerable.Range(0, 100).Select(async index =>
            {
                var tick = Stopwatch.GetTimestamp();
                if (index % 2 == 0)
                    await recorder.RecordRawAsync(WaferCaptureDirection.Rx, new byte[] { (byte)index }, "read", tick, tick + 1, "Open");
                else await recorder.AddMarkerAsync($"marker-{index}");
            });
            await Task.WhenAll(tasks);
            var artifact = await recorder.StopAsync();
            var path = Path.Combine(_directory, "concurrent.mdbcap.json");
            var serializer = new WaferCaptureSerializer();
            await serializer.ExportAsync(artifact, path);
            var imported = await serializer.LoadAsync(path);
            Assert.Equal(Enumerable.Range(1, 100).Select(value => (long)value), imported.Events.Select(item => item.Sequence));
        }
    }

    [Fact]
    public async Task AsciiHexCaptureStoresExactFormattedTxIncludingTerminator()
    {
        var recorder = await WaferCaptureRecorder.StartAsync(Header(), _directory);
        await using var controller = new WaferDiscoveryCaptureController(new DiscoverySimulatorTransport(), recorder);
        await controller.StartAsync();
        await controller.SendAsync(new byte[] { 0x10, 0xAF }, new SerialWireFormatOptions
        { Format = SerialWireFormat.AsciiHex, Terminator = AsciiHexTerminator.CRLF }, "AsciiProbe");
        var artifact = await controller.StopAsync();
        var path = Path.Combine(_directory, "ascii.mdbcap.json");
        var serializer = new WaferCaptureSerializer();
        await serializer.ExportAsync(artifact, path);
        var imported = await serializer.LoadAsync(path);
        var tx = Assert.Single(imported.Events, item => item.Direction == WaferCaptureDirection.Tx);
        Assert.Equal(System.Text.Encoding.ASCII.GetBytes("10AF\r\n"), tx.GetRawBytes());
    }

    [Fact]
    public async Task ConcurrentStopIsIdempotentAndClosesTransportOnce()
    {
        var transport = new CountingRawTransport();
        var recorder = await WaferCaptureRecorder.StartAsync(Header(), _directory);
        await using var controller = new WaferDiscoveryCaptureController(transport, recorder);
        await controller.StartAsync();

        var artifacts = await Task.WhenAll(controller.StopAsync(), controller.StopAsync(), controller.StopAsync());

        Assert.All(artifacts, artifact => Assert.Equal(artifacts[0], artifact));
        Assert.Equal(1, transport.DisconnectCount);
        var path = Path.Combine(_directory, "concurrent-stop.mdbcap.json");
        var serializer = new WaferCaptureSerializer();
        await serializer.ExportAsync(artifacts[0], path);
        var imported = await serializer.LoadAsync(path);
        Assert.Single(imported.Events, item => item.TransportState == "SerialClosed");
    }

    [Fact]
    public async Task StopWaitsForInFlightWriteAndDoesNotLoseTxEvent()
    {
        var transport = new CountingRawTransport { WriteDelay = TimeSpan.FromMilliseconds(30) };
        var recorder = await WaferCaptureRecorder.StartAsync(Header(), _directory);
        await using var controller = new WaferDiscoveryCaptureController(transport, recorder);
        await controller.StartAsync();
        var send = controller.SendAsync(new byte[] { 0x10 }, new SerialWireFormatOptions(), "ConcurrentTx");
        await Task.Delay(5);
        var stop = controller.StopAsync();
        await Task.WhenAll(send, stop);
        var path = Path.Combine(_directory, "stop-write.mdbcap.json");
        var serializer = new WaferCaptureSerializer();
        await serializer.ExportAsync(await stop, path);
        var imported = await serializer.LoadAsync(path);
        Assert.Contains(imported.Events, item => item.Direction == WaferCaptureDirection.Tx && item.Hex == "10");
    }

    [Fact]
    public async Task ApplicationCloseDisposesActiveCaptureExactlyOnce()
    {
        var transport = new CountingRawTransport();
        var recorder = await WaferCaptureRecorder.StartAsync(Header(), _directory);
        var recorded = new List<WaferCaptureEvent>();
        var controller = new WaferDiscoveryCaptureController(transport, recorder);
        controller.EventRecorded += (_, item) => recorded.Add(item);
        await controller.StartAsync();

        await controller.DisposeAsync();

        Assert.Equal(1, transport.DisconnectCount);
        Assert.Equal(1, transport.DisposeCount);
        Assert.Single(recorded, item => item.TransportState == "SerialClosed");
    }

    [Fact]
    public async Task FailingUiSubscriberCannotStopOrLoseRawEvidence()
    {
        var recorder = await WaferCaptureRecorder.StartAsync(Header(), _directory);
        await using (recorder)
        {
            recorder.EventRecorded += (_, _) => throw new InvalidOperationException("simulated UI failure");
            var tick = Stopwatch.GetTimestamp();
            await recorder.RecordRawAsync(WaferCaptureDirection.Rx, new byte[] { 0x03, 0xFF, 0xFE }, "read", tick, tick + 1, "Open");
            var artifact = await recorder.StopAsync();
            var path = Path.Combine(_directory, "subscriber-failure.mdbcap.json");
            var serializer = new WaferCaptureSerializer();
            await serializer.ExportAsync(artifact, path);
            var imported = await serializer.LoadAsync(path);
            Assert.Equal(new byte[] { 0x03, 0xFF, 0xFE }, Assert.Single(imported.Events).GetRawBytes());
        }
    }

    [Fact]
    public async Task LongCaptureStreamsToSpoolWithoutRetainingEventCollection()
    {
        var recorder = await WaferCaptureRecorder.StartAsync(Header(), _directory, 32 * 1024 * 1024);
        await using (recorder)
        {
            var payload = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
            var tick = Stopwatch.GetTimestamp();
            for (var index = 0; index < 10_000; index++)
                await recorder.RecordRawAsync(WaferCaptureDirection.Rx, payload, "long-read", tick + index, tick + index + 1, "Open");
            var artifact = await recorder.StopAsync();
            Assert.True(artifact.CaptureSizeBytes > 5 * 1024 * 1024);
            Assert.DoesNotContain(typeof(WaferCaptureRecorder).GetFields(System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic), field =>
                    typeof(IEnumerable<WaferCaptureEvent>).IsAssignableFrom(field.FieldType));
            Assert.Equal(10_000, artifact.Header.Statistics.RxEvents);
        }
    }

    private static WaferCaptureDocument Header()
    {
        var now = DateTimeOffset.UtcNow;
        return new WaferCaptureDocument
        {
            CaptureId = Guid.NewGuid().ToString("N"),
            Application = new("MDB Test Bench", "0.1.1"),
            Adapter = new() { Model = "MDB-RS232 PC Adapter", PrintedRevision = "2022061K5" },
            Host = new("Test OS", "Test Version", "x64", Environment.Version.ToString()),
            Serial = new() { Port = null, BaudRate = 9600, DataBits = 8, PollingMode = PollingMode.AdapterManaged },
            Capture = new() { CreatedAtUtc = now, StartedAtUtc = now, MonotonicFrequency = Stopwatch.Frequency }
        };
    }

    public Task InitializeAsync() { Directory.CreateDirectory(_directory); return Task.CompletedTask; }
    public Task DisposeAsync() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); return Task.CompletedTask; }

    private static class WaferCaptureSerializerForTests
    {
        internal static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    private sealed class CountingRawTransport : IRawByteTransport
    {
        private readonly TaskCompletionSource _read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsConnected { get; private set; }
        public int DisconnectCount { get; private set; }
        public int DisposeCount { get; private set; }
        public TimeSpan WriteDelay { get; init; }
        public Task ConnectAsync(CancellationToken cancellationToken = default) { IsConnected = true; return Task.CompletedTask; }
        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            DisconnectCount++; IsConnected = false; return Task.CompletedTask;
        }
        public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default) =>
            await Task.Delay(WriteDelay, cancellationToken);
        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _read.Task.WaitAsync(cancellationToken); return 0;
        }
        public ValueTask DisposeAsync() { DisposeCount++; IsConnected = false; return ValueTask.CompletedTask; }
    }
}
