namespace MdbTestBench.Transport.Abstractions;

public enum TransportError
{
    PortNotFound,
    PermissionDenied,
    PortBusy,
    Disconnected,
    Timeout,
    IncompleteData,
    InvalidData,
    ReadFailure,
    WriteFailure,
    Unknown
}

public sealed class TransportException(TransportError error, string message, Exception? innerException = null)
    : IOException(message, innerException)
{
    public TransportError Error { get; } = error;
}
