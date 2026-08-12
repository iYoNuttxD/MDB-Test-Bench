# Wafer MDB-RS232 integration

The reported hardware is a Wafer MDB-RS232, revision `2022061K5`. Its exact host protocol has not been validated.

Material about some newer adapters indicates that polling may be performed internally. That does not establish the behavior of revision `2022061K5`. Therefore Settings exposes `AdapterManaged` (default for initial modern-adapter tests) and `HostManaged`; no unconditional POLL loop exists.

## Implemented extension seam

- `IMdbTransport` isolates logical MDB exchange.
- `IRawByteTransport` isolates serial bytes.
- `IWaferProtocolCodec` must encode and decode a validated adapter protocol.
- `WaferMdbRs232Transport` composes serial I/O with that codec.
- No production/default Wafer codec is registered.

Structured hardware commands are intentionally disabled in the UI until the codec is validated. Advanced / Adapter Debug can send user-confirmed data as `BinaryBytes` or `AsciiHex`, with None/CR/LF/CRLF terminators. Those settings are experimental probes, not protocol claims.

Physical tests must determine serial parameters, initialization, byte representation, terminator, framing, checksums, response boundaries, incomplete-data behavior, error behavior, timing, and polling ownership. Record evidence in [TESTING_WITH_HARDWARE.md](TESTING_WITH_HARDWARE.md) before implementing a codec.
