namespace MdbTestBench.Transport.Wafer;

public interface IWaferProtocolCodec
{
    ReadOnlyMemory<byte> EncodeMdbBlock(ReadOnlyMemory<byte> mdbBlock);
    ReadOnlyMemory<byte> DecodeMdbBlock(ReadOnlyMemory<byte> adapterData);
}
