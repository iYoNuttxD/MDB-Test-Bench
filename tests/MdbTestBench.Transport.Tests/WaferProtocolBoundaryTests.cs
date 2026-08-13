using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;
using MdbTestBench.Core.Protocol.Frames;
using MdbTestBench.Transport.Abstractions;
using MdbTestBench.Transport.Wafer;

namespace MdbTestBench.Transport.Tests;

public sealed class WaferProtocolBoundaryTests
{
    [Fact]
    public async Task CodecReceivesOnlyEncodedMdbBlockAndReturnsOnlyMdbResponseBlock()
    {
        var raw = new RecordingRawTransport([0xA5, 0x00]);
        await using var transport = new WaferMdbRs232Transport(raw, new PrefixTestCodec(),
            PollingMode.AdapterManaged, TimeSpan.FromSeconds(1));
        var bytes = new MdbCashlessEncoder().Encode(new MdbResetCommand());
        var request = MdbFrame.CommandFrame(MdbAddress.Vmc,
            new MdbAddress(0x10, MdbDeviceType.CashlessDevice1), MdbCommandType.Reset, payload: bytes) with
        {
            CashlessDevice = MdbCashlessDevice.CashlessDevice1,
            WireCommandByte = 0x10
        };

        var response = await transport.ExchangeAsync(request);

        Assert.Equal(new byte[] { 0xA5, 0x10, 0x10 }, raw.Written);
        Assert.Equal(new byte[] { 0x00 }, response.RawBytes.ToArray());
        Assert.Equal(MdbResponseType.Ack, response.Response);
    }

    [Fact]
    public async Task StructuredWaferExchangeRejectsMissingMdbBlock()
    {
        await using var transport = new WaferMdbRs232Transport(
            new RecordingRawTransport([]), new PrefixTestCodec(), PollingMode.AdapterManaged, TimeSpan.FromSeconds(1));
        var request = MdbFrame.CommandFrame(MdbAddress.Vmc,
            new MdbAddress(0x10, MdbDeviceType.CashlessDevice1), MdbCommandType.Reset);

        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.ExchangeAsync(request));
    }

    private sealed class PrefixTestCodec : IWaferProtocolCodec
    {
        public ReadOnlyMemory<byte> EncodeMdbBlock(ReadOnlyMemory<byte> mdbBlock) =>
            new byte[] { 0xA5 }.Concat(mdbBlock.ToArray()).ToArray();

        public ReadOnlyMemory<byte> DecodeMdbBlock(ReadOnlyMemory<byte> adapterData)
        {
            if (adapterData.IsEmpty || adapterData.Span[0] != 0xA5) throw new InvalidDataException("Missing test prefix.");
            return adapterData[1..];
        }
    }

    private sealed class RecordingRawTransport(byte[] response) : IRawByteTransport
    {
        public byte[] Written { get; private set; } = [];
        public bool IsConnected { get; private set; } = true;
        public Task ConnectAsync(CancellationToken cancellationToken = default) { IsConnected = true; return Task.CompletedTask; }
        public Task DisconnectAsync(CancellationToken cancellationToken = default) { IsConnected = false; return Task.CompletedTask; }
        public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        { Written = data.ToArray(); return Task.CompletedTask; }
        public Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { response.CopyTo(buffer); return Task.FromResult(response.Length); }
        public ValueTask DisposeAsync() { IsConnected = false; return ValueTask.CompletedTask; }
    }
}
