# Wafer MDB-RS232 integration

The reported hardware is a Wafer MDB-RS232, revision `2022061K5`.

The exact host protocol for this revision still needs to be validated. Public material for some newer MDB-RS232 versions indicates that polling may be performed internally by the adapter. That does not prove the behavior of revision `2022061K5`.

Consequently:

- no unknown framing, bytes, checksums, responses, or timing have been invented;
- the Core models logical MDB independently from serial/Wafer framing;
- `IWaferProtocolCodec` is the replaceable boundary for the validated adapter protocol;
- `PollingMode` supports `AdapterManaged` and `HostManaged`;
- transport capabilities expose the selected and supported polling modes;
- POLL is not scattered or hardcoded as an unconditional background operation.

Physical tests are required to establish serial settings, framing, initialization, polling ownership, error behavior, response boundaries, timing, and revision-specific behavior. Capture evidence with a safe test setup and compare it with authoritative documentation before implementing a production codec.
