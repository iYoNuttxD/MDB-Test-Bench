# Architecture

MDB Test Bench v0.1 is a cross-platform desktop application whose logical role is the VMC/master side of an MDB test setup. The dependency direction is deliberately one-way:

```text
App (Avalonia/MVVM)
  -> TestEngine
  -> Transport
  -> Core
```

`MdbTestBench.Core` has no Avalonia or serial dependency. It owns logical MDB concepts, profiles, capabilities, structured log records, and the VMC state machine. The encoding and decoding namespaces define extension seams; they do not claim a physical wire format.

`MdbTestBench.Transport` owns asynchronous I/O. `IMdbTransport` exchanges logical `MdbFrame` instances. `SerialTransport` is only a raw byte channel. `WaferMdbRs232Transport` composes that channel with `IWaferProtocolCodec`, which must be supplied after the adapter protocol is validated. `SimulatedCashlessTransport` is a hardware-free deterministic implementation.

`MdbTestBench.TestEngine` loads JSON scenarios and runs their steps sequentially with cancellation, a scenario deadline, expected-response validation, and structured logging.

`MdbTestBench.App` is an Avalonia shell using MVVM. It performs explicit composition at startup and does not open a serial port automatically. View code-behind contains only unavoidable view initialization.

## Key boundaries

- Feature Level and capabilities are independent. A Level 1 profile can explicitly advertise selected optional capabilities.
- Polling ownership belongs to transport configuration/capabilities, not to arbitrary UI or domain code.
- Logical MDB frames are distinct from Wafer serial frames.
- All transport APIs are asynchronous and cancellation-aware.
- No database, cloud service, web backend, or OS-specific domain API is used.

## Lifecycle

Transports implement `IAsyncDisposable`. The owner must disconnect or dispose them. Scenario deadlines use linked cancellation tokens, and the simulator serializes stateful exchanges with a semaphore rather than busy-waiting.
