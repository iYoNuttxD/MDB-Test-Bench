using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;
using MdbTestBench.Core.Protocol.Frames;
using MdbTestBench.Core.Vmc;
using MdbTestBench.Transport.Simulation;

namespace MdbTestBench.Transport.Tests;

public sealed class SimulatedCashlessTransportTests
{
    private static readonly MdbAddress Cashless = new(0x10, MdbDeviceType.CashlessDevice1);

    [Fact]
    public async Task CompleteApprovedSequence_SucceedsWithoutHardware()
    {
        await using var transport = new SimulatedCashlessTransport();
        await transport.ConnectAsync();

        Assert.Equal(MdbResponseType.Ack, (await Send(transport, MdbCommandType.Reset)).Response);
        Assert.Equal(MdbResponseType.JustReset, (await Send(transport, MdbCommandType.Poll)).Response);
        Assert.Equal(MdbResponseType.ReaderConfigData, (await Send(transport, MdbCommandType.Setup)).Response);
        Assert.Equal(MdbResponseType.Ack, (await Send(transport, MdbCommandType.Reader, MdbSubcommandType.Enable)).Response);
        Assert.Equal(MdbResponseType.BeginSession, (await Send(transport, MdbCommandType.Poll)).Response);
        Assert.Equal(MdbResponseType.Ack, (await Send(transport, MdbCommandType.Vend, MdbSubcommandType.VendRequest)).Response);
        Assert.Equal(MdbResponseType.VendApproved, (await Send(transport, MdbCommandType.Poll)).Response);
        Assert.Equal(MdbResponseType.Ack, (await Send(transport, MdbCommandType.Vend, MdbSubcommandType.VendSuccess)).Response);
        Assert.Equal(MdbResponseType.EndSession, (await Send(transport, MdbCommandType.Vend, MdbSubcommandType.SessionComplete)).Response);
        Assert.Equal(VmcState.Enabled, transport.State);
    }

    [Fact]
    public async Task DeniedVend_CanCompleteSession()
    {
        await using var transport = new SimulatedCashlessTransport(
            new SimulatedCashlessOptions { Behavior = SimulatorBehavior.AlwaysDeny });
        await transport.ConnectAsync();
        await Send(transport, MdbCommandType.Reset);
        await Send(transport, MdbCommandType.Poll);
        await Send(transport, MdbCommandType.Setup);
        await Send(transport, MdbCommandType.Reader, MdbSubcommandType.Enable);
        await Send(transport, MdbCommandType.Poll);

        var denied = await Send(transport, MdbCommandType.Vend, MdbSubcommandType.VendRequest);
        var ended = await Send(transport, MdbCommandType.Vend, MdbSubcommandType.SessionComplete);

        Assert.Equal(MdbResponseType.VendDenied, denied.Response);
        Assert.Equal(MdbResponseType.EndSession, ended.Response);
    }

    [Fact]
    public async Task InvalidSequence_Throws()
    {
        await using var transport = new SimulatedCashlessTransport();
        await transport.ConnectAsync();

        await Assert.ThrowsAsync<InvalidVmcTransitionException>(
            () => Send(transport, MdbCommandType.Vend, MdbSubcommandType.VendRequest));
    }

    [Fact]
    public async Task MaxMinSetupIsBlockedUntilConfigCompletes()
    {
        await using var transport = new SimulatedCashlessTransport();
        await transport.ConnectAsync();
        await Send(transport, MdbCommandType.Reset);
        await Send(transport, MdbCommandType.Poll);

        Assert.False(transport.CanExchange(MdbFrame.CommandFrame(MdbAddress.Vmc, Cashless,
            MdbCommandType.Setup, MdbSubcommandType.SetupMaxMinPrices)));
        await Assert.ThrowsAsync<InvalidVmcTransitionException>(() =>
            Send(transport, MdbCommandType.Setup, MdbSubcommandType.SetupMaxMinPrices));
    }

    [Fact]
    public async Task Exchange_ObservesCallerCancellation()
    {
        await using var transport = new SimulatedCashlessTransport(
            new SimulatedCashlessOptions { ResponseDelay = TimeSpan.FromSeconds(1) });
        await transport.ConnectAsync();
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Send(transport, MdbCommandType.Reset, cancellationToken: source.Token));
    }

    [Fact]
    public async Task Exchange_ConvertsInternalDeadlineToTimeout()
    {
        await using var transport = new SimulatedCashlessTransport(new SimulatedCashlessOptions
        {
            ResponseDelay = TimeSpan.FromSeconds(1),
            OperationTimeout = TimeSpan.FromMilliseconds(20)
        });
        await transport.ConnectAsync();

        await Assert.ThrowsAsync<TimeoutException>(() => Send(transport, MdbCommandType.Reset));
    }

    [Fact]
    public async Task StructuredExchangeUsesMdbEncoderAndDecoderForDevice2()
    {
        await using var transport = new SimulatedCashlessTransport();
        await transport.ConnectAsync();
        var encoder = new MdbCashlessEncoder();
        var requestBytes = encoder.Encode(new MdbResetCommand(MdbCashlessDevice.CashlessDevice2));
        var request = MdbFrame.CommandFrame(MdbAddress.Vmc,
            new MdbAddress(0x60, MdbDeviceType.CashlessDevice2), MdbCommandType.Reset, payload: requestBytes) with
        {
            CashlessDevice = MdbCashlessDevice.CashlessDevice2,
            WireCommandByte = 0x60
        };

        var reset = await transport.ExchangeAsync(request);
        var poll = await transport.ExchangeAsync(MdbFrame.CommandFrame(MdbAddress.Vmc,
            request.Destination, MdbCommandType.Poll, payload: encoder.Encode(new MdbPollCommand(MdbCashlessDevice.CashlessDevice2))));

        Assert.Equal(new byte[] { 0x00 }, reset.RawBytes.ToArray());
        Assert.Equal(new byte[] { 0x00, 0x00 }, poll.RawBytes.ToArray());
        Assert.Equal(MdbDeviceType.CashlessDevice2, reset.Source.DeviceType);
        Assert.Equal(MdbCashlessDevice.CashlessDevice2, reset.CashlessDevice);
    }

    private static Task<MdbFrame> Send(
        SimulatedCashlessTransport transport,
        MdbCommandType command,
        MdbSubcommandType subcommand = MdbSubcommandType.None,
        CancellationToken cancellationToken = default) =>
        transport.ExchangeAsync(MdbFrame.CommandFrame(MdbAddress.Vmc, Cashless, command, subcommand), cancellationToken);
}
