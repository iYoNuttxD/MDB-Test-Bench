using MdbTestBench.Core.Protocol.Frames;

namespace MdbTestBench.Core.Protocol.Encoding;

public interface IMdbEncoder
{
    ReadOnlyMemory<byte> Encode(MdbFrame frame);
}
