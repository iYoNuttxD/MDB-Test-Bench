# MDB Test Bench

Cross-platform desktop application for exercising the VMC/master side of a cashless MDB test setup. Version `0.1.0` runs end-to-end with a deterministic simulator and provides a deliberately constrained serial diagnostic path for the reported Wafer MDB-RS232 revision `2022061K5`.

The simulator is a development tool, not a statement of MDB conformance. The binary and ASCII HEX serial wire formats are experimental representations selected by the operator. No unknown Wafer byte, framing, checksum, response boundary, timing rule, or polling behavior is claimed or invented.

## Functional scope

- Avalonia MVVM UI with Dashboard, Manual, Automatic, Profiles, Logs, and Settings.
- Explicit SIMULATION identity, connection lifecycle, and no automatic serial connection.
- Structured simulator actions guarded by the VMC state machine.
- Advanced raw HEX validation with a 4,096-byte limit and explicit confirmation.
- Cross-platform serial-port discovery; no port names are hardcoded.
- Configurable serial parameters, polling ownership, timeout, binary bytes, ASCII HEX, and terminator.
- Normal, approve, deny, timeout, malformed, and unexpected simulator behaviors.
- Seven built-in asynchronous scenarios with cancellation and structured results.
- Bounded structured traffic log with filtering, pause-view, copy, clear, and TXT/JSON export.
- Level 1, Level 2, Level 3, and custom JSON profiles with independent capability status.

## Architecture

```text
src/
  MdbTestBench.App/          Avalonia views, ViewModels, composition, user persistence
  MdbTestBench.Core/         Logical MDB model, profiles, state machine, HEX, logs
  MdbTestBench.Transport/    Serial, simulator, wire format, Wafer extension seam
  MdbTestBench.TestEngine/   Typed scenarios and asynchronous execution
tests/
  MdbTestBench.Core.Tests/
  MdbTestBench.Transport.Tests/
  MdbTestBench.TestEngine.Tests/
  MdbTestBench.App.Tests/
```

`MdbTestBench.Core` depends on neither Avalonia nor `System.IO.Ports`. `IMdbTransport` separates logical exchange from its origin, and future structured hardware communication still requires an evidence-based `IWaferProtocolCodec`.

## Development

Install the .NET 10 SDK. Clone the repository, then run commands from its root. The application has no database, backend, cloud service, login, BLE integration, or telemetry.

```bash
dotnet restore MDBTestBench.sln
```

## Build

```bash
dotnet build MDBTestBench.sln --configuration Release --no-restore
```

Warnings and nullable analysis are enabled centrally in `Directory.Build.props`.

## Test

All automated tests are hardware-free:

```bash
dotnet test MDBTestBench.sln --configuration Release --no-build
```

The suite covers Core, state machine, profiles, JSON, manual commands, HEX limits, bounded logs, serial configuration and wire formats, simulator lifecycle and behaviors, timeouts, cancellation, scenario validation, and end-to-end `VmcSimulator + TestEngine + SimulatedCashlessTransport` flows.

## Run

```bash
dotnet run --project src/MdbTestBench.App/MdbTestBench.App.csproj
```

Start in Simulator, open Settings, and press Connect. For an approved flow use Automatic → L1 - Approved Vend, or execute Reset → Setup Config → Reader Enable → Wait Session → Vend Request → Vend Success → Session Complete in Manual.

The packaged executable supports a non-GUI distribution smoke test:

```bash
./MDB-Test-Bench --smoke-test
```

## Supported Platforms

Release automation produces self-contained packages for:

- Windows x64 (`win-x64`);
- macOS Apple Silicon (`osx-arm64`);
- macOS Intel (`osx-x64`);
- Linux x64 (`linux-x64`).

The user does not need to install .NET. macOS bundles are unsigned until an Apple Developer identity is supplied outside the repository. Linux requires the native desktop/X11 libraries used by Avalonia; package names vary by distribution.

## Hardware

The reported adapter is Wafer MDB-RS232 revision `2022061K5`. Its host framing and polling behavior remain unconfirmed. Structured hardware commands remain disabled; only operator-confirmed Adapter Debug bytes can be sent. Follow [the hardware checklist](docs/TESTING_WITH_HARDWARE.md) and preserve exact captures before implementing a codec.

## Downloads / Releases

A tag such as `v0.1.0` triggers the release workflow and creates:

```text
MDB-Test-Bench-v0.1.0-windows-x64.zip
MDB-Test-Bench-v0.1.0-macos-arm64.zip
MDB-Test-Bench-v0.1.0-macos-x64.zip
MDB-Test-Bench-v0.1.0-linux-x64.tar.gz
```

No tag or GitHub Release is created automatically during development. Local packaging and the complete release procedure are documented in [RELEASING.md](docs/RELEASING.md).

## Local data

Settings, custom profiles, future custom scenarios, exports, and logs are stored below the operating system's per-user local application-data directory in `MdbTestBench/`. Invalid or oversized JSON falls back safely or is rejected. A saved serial name is selected only when currently discovered.

Further reading: [architecture](docs/ARCHITECTURE.md), [hardware](docs/HARDWARE.md), [MDB scope](docs/MDB_SCOPE.md), and [Wafer integration](docs/WAFER_INTEGRATION.md).
