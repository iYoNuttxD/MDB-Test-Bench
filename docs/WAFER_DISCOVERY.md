# Wafer Discovery mode

Wafer Discovery is an evidence-collection tool for the reported **Wafer MDB-RS232 PC Adapter**, printed code/revision `2022061K5`. It does not define a Wafer protocol and it does not establish physical MDB compatibility.

## Evidence boundary

```text
Serial read chunk -> append-only temporary spool -> live view
                                          |----> optional MDB interpretation overlay
                                          |----> conservative statistics
                                          `----> versioned .mdbcap.json export
```

Every successful `IRawByteTransport.ReadAsync` result becomes exactly one RX event. Chunks are not concatenated, split, stripped, decoded, or normalized. TX evidence contains the exact bytes passed to the raw transport after the operator-selected `BinaryBytes` or `AsciiHex` formatter and terminator. Interpretation is a separate overlay and can be recalculated when a capture is opened later.

The temporary spool is UTF-8 JSON Lines and is written incrementally. The default limit is 100 MB and is configurable from 1 to 1024 MB. At the limit, capture stops with a warning. The live view retains at most 10,000 display rows; the spool remains the export source, so view bounding does not discard exported evidence.

## Timing limits

UTC timestamps correlate events with operator observations. Deltas use `Stopwatch`, whose frequency is recorded in the capture. Read-operation duration measures the application-visible asynchronous read boundary. `System.IO.Ports` does not expose the physical arrival time of each byte, so first-byte timestamps and inter-byte timing remain `null`; the last-byte timestamp is the time the chunk was returned to the application. These values must not be treated as logic-analyzer precision.

Periodic RX analysis reports median, minimum, and maximum application-observed intervals only when at least four RX chunks have a stable interval distribution. Its classification always remains an observation. Periodicity alone never confirms that the adapter performs MDB POLL.

## Operator workflow

1. Select Simulator or Serial / Wafer in Settings. No port opens automatically.
2. Open Wafer Discovery and verify adapter identity and notes.
3. Press **Start Capture**. The main workbench connection must be disconnected so two consumers cannot race on one port.
4. Add markers whenever physical state changes.
5. Observe unsolicited traffic before transmitting.
6. To transmit, enter HEX in **Raw Adapter / Wafer Debug**, review the wire format and terminator in Settings, check the confirmation box, then send.
7. Save useful inputs as probes. Saving or loading a probe never sends it.
8. Stop and choose **Export for Analysis**. Review the file before sharing.
9. Enter a capture path and choose **Open Capture** to inspect it without hardware. Opening never retransmits bytes.

The Discovery simulator is prominently identified as simulation and creates separate RX chunks, including a split sample, for development validation. It does not model Wafer framing.

## Errors

Open/close state, permission failures, I/O failures, disconnects, cancellation, invalid data, timeouts reported by the transport, and the capture-size limit are represented as capture events where possible. Error messages shown or exported avoid unnecessary platform exception details.

USB VID/PID, manufacturer, product, serial number, and driver fields are optional. v0.1.1 leaves them `null` unless a future cross-platform, reliable discovery source provides them; it does not add fragile platform-specific APIs.
