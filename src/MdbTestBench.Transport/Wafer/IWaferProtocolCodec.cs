using MdbTestBench.Core.Protocol.Frames;

namespace MdbTestBench.Transport.Wafer;

public interface IWaferProtocolCodec
{
    ReadOnlyMemory<byte> Encode(MdbFrame frame);
    MdbFrame Decode(ReadOnlyMemory<byte> data, MdbFrame request);
}
