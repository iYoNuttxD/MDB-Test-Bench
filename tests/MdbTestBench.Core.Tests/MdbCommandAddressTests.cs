using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;

namespace MdbTestBench.Core.Tests;

public sealed class MdbCommandAddressTests
{
    [Theory]
    [InlineData(MdbCashlessDevice.CashlessDevice1, MdbCommandType.Reset, 0x10)]
    [InlineData(MdbCashlessDevice.CashlessDevice1, MdbCommandType.Setup, 0x11)]
    [InlineData(MdbCashlessDevice.CashlessDevice1, MdbCommandType.Poll, 0x12)]
    [InlineData(MdbCashlessDevice.CashlessDevice1, MdbCommandType.Vend, 0x13)]
    [InlineData(MdbCashlessDevice.CashlessDevice1, MdbCommandType.Reader, 0x14)]
    [InlineData(MdbCashlessDevice.CashlessDevice1, MdbCommandType.Revalue, 0x15)]
    [InlineData(MdbCashlessDevice.CashlessDevice1, MdbCommandType.Expansion, 0x17)]
    [InlineData(MdbCashlessDevice.CashlessDevice2, MdbCommandType.Reset, 0x60)]
    [InlineData(MdbCashlessDevice.CashlessDevice2, MdbCommandType.Setup, 0x61)]
    [InlineData(MdbCashlessDevice.CashlessDevice2, MdbCommandType.Poll, 0x62)]
    [InlineData(MdbCashlessDevice.CashlessDevice2, MdbCommandType.Vend, 0x63)]
    [InlineData(MdbCashlessDevice.CashlessDevice2, MdbCommandType.Reader, 0x64)]
    [InlineData(MdbCashlessDevice.CashlessDevice2, MdbCommandType.Revalue, 0x65)]
    [InlineData(MdbCashlessDevice.CashlessDevice2, MdbCommandType.Expansion, 0x67)]
    public void UsesMdb43CashlessAddressTable(MdbCashlessDevice device, MdbCommandType command, byte expected)
    {
        Assert.Equal(expected, MdbCashlessAddressing.GetCommandByte(device, command));
        Assert.True(MdbCashlessAddressing.TryDecode(expected, out var decodedDevice, out var decodedCommand));
        Assert.Equal(device, decodedDevice);
        Assert.Equal(command, decodedCommand);
    }
}
