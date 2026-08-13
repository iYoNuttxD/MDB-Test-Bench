namespace MdbTestBench.Core.Protocol.Cashless;

public static class MdbCashlessAddressing
{
    public static byte GetBaseAddress(MdbCashlessDevice device) => device switch
    {
        MdbCashlessDevice.CashlessDevice1 => 0x10,
        MdbCashlessDevice.CashlessDevice2 => 0x60,
        _ => throw new ArgumentOutOfRangeException(nameof(device))
    };

    public static byte GetCommandByte(MdbCashlessDevice device, MdbCommandType command) =>
        (byte)(GetBaseAddress(device) | GetCommandOffset(command));

    public static bool TryDecode(byte commandByte, out MdbCashlessDevice device, out MdbCommandType command)
    {
        var baseAddress = (byte)(commandByte & 0xF8);
        device = baseAddress switch
        {
            0x10 => MdbCashlessDevice.CashlessDevice1,
            0x60 => MdbCashlessDevice.CashlessDevice2,
            _ => default
        };
        if (baseAddress is not (0x10 or 0x60))
        {
            command = default;
            return false;
        }

        command = (commandByte & 0x07) switch
        {
            0 => MdbCommandType.Reset,
            1 => MdbCommandType.Setup,
            2 => MdbCommandType.Poll,
            3 => MdbCommandType.Vend,
            4 => MdbCommandType.Reader,
            5 => MdbCommandType.Revalue,
            7 => MdbCommandType.Expansion,
            _ => MdbCommandType.Custom
        };
        return command != MdbCommandType.Custom;
    }

    private static byte GetCommandOffset(MdbCommandType command) => command switch
    {
        MdbCommandType.Reset => 0,
        MdbCommandType.Setup => 1,
        MdbCommandType.Poll => 2,
        MdbCommandType.Vend => 3,
        MdbCommandType.Reader => 4,
        MdbCommandType.Revalue => 5,
        MdbCommandType.Expansion => 7,
        _ => throw new ArgumentOutOfRangeException(nameof(command), "Not a standard MDB cashless command.")
    };
}
