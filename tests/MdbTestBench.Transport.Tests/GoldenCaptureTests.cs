using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;
using MdbTestBench.Transport.Capture;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MdbTestBench.Transport.Tests;

public sealed class GoldenCaptureTests
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "fixtures", "simulated-approved-vend.mdbcap.json");

    [Fact]
    public async Task SimulatedApprovedVendFixtureMatchesAuthoritativeMdbCodec()
    {
        var document = await new WaferCaptureSerializer().LoadAsync(FixturePath);
        var encoder = new MdbCashlessEncoder();
        var decoder = new MdbCashlessDecoder();
        var tx = document.Events.Where(item => item.Direction == WaferCaptureDirection.Tx).Select(item => item.GetRawBytes()).ToArray();
        var rx = document.Events.Where(item => item.Direction == WaferCaptureDirection.Rx).Select(item => item.GetRawBytes()).ToArray();

        var expectedCommands = new MdbCashlessCommand[]
        {
            new MdbResetCommand(), new MdbPollCommand(),
            new MdbSetupConfigCommand(MdbFeatureLevel.Level1, 0, 0, 0), new MdbReaderEnableCommand(),
            new MdbPollCommand(), new MdbVendRequestCommand(500, 1),
            new MdbVendSuccessCommand(1), new MdbSessionCompleteCommand()
        };
        Assert.Equal(expectedCommands.Select(command => encoder.Encode(command).ToArray()), tx);
        Assert.Equal(new[]
        {
            MdbResponseType.Ack, MdbResponseType.JustReset, MdbResponseType.ReaderConfigData, MdbResponseType.Ack,
            MdbResponseType.BeginSession, MdbResponseType.VendApproved, MdbResponseType.Ack, MdbResponseType.EndSession
        }, rx.Select(bytes => decoder.DecodeResponse(bytes).ResponseType));
        Assert.True(document.PrivacySafe);
        Assert.Contains("Synthetic", document.UserNotes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportThenNewSerializerImportPreservesEveryGoldenEvent()
    {
        var firstProcess = new WaferCaptureSerializer();
        var original = await firstProcess.LoadAsync(FixturePath);
        var directory = Path.Combine(Path.GetTempPath(), "mdb-golden-restart-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var spool = Path.Combine(directory, "events.jsonl");
            var spoolOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
            await File.WriteAllLinesAsync(spool, original.Events.Select(item => JsonSerializer.Serialize(item, spoolOptions)));
            var artifact = new WaferCaptureArtifact(original with { Events = [] }, spool, new FileInfo(spool).Length, false);
            var exported = Path.Combine(directory, "sample-simulator.mdbcap.json");
            await firstProcess.ExportAsync(artifact, exported);

            var afterApplicationRestart = new WaferCaptureSerializer();
            var imported = await afterApplicationRestart.LoadAsync(exported);
            Assert.Equal(original.Events.Count, imported.Events.Count);
            for (var index = 0; index < original.Events.Count; index++)
            {
                Assert.Equal(original.Events[index].Sequence, imported.Events[index].Sequence);
                Assert.Equal(original.Events[index].Direction, imported.Events[index].Direction);
                Assert.Equal(original.Events[index].TimestampUtc, imported.Events[index].TimestampUtc);
                Assert.Equal(original.Events[index].MonotonicTimestamp, imported.Events[index].MonotonicTimestamp);
                Assert.Equal(original.Events[index].GetRawBytes(), imported.Events[index].GetRawBytes());
            }
        }
        finally { Directory.Delete(directory, true); }
    }
}
