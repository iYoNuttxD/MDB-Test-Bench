using MdbTestBench.App.Services;
using MdbTestBench.Core.Logging;
using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Frames;
using MdbTestBench.Transport.Configuration;
using MdbTestBench.Transport.Simulation;

namespace MdbTestBench.App.Tests;

public sealed class WorkbenchSessionTests
{
    [Fact]
    public async Task DisconnectWaitsForInFlightExchangeAndLeavesSessionDisconnected()
    {
        await using var session = new WorkbenchSession(new InMemoryMdbLogSink());
        await session.ConnectAsync(new AppSettings
        {
            SelectedTransport = TransportKind.Simulated,
            SimulatorBehavior = SimulatorBehavior.Normal,
            TimeoutMilliseconds = 1_000
        });
        var request = MdbFrame.CommandFrame(MdbAddress.Vmc,
            new MdbAddress(0x10, MdbDeviceType.CashlessDevice1), MdbCommandType.Reset);

        var exchange = session.ExchangeAsync(request);
        await Task.Delay(5);
        var disconnect = session.DisconnectAsync();
        var response = await exchange;
        await disconnect;

        Assert.Equal(MdbResponseType.JustReset, response.Response);
        Assert.False(session.IsConnected);
        Assert.Equal("Disconnected", session.State);
    }
}
