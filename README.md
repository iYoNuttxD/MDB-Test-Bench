# MDB Test Bench

Cross-platform desktop foundation for simulating the VMC/master side of an MDB test setup. The v0.1 architecture targets QA and development workflows for a proprietary cashless MDB device connected through a Wafer MDB-RS232 adapter.

## Stack

- C# and .NET 10
- Avalonia UI with MVVM
- xUnit
- JSON profiles, settings, and scenarios
- macOS, Windows, and Linux targets

## Projects

```text
src/
  MdbTestBench.App/          Avalonia shell and composition root
  MdbTestBench.Core/         Logical MDB model, profiles, capabilities, VMC state, logs
  MdbTestBench.Transport/    Serial, simulated transport, Wafer integration seam
  MdbTestBench.TestEngine/   JSON scenarios and asynchronous execution
tests/
  MdbTestBench.Core.Tests/
  MdbTestBench.Transport.Tests/
  MdbTestBench.TestEngine.Tests/
```

The simulator is deliberately a development aid, not a statement of full MDB compliance. No unverified protocol for Wafer revision `2022061K5` is implemented. See [architecture](docs/ARCHITECTURE.md), [hardware](docs/HARDWARE.md), [MDB scope](docs/MDB_SCOPE.md), and [Wafer integration](docs/WAFER_INTEGRATION.md).

## Requirements

- .NET SDK 10.0.x

## Build and run

```bash
dotnet restore MDBTestBench.sln
dotnet build MDBTestBench.sln
dotnet test MDBTestBench.sln
dotnet run --project src/MdbTestBench.App/MdbTestBench.App.csproj
```

The application starts disconnected and does not open a serial port automatically. Safe defaults select the simulated transport. Local settings containing machine-specific port names should be stored as `settings.local.json`, which is ignored by Git.

## Current UI

The functional Avalonia shell navigates among Dashboard, Manual, Automatic, Profiles, Logs, and Settings placeholders. MDB behavior remains outside view code-behind.

## Hardware status

The Wafer MDB-RS232 revision is reported as `2022061K5`, but its exact host framing and polling behavior require documentation and physical validation. The code therefore requires an injected `IWaferProtocolCodec` and explicitly models adapter-managed versus host-managed polling.
