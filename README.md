# MDB Test Bench

Cross-platform desktop application for exercising the VMC/master side of a cashless MDB test setup. Version 0.1 runs end-to-end with a deterministic simulator and provides a deliberately constrained serial diagnostic path for the reported Wafer MDB-RS232 revision `2022061K5`.

## What is functional

- Avalonia MVVM desktop UI for Dashboard, Manual, Automatic, Profiles, Logs, and Settings.
- Explicit Simulator banner and connection lifecycle; no port opens at startup.
- Structured cashless actions with state-machine validation and semantic payload preview.
- Advanced raw HEX validation and confirmed send path.
- Serial-port discovery through the cross-platform .NET API; no port names are hardcoded.
- Configurable 9600/8/N/1 defaults, polling ownership, timeout, binary bytes, ASCII HEX, and terminator.
- Simulator modes: Normal, AlwaysApprove, AlwaysDeny, Timeout, MalformedResponse, and UnexpectedResponse.
- Seven built-in visual scenarios with asynchronous execution and cancellation.
- Structured traffic logs with filtering, pause-view, search, copy, clear, and TXT/JSON export.
- Standard JSON profiles plus editable custom profiles and capability status values.

The simulator is a development tool, not a statement of full MDB conformance. The serial debug wire formats are experimental representations only. No unknown Wafer byte, framing, checksum, response boundary, or timing rule is implemented.

## Architecture

```text
src/
  MdbTestBench.App/          Avalonia views, ViewModels, composition, user persistence
  MdbTestBench.Core/         Logical MDB model, profiles, state machine, parser, logs
  MdbTestBench.Transport/    Serial, simulator, wire formatting, Wafer extension seam
  MdbTestBench.TestEngine/   JSON scenarios and asynchronous execution
tests/
  MdbTestBench.Core.Tests/
  MdbTestBench.Transport.Tests/
  MdbTestBench.TestEngine.Tests/
```

`MdbTestBench.Core` has no Avalonia or `SerialPort` dependency. `IWaferProtocolCodec` remains mandatory for future logical hardware communication.

## Requirements and commands

- .NET SDK 10.0.x

```bash
dotnet restore MDBTestBench.sln
dotnet build MDBTestBench.sln
dotnet test MDBTestBench.sln
dotnet run --project src/MdbTestBench.App/MdbTestBench.App.csproj
```

Start in Simulator, open Settings, and press Connect. For an approved flow use Automatic → L1 - Approved Vend, or execute Reset → Setup Config → Reader Enable → Wait Session → Vend Request → Vend Success → Session Complete in Manual.

## Local data

Settings, custom profiles, future custom scenarios, and exported logs are stored below the operating system's per-user local application-data directory in `MdbTestBench/`. A serial port saved previously is accepted only when it is currently discovered; otherwise no port is selected.

## Hardware status

The reported adapter is Wafer MDB-RS232 revision `2022061K5`. Its host framing and polling behavior remain unconfirmed. Physical testing must follow [the hardware checklist](docs/TESTING_WITH_HARDWARE.md). Supporting documents: [architecture](docs/ARCHITECTURE.md), [hardware](docs/HARDWARE.md), [MDB scope](docs/MDB_SCOPE.md), and [Wafer integration](docs/WAFER_INTEGRATION.md).
