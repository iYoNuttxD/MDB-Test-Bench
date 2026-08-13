using MdbTestBench.App.Services;
using MdbTestBench.App.ViewModels;
using MdbTestBench.Transport.Configuration;
using MdbTestBench.Transport.Serial;

namespace MdbTestBench.App.Tests;

public sealed class WaferDiscoveryViewModelTests
{
    [Fact]
    public async Task RawConfirmationTextAlwaysNamesPortWireFormatAndTerminator()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mdb-discovery-vm-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var viewModel = new WaferDiscoveryViewModel(new AppPaths(directory), () => new AppSettings
            {
                SelectedTransport = TransportKind.WaferMdbRs232,
                SerialPort = "/dev/test-adapter",
                WireFormat = SerialWireFormat.AsciiHex,
                AsciiHexTerminator = AsciiHexTerminator.CRLF
            }, () => false) { RawHex = "10" };

            Assert.Contains("/dev/test-adapter", viewModel.RawValidation, StringComparison.Ordinal);
            Assert.Contains("AsciiHex", viewModel.RawValidation, StringComparison.Ordinal);
            Assert.Contains("CRLF", viewModel.RawValidation, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
