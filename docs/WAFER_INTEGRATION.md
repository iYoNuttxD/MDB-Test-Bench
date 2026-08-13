# Wafer MDB-RS232 integration

The reported hardware is a Wafer MDB-RS232, revision `2022061K5`. Its exact host protocol has not been validated.

Material about some newer adapters indicates that polling may be performed internally. That does not establish the behavior of revision `2022061K5`. Therefore Settings exposes `AdapterManaged` (default for initial modern-adapter tests) and `HostManaged`; no unconditional POLL loop exists.

## Implemented extension seam

- `IMdbTransport` isolates logical MDB exchange.
- `IRawByteTransport` isolates serial bytes.
- `IWaferProtocolCodec` transforms an encoded MDB block to validated adapter bytes and adapter response bytes back to an MDB block.
- `WaferMdbRs232Transport` composes serial I/O with that codec.
- No production/default Wafer codec is registered.

The Core now produces standard MDB cashless blocks (`address/command + data + MDB checksum`) and decodes standard reader response blocks. This does not answer whether revision `2022061K5` expects the host to include or omit that checksum, how it represents the MDB mode/9th bit, or what envelope it uses. Those transformations belong exclusively in `IWaferProtocolCodec`.

Structured hardware commands are intentionally disabled in the UI until the codec is validated. Advanced / Adapter Debug physical transmission exists only in Wafer Discovery, where the operator sees port, input bytes, `BinaryBytes`/`AsciiHex`, None/CR/LF/CRLF terminator and a warning, confirms the transmission, and captures the exact formatted TX. The normal workbench session cannot perform a raw hardware write. These settings are experimental probes, not protocol claims.

Wafer Discovery is the evidence path used before a codec exists. It captures actual serial read chunks before interpretation, exact formatted TX bytes, monotonic event deltas, markers, lifecycle/errors, optional operator-saved probes, and a recalculable possible-MDB overlay. It exports the versioned privacy-safe format documented in [CAPTURE_FORMAT.md](CAPTURE_FORMAT.md). Repeated prefixes/suffixes, delimiters, traffic appearance and periodic RX are statistical observations only. They do not establish framing or POLL ownership.

Physical tests must determine serial parameters, initialization, byte representation, terminator, framing, whether MDB checksum bytes are passed through, response boundaries, incomplete-data behavior, error behavior, timing, mode-bit handling performed by the adapter, and polling ownership. Record evidence in [TESTING_WITH_HARDWARE.md](TESTING_WITH_HARDWARE.md), export the `.mdbcap.json`, and obtain repeatable review before implementing a codec.

The distributed v0.1 packages therefore contain no registered production codec. A successful application smoke test or simulator scenario says nothing about revision `2022061K5` compatibility.
