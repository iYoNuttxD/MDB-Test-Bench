using System.Text.Json;
using System.Text.Json.Serialization;

namespace MdbTestBench.Transport.Configuration;

public sealed class JsonSettingsStore
{
    public const int MaxSettingsFileBytes = 1_048_576;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<AppSettings> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return new AppSettings();
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 16_384, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length > MaxSettingsFileBytes) return new AppSettings();
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options, cancellationToken);
            return Normalize(settings);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or
                                          IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(
        string path,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var temporaryPath = path + $".tmp-{Guid.NewGuid():N}";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                             FileShare.None, bufferSize: 16_384, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static AppSettings Normalize(AppSettings? settings)
    {
        var defaults = new AppSettings();
        if (settings is null) return defaults;
        return settings with
        {
            Language = settings.Language is "en-US" or "pt-BR" ? settings.Language : null,
            SelectedTransport = Enum.IsDefined(settings.SelectedTransport)
                ? settings.SelectedTransport : defaults.SelectedTransport,
            SimulatorBehavior = Enum.IsDefined(settings.SimulatorBehavior)
                ? settings.SimulatorBehavior : defaults.SimulatorBehavior,
            SerialPort = settings.SerialPort is { Length: <= 1_024 } ? settings.SerialPort : string.Empty,
            BaudRate = settings.BaudRate is > 0 and <= 10_000_000 ? settings.BaudRate : defaults.BaudRate,
            DataBits = settings.DataBits is >= 5 and <= 8 ? settings.DataBits : defaults.DataBits,
            StopBits = Enum.IsDefined(settings.StopBits) && settings.StopBits != System.IO.Ports.StopBits.None
                ? settings.StopBits : defaults.StopBits,
            Parity = Enum.IsDefined(settings.Parity) ? settings.Parity : defaults.Parity,
            Handshake = Enum.IsDefined(settings.Handshake) ? settings.Handshake : defaults.Handshake,
            PollingMode = Enum.IsDefined(settings.PollingMode) ? settings.PollingMode : defaults.PollingMode,
            TimeoutMilliseconds = settings.TimeoutMilliseconds is >= 50 and <= 120_000
                ? settings.TimeoutMilliseconds : defaults.TimeoutMilliseconds,
            WireFormat = Enum.IsDefined(settings.WireFormat) ? settings.WireFormat : defaults.WireFormat,
            AsciiHexTerminator = Enum.IsDefined(settings.AsciiHexTerminator)
                ? settings.AsciiHexTerminator : defaults.AsciiHexTerminator,
            CaptureMaximumMegabytes = settings.CaptureMaximumMegabytes is >= 1 and <= 1024
                ? settings.CaptureMaximumMegabytes : defaults.CaptureMaximumMegabytes,
            LastProfileId = settings.LastProfileId is { Length: > 0 and <= 256 }
                ? settings.LastProfileId : defaults.LastProfileId,
            WindowWidth = settings.WindowWidth is >= 920 and <= 10_000 ? settings.WindowWidth : defaults.WindowWidth,
            WindowHeight = settings.WindowHeight is >= 620 and <= 10_000 ? settings.WindowHeight : defaults.WindowHeight
        };
    }
}
