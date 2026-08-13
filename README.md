# MDB Test Bench

[English](README.md) | [Português (Brasil)](README.pt-BR.md)

Cross-platform desktop application for exercising the VMC/master side of a cashless MDB test setup. Version `0.1.1` includes an MDB/ICP 4.3 cashless encoder/decoder, runs end-to-end with a deterministic simulator, and provides a deliberately constrained serial diagnostic path for the reported Wafer MDB-RS232 revision `2022061K5`.

The simulator is a development tool, not a statement of MDB conformance. The binary and ASCII HEX serial wire formats are experimental representations selected by the operator. No unknown Wafer byte, framing, checksum, response boundary, timing rule, or polling behavior is claimed or invented.

## Functional scope

- Avalonia MVVM UI with Dashboard, Manual, Automatic, Profiles, Logs, Wafer Discovery, and Settings.
- Explicit SIMULATION identity, connection lifecycle, and no automatic serial connection.
- Structured simulator actions guarded by the VMC state machine and encoded as deterministic MDB bytes.
- Cashless Device #1/#2 addressing, MDB checksum, big-endian values, packed-BCD currency, and monetary scaling helpers.
- Typed L1 cashless commands/responses, L2 Revalue, and capability-gated partial L3/Expansion support.
- Advanced raw HEX validation with a 4,096-byte limit and explicit confirmation.
- Cross-platform serial-port discovery; no port names are hardcoded.
- Configurable serial parameters, polling ownership, timeout, binary bytes, ASCII HEX, and terminator.
- Normal, approve, deny, timeout, malformed, and unexpected simulator behaviors.
- Seven built-in asynchronous scenarios with cancellation and structured results.
- Bounded structured traffic log with filtering, pause-view, copy, clear, and TXT/JSON export.
- Level 1, Level 2, Level 3, and custom JSON profiles with independent capability status.
- Wafer Discovery with exact RX-chunk/TX evidence, monotonic deltas, operator markers, manual probes, bounded streaming capture, conservative protocol observations, and offline import/reanalysis.
- Privacy-safe, versioned `*.mdbcap.json` export plus a human-readable TXT summary.
- Runtime-selectable `en-US` / `pt-BR` interface with OS-culture detection and persisted preference.

## Architecture

```text
src/
  MdbTestBench.App/          Avalonia views, ViewModels, composition, user persistence
  MdbTestBench.Core/         MDB/ICP encoder/decoder, logical model, profiles, state machine, HEX, logs
  MdbTestBench.Transport/    Serial, simulator, wire format, Wafer extension seam
  MdbTestBench.TestEngine/   Typed scenarios and asynchronous execution
tests/
  MdbTestBench.Core.Tests/
  MdbTestBench.Transport.Tests/
  MdbTestBench.TestEngine.Tests/
  MdbTestBench.App.Tests/
```

`MdbTestBench.Core` depends on neither Avalonia nor `System.IO.Ports`. `IMdbCashlessEncoder` converts semantic operations into standard MDB blocks; `IMdbCashlessDecoder` produces typed responses while preserving unknown bytes. `IMdbTransport` separates logical exchange from its origin, and structured hardware communication still requires an evidence-based `IWaferProtocolCodec`.

```text
                         UI
                          |
             +------------+------------+
             |                         |
        Structured                  Discovery
             |                         |
       MDB commands                RAW capture
             |                         |
    MdbCashlessEncoder               Serial
             |                         |
       MDB byte blocks                Wafer
             |
    IWaferProtocolCodec (pending validation)
             |
      Wafer transport -> Serial
```

The Core representation includes the standard MDB checksum but not the physical mode/9th bit. Whether revision `2022061K5` expects the checksum in its host payload is still a Wafer codec decision that must be established from hardware evidence.

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

The suite covers MDB command addresses, encoder/decoder vectors, round trips, feature levels, values/checksum, Core, state machine, profiles, JSON, manual commands, HEX limits, bounded logs, serial configuration and wire formats, simulator lifecycle and behaviors, timeouts, cancellation, scenario validation, long streaming captures, concurrent stop/write behavior, byte-exact capture restart round trips, and end-to-end `VmcSimulator + TestEngine + SimulatedCashlessTransport` flows. The synthetic golden capture is `tests/fixtures/simulated-approved-vend.mdbcap.json`; it contains no hardware evidence.

## Run

```bash
dotnet run --project src/MdbTestBench.App/MdbTestBench.App.csproj
```

Start in Simulator, open Settings, and press Connect. For an approved flow use Automatic → L1 - Approved Vend, or, with the Normal simulator behavior, execute Reset → Wait Session/Poll (JUST RESET) → Setup Config → Reader Enable → Wait Session/Poll (BEGIN SESSION) → Vend Request (ACK) → Wait Session/Poll (VEND APPROVED) → Vend Success → Session Complete in Manual. Structured preview shows both semantic fields and the MDB bytes; it never labels those bytes as Wafer framing.

The packaged executable supports a non-GUI distribution smoke test:

```bash
./MDB-Test-Bench --smoke-test
./MDB-Test-Bench --discovery-smoke-test
./MDB-Test-Bench --discovery-smoke-test --capture-output=/absolute/path/sample-simulator.mdbcap.json
```

## Supported Platforms

Release automation produces self-contained packages for:

- Windows x64 (`win-x64`);
- macOS Apple Silicon (`osx-arm64`);
- macOS Intel (`osx-x64`);
- Linux x64 (`linux-x64`).

The user does not need to install .NET. macOS bundles are unsigned until an Apple Developer identity is supplied outside the repository. Linux requires the native desktop/X11 libraries used by Avalonia; package names vary by distribution.

## Hardware

The reported adapter is Wafer MDB-RS232 revision `2022061K5`. Its host framing and polling behavior remain unconfirmed. Structured hardware commands remain disabled. Physical Raw Adapter TX exists only inside Wafer Discovery, requires operator confirmation, and is written to the active capture after wire formatting. Wafer Discovery preserves raw read chunks before interpretation and exports them for offline analysis. Follow [the hardware checklist](docs/TESTING_WITH_HARDWARE.md) and preserve exact captures before implementing a codec.

## Downloads / Releases

Download the current self-contained packages from [GitHub Releases](https://github.com/iYoNuttxD/MDB-Test-Bench/releases/latest). End users do not need to install .NET. Verify every download against the accompanying `SHA256SUMS.txt` before running it.

A tag such as `v0.1.1` triggers the release workflow and creates:

```text
MDB-Test-Bench-v0.1.1-windows-x64.zip
MDB-Test-Bench-v0.1.1-macos-arm64.zip
MDB-Test-Bench-v0.1.1-macos-x64.zip
MDB-Test-Bench-v0.1.1-linux-x64.tar.gz
SHA256SUMS.txt
```

The tag/release remains a deliberate maintainer operation. Local packaging and the complete release procedure are documented in [RELEASING.md](docs/RELEASING.md).

## Local data

Settings, custom profiles, captures, temporary capture spools, exports, and logs are stored below the operating system's per-user local application-data directory in `MdbTestBench/`. Captures default to a configurable 100 MB maximum. Invalid or oversized JSON is rejected without closing the application. A saved serial name is selected only when currently discovered.

User documentation: [English user guide](docs/en-US/USER_GUIDE.md) and [Guia do usuário em português](docs/pt-BR/GUIA_DO_USUARIO.md).

Further reading: [v0.1.1 status](docs/V0.1.1_STATUS.md), [architecture](docs/ARCHITECTURE.md), [localization](docs/LOCALIZATION.md), [capture format](docs/CAPTURE_FORMAT.md), [Discovery mode](docs/WAFER_DISCOVERY.md), [MDB reference](docs/MDB_REFERENCE.md), [implementation status](docs/MDB_IMPLEMENTATION_STATUS.md), [hardware](docs/HARDWARE.md), [MDB scope](docs/MDB_SCOPE.md), and [Wafer integration](docs/WAFER_INTEGRATION.md).
