# Architecture

MDB Test Bench v0.1 is a cross-platform desktop application whose logical role is the VMC/master side of an MDB test setup.

```text
Avalonia Views
  -> MainWindowViewModel / application services
      -> TestEngine
      -> Transport
      -> Core
```

## Boundaries

`MdbTestBench.Core` owns logical frames, commands, responses, profiles, capability states, structured logs, safe HEX parsing, semantic manual-command construction, and the VMC state machine. It references neither Avalonia nor `System.IO.Ports`. A manual semantic payload is descriptive data; it is not silently converted into invented Wafer bytes.

`MdbTestBench.Transport` owns I/O and adapter representation. `IMdbTransport` exchanges logical `MdbFrame` objects. `IRawByteTransport` is the byte channel, while `IRawCommandTransport` supports explicitly advanced diagnostics. `SerialTransport` wraps `SerialPort`, translates common lifecycle failures into friendly transport errors, and never opens itself at application startup.

`SerialWireFormatter` implements only user-selected experimental representation:

- `BinaryBytes`: preserves the input bytes;
- `AsciiHex`: uppercase ASCII hexadecimal plus None, CR, LF, or CRLF.

This formatter is entirely outside Core and does not claim to be the Wafer protocol.

`WaferMdbRs232Transport` still requires an injected `IWaferProtocolCodec`. No default codec exists. Until a validated codec is implemented, the UI disables Structured hardware actions and exposes only confirmed Advanced / Adapter Debug raw exchange.

`SimulatedCashlessTransport` implements both logical and raw development paths. It owns the state machine used by the UI and supports Normal, AlwaysApprove, AlwaysDeny, Timeout, MalformedResponse, and UnexpectedResponse behavior.

`MdbTestBench.TestEngine` executes typed JSON scenarios sequentially, reports expected/received values and duration, writes to the shared structured log sink, supports cancellation, and enforces scenario deadlines.

`MdbTestBench.App` composes services explicitly. View code-behind contains only view initialization, clipboard integration, and window-lifecycle persistence; MDB rules remain outside code-behind.

## State and concurrency

The logical lifecycle is Disconnected → Connected → Reset → Disabled → Enabled → SessionIdle → VendPending → VendApproved/VendDenied → SessionComplete → Enabled. Invalid Structured commands are blocked through `VmcStateMachine.CanFire`; Advanced raw actions remain possible only with an explicit warning and confirmation.

All transport/test operations use `async`/`await` and `CancellationToken`. Stateful exchanges are serialized with semaphores. Transports implement `IAsyncDisposable`. No busy-wait, `Thread.Sleep`, database, cloud service, or web backend is used.

## Persistence

`AppPaths` resolves the operating system's per-user local application-data directory. Settings, window size, last profile, custom profiles, future custom scenarios, and exported logs live outside the application installation directory. A saved serial name is cleared from selection when discovery no longer returns it.
