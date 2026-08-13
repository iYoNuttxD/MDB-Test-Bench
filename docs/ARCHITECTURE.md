# Architecture

MDB Test Bench v0.1.1 is a cross-platform desktop application whose logical role is the VMC/master side of an MDB test setup.

```text
                         UI
                          |
             +------------+------------+
             |                         |
        Structured                  Discovery
             |                         |
       MDB Commands                RAW capture
             |                         |
    MdbCashlessEncoder               Serial
             |                         |
       MDB byte frames                Wafer
             |
      IWaferProtocolCodec
             |
      Wafer transport
             |
           Serial
```

## Boundaries

`MdbTestBench.Core` owns logical frames, standard MDB cashless commands/responses, profiles, capability states, structured logs, safe HEX parsing, semantic manual-command construction, and the VMC state machine. It references neither Avalonia nor `System.IO.Ports`.

The standard protocol path is explicit:

```text
MdbCashlessCommand
  -> IMdbCashlessEncoder
  -> address/command + data + MDB checksum (8-bit bytes)
  -> transport/adapter boundary

adapter boundary
  -> MDB response data + checksum
  -> IMdbCashlessDecoder
  -> typed MdbCashlessResponse (original bytes retained)
```

`MdbCashlessAddressing` selects Cashless Device #1 or #2 without duplicating command implementations. `MdbCashlessBinary`, `MdbPackedBcdCurrencyCode`, `MdbMonetaryScale`, and `MdbChecksum` centralize byte order and value rules. `MdbFrame.Source` and `Destination` are logical endpoints; `RawBytes` is the actual 8-bit MDB block and `WireCommandByte` is explicit. The logical VMC address is never prepended as a transmitted `00` byte.

The encoder emits binary MDB bytes, never ASCII HEX. It includes the standard checksum. Mode/9th-bit signaling is physical MDB behavior and is not represented or driven by Core.

`MdbTestBench.Transport` owns I/O and adapter representation. `IMdbTransport` exchanges logical `MdbFrame` objects. `IRawByteTransport` is the byte channel, while `IRawCommandTransport` supports explicitly advanced diagnostics. `SerialTransport` wraps `SerialPort`, translates common lifecycle failures into friendly transport errors, and never opens itself at application startup.

`SerialWireFormatter` implements only user-selected experimental representation:

- `BinaryBytes`: preserves the input bytes;
- `AsciiHex`: uppercase ASCII hexadecimal plus None, CR, LF, or CRLF.

This formatter is entirely outside Core and does not claim to be the Wafer protocol.

Discovery capture is also a Transport responsibility. `WaferDiscoveryCaptureController` reads `IRawByteTransport` directly and writes each returned RX chunk, each exact formatted TX payload, lifecycle event, error, and operator marker to `WaferCaptureRecorder`. The recorder uses an append-only bounded temporary spool; it never infers frame boundaries. `WaferCaptureInterpreter` adds a replaceable MDB interpretation overlay, while `WaferCaptureAnalyzer` produces conservative length/delimiter/appearance/periodicity observations. Neither mutates raw evidence. `WaferCaptureSerializer` creates and validates portable format-version-1 `.mdbcap.json` files.

`WaferMdbRs232Transport` still requires an injected `IWaferProtocolCodec`. Its contract accepts an already encoded MDB block and returns an MDB response block; it does not receive semantic command objects. No default codec exists. Until a validated codec is implemented, the UI disables Structured hardware actions and exposes only confirmed Advanced / Adapter Debug raw exchange.

`SimulatedCashlessTransport` implements both logical and raw development paths. Structured requests pass through `MdbCashlessEncoder`/`MdbCashlessDecoder`, and simulated responses are valid MDB response blocks decoded by the same Core decoder. It receives or creates a `VmcSimulator`, so the same Core state machine is exercised by UI flows and headless end-to-end tests. It supports Normal, AlwaysApprove, AlwaysDeny, Timeout, MalformedResponse, and UnexpectedResponse behavior.

`MdbTestBench.TestEngine` executes typed JSON scenarios sequentially, reports expected/received values and duration, writes to the shared structured log sink, supports cancellation, and enforces scenario deadlines. Its primary built-in path maps each semantic step through an injectable `IMdbCashlessEncoder`; the simulator decodes that command block, encodes its response block and decodes the response before assertions and state transitions.

`MdbTestBench.App` composes services explicitly. `MainWindowViewModel` retains only navigation, shared connection/settings composition and dashboard projections. Page behavior is divided among `Dashboard` projections, `ManualViewModel`, `AutomaticViewModel`, `ProfilesViewModel`, `LogsViewModel`, `WaferDiscoveryViewModel`, and Settings properties. The cohesive Discovery page remains larger because it owns one capture lifecycle, not unrelated application features. View code-behind contains only view initialization, clipboard integration, and window-lifecycle persistence; MDB rules remain outside code-behind.

Application log/status, structured MDB traffic log, and raw adapter capture are separate data products. The first two use the bounded `IMdbLogSink`; Discovery evidence uses its own append-only spool and capture schema. Hardware raw writes are not exposed through `WorkbenchSession`: they are available only through the Discovery controller, which applies the selected wire representation, transmits, and records the exact on-wire bytes.

## State and concurrency

The logical lifecycle is Disconnected → Connected → Reset → Disabled → Enabled → SessionIdle → VendPending → VendApproved/VendDenied → SessionComplete → Enabled. RESET receives ACK, and JUST RESET is a distinct POLL response. Invalid Structured commands are blocked through `VmcStateMachine.CanFire`; Advanced raw actions remain possible only with an explicit warning and confirmation.

All transport/test operations use `async`/`await` and `CancellationToken`. Stateful exchanges are serialized with semaphores. Transports implement `IAsyncDisposable`. Discovery rejects starting while the normal workbench owns a connection, preventing competing reads from one serial port. Capture stop is idempotent, waits for any in-flight write, closes the transport once, and finalizes one artifact; subscriber/UI failure cannot interrupt spool persistence. No busy-wait, `Thread.Sleep`, database, cloud service, or web backend is used.

In-memory traffic retention and the Discovery live view are bounded to 10,000 entries. Raw HEX and diagnostic payloads are bounded to 4,096 bytes, serial receive buffers are capped at 65,536 bytes, capture spools default to 100 MB, imports default to 100 MB/1,000,000 events, and scenarios are validated before execution. Capture summary analysis streams the spool instead of retaining event objects.

## Persistence

`AppPaths` resolves the operating system's per-user local application-data directory. Settings, window size, last profile, custom profiles, captures, capture spools, and exports live outside the application installation directory. Custom profile storage and exported copies use separate directories. Imported JSON is size-limited and validated; managed exports use generated safe names. A saved serial name is cleared from selection when discovery no longer returns it. Privacy-safe capture metadata excludes username, home directory, hostname and network addresses by default.

## Distribution boundary

The application assembly is versioned as `0.1.1`. Publish scripts produce untrimmed, self-contained, single-file executables so reflective Avalonia behavior is preserved. macOS packaging adds only standard application-bundle metadata; it does not alter Core or transport behavior. Release automation and platform limitations are detailed in [RELEASING.md](RELEASING.md).
