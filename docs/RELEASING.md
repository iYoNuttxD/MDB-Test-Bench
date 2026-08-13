# Releasing MDB Test Bench

Version `0.1.1` is defined centrally in `Directory.Build.props`. Creating packages is allowed during validation; creating or pushing the Git tag is a deliberate maintainer action.

## Release preflight

Use a clean, reviewed checkout and the .NET 10 SDK:

```bash
dotnet restore MDBTestBench.sln
dotnet build MDBTestBench.sln --configuration Release --no-restore
dotnet test MDBTestBench.sln --configuration Release --no-build
dotnet list MDBTestBench.sln package --vulnerable --include-transitive
```

Review `git status`, release notes, version metadata, and the Wafer limitations. Automated tests use no hardware and do not establish physical compatibility.

## Local packages

On macOS or Linux, package one runtime at a time:

```bash
./scripts/package.sh win-x64 0.1.1
./scripts/package.sh osx-arm64 0.1.1
./scripts/package.sh osx-x64 0.1.1
./scripts/package.sh linux-x64 0.1.1
```

On Windows PowerShell 7:

```powershell
./scripts/package.ps1 -RuntimeIdentifier win-x64 -Version 0.1.1
```

The scripts run `dotnet publish` as self-contained, single-file, untrimmed output and write disposable intermediates below `artifacts/publish` and `artifacts/staging`. Final packages are placed in `artifacts/packages`:

```text
MDB-Test-Bench-v0.1.1-windows-x64.zip
MDB-Test-Bench-v0.1.1-macos-arm64.zip
MDB-Test-Bench-v0.1.1-macos-x64.zip
MDB-Test-Bench-v0.1.1-linux-x64.tar.gz
```

Cross-compilation verifies publication and binary architecture. A package's executable smoke test runs only when the target runtime is native to the current host; GitHub Actions supplies native Windows, macOS, and Linux runners.

## Direct publish commands

Use the following pattern when inspecting unarchived output:

```bash
dotnet publish src/MdbTestBench.App/MdbTestBench.App.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishTrimmed=false \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o artifacts/publish/linux-x64
```

Replace `linux-x64` with `win-x64`, `osx-arm64`, or `osx-x64`. The packaging script additionally builds `MDB Test Bench.app` and its `Info.plist` for macOS.

## Test a package before publishing

1. Extract the package into a new temporary directory.
2. Confirm the archive contains the expected executable or `.app`, not absolute paths.
3. Run `MDB-Test-Bench --smoke-test` and require exit code zero.
4. Start the GUI normally; verify all seven navigation pages.
5. Connect the Simulator, execute L1 Initialization and Approved Vend, cancel a running timeout scenario, validate raw HEX rejection, export logs, and restart to verify settings.
6. Confirm SIMULATION remains visibly labelled and no serial port opens on startup.
7. On a native machine for every target, repeat launch and smoke testing. Cross-publish alone is not native runtime validation.

For the capture-format gate, generate and immediately re-import a simulator sample:

```bash
MDB-Test-Bench --discovery-smoke-test --capture-output=/absolute/path/sample-simulator.mdbcap.json
```

Retain this file only as simulator evidence; it must not be described as a Wafer capture.

## macOS signing and notarization

The generated `MDB Test Bench.app` bundles are currently unsigned and not notarized. This does not block internal v0.1 artifacts, but Gatekeeper can warn or quarantine downloads. Professional external distribution requires an Apple Developer certificate, hardened runtime signing, notarization, stapling, and verification. Credentials and signing identities must be supplied by protected CI secrets or the operator's keychain; never commit them.

## Linux runtime libraries

The .NET runtime is included, but Avalonia still uses native desktop facilities. A typical Linux desktop needs X11, X11/XCB, fontconfig, freetype, cursor, input, RandR, render, and OpenGL/mesa libraries. Exact package names depend on the distribution and its display stack. AppImage is intentionally deferred.

## GitHub Actions

`.github/workflows/ci.yml` runs restore, Release build, and tests on Ubuntu, Windows, and macOS for pull requests and pushes to `main`.

`.github/workflows/release.yml` runs only for tags matching `v*`. Its matrix packages each target on an appropriate official GitHub-hosted runner, uploads short-lived workflow artifacts, then uses the GitHub CLI and the workflow token to create the GitHub Release and attach all four assets. Job permissions default to read-only; only the final publication job receives `contents: write`. No repository secret is required for unsigned artifacts.

To publish after review:

```bash
git tag v0.1.1
git push origin v0.1.1
```

Do not run these commands until the exact commit is approved.

## Rollback

If a release is defective, do not move or overwrite an existing semantic-version tag. Mark the GitHub Release as a prerelease or delete only the GitHub Release assets through an authorized maintainer workflow, document the reason, fix the defect, and publish a new patch version such as `v0.1.1`. Removing a remote tag is exceptional and requires coordination because consumers may already have fetched it.
