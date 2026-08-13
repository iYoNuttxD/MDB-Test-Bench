# Architecture

MDB Test Bench v0.1.1 is a cross-platform desktop application whose logical role is the VMC/master side of an MDB test setup.

```text
Avalonia Views
  -> MainWindowViewModel / application services
      -> TestEngine
      -> Transport
      -> Core
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

`WaferMdbRs232Transport` still requires an injected `IWaferProtocolCodec`. Its contract accepts an already encoded MDB block and returns an MDB response block; it does not receive semantic command objects. No default codec exists. Until a validated codec is implemented, the UI disables Structured hardware actions and exposes only confirmed Advanced / Adapter Debug raw exchange.

`SimulatedCashlessTransport` implements both logical and raw development paths. Structured requests pass through `MdbCashlessEncoder`/`MdbCashlessDecoder`, and simulated responses are valid MDB response blocks decoded by the same Core decoder. It receives or creates a `VmcSimulator`, so the same Core state machine is exercised by UI flows and headless end-to-end tests. It supports Normal, AlwaysApprove, AlwaysDeny, Timeout, MalformedResponse, and UnexpectedResponse behavior.

`MdbTestBench.TestEngine` executes typed JSON scenarios sequentially, reports expected/received values and duration, writes to the shared structured log sink, supports cancellation, and enforces scenario deadlines.

`MdbTestBench.App` composes services explicitly. View code-behind contains only view initialization, clipboard integration, and window-lifecycle persistence; MDB rules remain outside code-behind.

## State and concurrency

The logical lifecycle is Disconnected → Connected → Reset → Disabled → Enabled → SessionIdle → VendPending → VendApproved/VendDenied → SessionComplete → Enabled. RESET receives ACK, and JUST RESET is a distinct POLL response. Invalid Structured commands are blocked through `VmcStateMachine.CanFire`; Advanced raw actions remain possible only with an explicit warning and confirmation.

All transport/test operations use `async`/`await` and `CancellationToken`. Stateful exchanges are serialized with semaphores. Transports implement `IAsyncDisposable`. No busy-wait, `Thread.Sleep`, database, cloud service, or web backend is used.

In-memory traffic retention is bounded to 10,000 entries. Raw HEX and diagnostic payloads are bounded to 4,096 bytes, serial receive buffers are capped at 65,536 bytes, and scenarios are validated before execution.

## Persistence

`AppPaths` resolves the operating system's per-user local application-data directory. Settings, window size, last profile, custom profiles, future custom scenarios, and exports live outside the application installation directory. Custom profile storage and exported copies use separate directories. Imported JSON is size-limited, validated, and saved under generated identifiers; path containment is enforced for managed files. A saved serial name is cleared from selection when discovery no longer returns it.

## Distribution boundary

The application assembly is versioned as `0.1.1`. Publish scripts produce untrimmed, self-contained, single-file executables so reflective Avalonia behavior is preserved. macOS packaging adds only standard application-bundle metadata; it does not alter Core or transport behavior. Release automation and platform limitations are detailed in [RELEASING.md](RELEASING.md).
