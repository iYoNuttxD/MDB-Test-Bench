using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Frames;
using MdbTestBench.Core.Vmc;
using MdbTestBench.Transport.Simulation;

namespace MdbTestBench.Transport.Tests;

public sealed class SimulatorBehaviorTests
{
    private static readonly MdbAddress Cashless = new(0x10, MdbDeviceType.CashlessDevice1);

    [Theory]
    [InlineData(SimulatorBehavior.MalformedResponse)]
    [InlineData(SimulatorBehavior.UnexpectedResponse)]
    public async Task ErrorModesReturnUnknownWithoutCrashing(SimulatorBehavior behavior)
    {
        await using var simulator = new SimulatedCashlessTransport(new SimulatedCashlessOptions { Behavior = behavior });
        await simulator.ConnectAsync();
        var response = await simulator.ExchangeAsync(MdbFrame.CommandFrame(MdbAddress.Vmc, Cashless, MdbCommandType.Reset));
        Assert.Equal(MdbResponseType.Unknown, response.Response);
    }

    [Fact]
    public async Task DisconnectAndReconnectResetLifecycle()
    {
        await using var simulator = new SimulatedCashlessTransport();
        await simulator.ConnectAsync();
        await simulator.ExchangeAsync(MdbFrame.CommandFrame(MdbAddress.Vmc, Cashless, MdbCommandType.Reset));
        await simulator.DisconnectAsync();
        await simulator.ConnectAsync();

        Assert.True(simulator.IsConnected);
        Assert.Equal(VmcState.Connected, simulator.State);
    }

    [Fact]
    public async Task RawPayloadOverTransportLimitIsRejected()
    {
        await using var simulator = new SimulatedCashlessTransport();
        await simulator.ConnectAsync();

        var exception = await Assert.ThrowsAsync<MdbTestBench.Transport.Abstractions.TransportException>(
            () => simulator.ExchangeRawAsync(new byte[4_097]));

        Assert.Equal(MdbTestBench.Transport.Abstractions.TransportError.InvalidData, exception.Error);
    }
}
