namespace MdbTestBench.Core.Protocol.Frames;

public readonly record struct MdbAddress(byte Value, Protocol.MdbDeviceType DeviceType)
{
    public static MdbAddress Vmc => new(0x00, Protocol.MdbDeviceType.Vmc);

    public override string ToString() => $"{DeviceType} (0x{Value:X2})";
}
