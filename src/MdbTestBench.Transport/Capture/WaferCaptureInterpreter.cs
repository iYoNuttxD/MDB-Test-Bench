using MdbTestBench.Core.Protocol.Cashless;

namespace MdbTestBench.Transport.Capture;

public sealed class WaferCaptureInterpreter(IMdbCashlessDecoder? decoder = null)
{
    private readonly IMdbCashlessDecoder _decoder = decoder ?? new MdbCashlessDecoder();

    public MdbCaptureInterpretation Interpret(WaferCaptureDirection direction, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return Unknown("Empty chunk");
        try
        {
            if (direction == WaferCaptureDirection.Tx)
            {
                var decoded = _decoder.DecodeCommand(bytes);
                return decoded.Command is null
                    ? Unknown(decoded.Error ?? "Unknown MDB command")
                    : new(decoded.Command.GetType().Name, MdbInterpretationConfidence.Possible, decoded.Command.CommandType.ToString());
            }

            var response = _decoder.DecodeResponse(bytes);
            return response switch
            {
                MdbMalformedCashlessResponse malformed => Unknown(malformed.Error),
                MdbUnknownCashlessResponse => Unknown("Unknown MDB cashless response"),
                UnknownExpansionResponse => Unknown("Unknown MDB expansion response"),
                MdbAckResponse => new("ACK", MdbInterpretationConfidence.Possible, response.ResponseType.ToString()),
                MdbNakResponse => new("NAK", MdbInterpretationConfidence.Possible, response.ResponseType.ToString()),
                _ => new(response.ResponseType.ToString(), MdbInterpretationConfidence.Likely, response.GetType().Name)
            };
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Unknown(exception.Message);
        }
    }

    private static MdbCaptureInterpretation Unknown(string description) =>
        new(description, MdbInterpretationConfidence.Unknown);
}
