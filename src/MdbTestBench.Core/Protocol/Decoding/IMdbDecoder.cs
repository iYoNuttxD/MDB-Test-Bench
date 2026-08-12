using MdbTestBench.Core.Protocol.Frames;

namespace MdbTestBench.Core.Protocol.Decoding;

public interface IMdbDecoder
{
    MdbFrame Decode(ReadOnlyMemory<byte> payload, MdbAddress source, MdbAddress destination);
}
