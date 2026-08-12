using MdbTestBench.Transport.Abstractions;
using MdbTestBench.Transport.Serial;

namespace MdbTestBench.Transport.Tests;

public sealed class SerialDiagnosticTransportTests
{
    [Fact]
    public async Task AsciiHexFormattingIsAppliedOnlyAtRawTransportBoundary()
    {
        var raw = new RecordingRawTransport([0x4F, 0x4B]);
        await using var transport = new SerialDiagnosticTransport(raw, new SerialWireFormatOptions
        {
            Format = SerialWireFormat.AsciiHex,
            Terminator = AsciiHexTerminator.CRLF
        }, TimeSpan.FromSeconds(1));

        var result = await transport.ExchangeRawAsync(new byte[] { 0x01, 0xA0 });

        Assert.Equal("01A0\r\n", System.Text.Encoding.ASCII.GetString(raw.Written));
        Assert.Equal(new byte[] { 0x4F, 0x4B }, result.ResponseBytes.ToArray());
    }

    [Fact]
    public void ConstructorRejectsUnboundedReceiveBuffer() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialDiagnosticTransport(
            new RecordingRawTransport([]), new SerialWireFormatOptions(), TimeSpan.FromSeconds(1), 65_537));

    private sealed class RecordingRawTransport(byte[] response) : IRawByteTransport
    {
        public byte[] Written { get; private set; } = [];
        public bool IsConnected { get; private set; } = true;
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }
        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            return Task.CompletedTask;
        }
        public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            Written = data.ToArray();
            return Task.CompletedTask;
        }
        public Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            response.CopyTo(buffer);
            return Task.FromResult(response.Length);
        }
        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }
}
