# MDB Test Bench v0.1.1 User Guide

## Getting started and installation

Download the package for Windows x64, macOS Apple Silicon, macOS Intel, or Linux x64 from [GitHub Releases](https://github.com/iYoNuttxD/MDB-Test-Bench/releases/latest). Verify it with `SHA256SUMS.txt`, extract it, and start `MDB-Test-Bench.exe`, `MDB Test Bench.app`, or `MDB-Test-Bench`. The packages include .NET. macOS bundles are unsigned and can trigger Gatekeeper; Linux still needs the desktop libraries described in `docs/RELEASING.md`.

The application starts disconnected and never opens a serial port or transmits bytes automatically. Settings → Language switches between English and Portuguese and persists the preference.

## Using Simulator

1. Open Settings and select Simulator.
2. Choose Normal, Always approve, Always deny, Timeout, Malformed response, or Unexpected response.
3. Press Connect and confirm the visible SIMULATION banner.
4. Use Manual or Automatic. Simulator results are development evidence, not physical Wafer validation.

## Manual mode

Structured builds a semantic command and shows the exact MDB bytes, including the MDB checksum. Supply price/product/value only when the selected command needs them. The state machine blocks incompatible commands. Structured sending works with Simulator only.

Advanced / Raw Adapter on this page is a simulator diagnostic. Physical Raw Adapter transmission is available only in Wafer Discovery so every TX is confirmed and captured.

## Automatic mode

Select a built-in scenario and press Run scenario. The page reports each step, expected/received response, total, passes, failures and duration. Cancel stops a running scenario without blocking the UI. Automatic scenarios use Simulator until a validated Wafer codec exists.

## Profiles

Level 1, Level 2 and Level 3 profiles are read-only. Duplicate one to create a custom profile. Capability status describes a device profile and does not claim implementation. Custom profiles can be created, edited, duplicated, deleted, imported and exported as validated JSON.

## Logs

Application/MDB logs are separate from raw adapter capture. Filter TX/RX/errors, pause only the view, search, copy a line or raw HEX, clear, and export TXT/JSON. Clearing these logs never deletes a Discovery capture.

## Wafer Discovery and capture export

Discovery preserves raw serial read chunks before interpretation. To use the simulator: Start Capture, Add Marker, enter valid HEX, review and confirm it, Send Raw Adapter, Stop, then Export for Analysis. Open Capture reloads a `.mdbcap.json` offline without retransmitting bytes. The JSON is the source of truth; TXT is a human summary.

For hardware, disconnect the normal workbench session, select Serial / Wafer and the port in Settings, keep the initial 9600/8/N/1 values unless the bench plan says otherwise, then start capture. Observe passively before any reviewed manual probe. The capture limit defaults to 100 MB.

## Hardware test

Follow `docs/TESTING_WITH_HARDWARE.md`. Structured control of Wafer revision `2022061K5` is disabled because host framing, checksum ownership, message boundaries and polling ownership remain unconfirmed. Periodic traffic is an observation, not proof that the adapter performs MDB POLL.

## Releases and support evidence

When reporting a problem, include version, OS, architecture, steps and the privacy-safe capture only after review. Do not attach personal paths or unrelated logs. A simulator capture must always be labelled simulated evidence.
