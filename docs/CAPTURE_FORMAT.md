# MDB Test Bench Capture Format

## Identity and versioning

The portable format is UTF-8 JSON with filename suffix `.mdbcap.json`.

- `format` (required): `MDBTestBenchCapture`
- `formatVersion` (required): integer `1`
- `privacySafe` (required for files created by v0.1.1): `true`

Readers reject an unknown format or version rather than guessing. Future compatible additions may introduce optional fields; an incompatible schema requires a new integer version.

## Top-level schema

| Field | Required | Meaning |
|---|---:|---|
| `captureId` | yes | Random session identifier; contains no host identity |
| `application` | yes | Application name and version |
| `adapter` | yes | Operator-supplied identity and optional USB metadata |
| `host` | yes | OS description/version, architecture and .NET version |
| `serial` | yes | Port and serial settings used for the session |
| `capture` | yes | UTC bounds, monotonic frequency and resolution note |
| `userNotes` | no | Operator notes |
| `probes` | yes | Saved manual probes; may be empty |
| `statistics` | yes | Derived summary; may be recalculated |
| `events` | yes | Strictly increasing raw/state/error/marker events |

Unknown values are JSON `null`; producers must not invent them. Privacy-safe exports omit username, home directory, hostname, network addresses and personal absolute paths. A serial port name is retained because it is operational capture context and can itself be reviewed before sharing.

## Raw events

Raw events contain both `hex` and `base64`. Import requires them to decode to identical bytes and requires `length` to match. RX `readChunkIndex` preserves the original read-call boundary.

```json
{
  "sequence": 42,
  "type": "raw",
  "timestampUtc": "2026-08-13T12:15:32.123456+00:00",
  "monotonicTimestamp": 123456789,
  "deltaMicroseconds": 4812.0,
  "direction": "rx",
  "hex": "03 FF FE",
  "base64": "A//+",
  "length": 3,
  "operation": "SerialReadChunk",
  "readChunkIndex": 7,
  "lastByteTimestampUtc": "2026-08-13T12:15:32.123456+00:00",
  "operationDurationMicroseconds": 231.0,
  "gapFromPreviousRxMicroseconds": 4812.0,
  "interByteTimingMicroseconds": null,
  "transportState": "Open"
}
```

`possibleMdbInterpretation` is derived, non-authoritative metadata. Its confidence is `likely`, `possible`, or `unknown` for automatically generated overlays. Raw evidence remains unchanged when the overlay is recalculated.

Markers use `type: "marker"` and `text`. Errors use `type: "error"`, `errorKind`, and a sanitized `errorMessage`. Transport lifecycle events use `type: "transportState"`.

## Validation limits

The importer validates format identity/version, required sections, application identity, serial ranges, maximum file size, maximum event count (1,000,000 by default), strictly increasing sequence, non-regressing monotonic timestamps, event operation/timing metadata, raw direction, capture bounds, raw length, HEX syntax, Base64 syntax, and HEX/Base64 equality. Invalid files produce a user-facing error and are never replayed.

The JSON is the source of truth. The optional `.txt` export is human-readable summary only. Import/reanalysis never retransmits events. `tests/fixtures/simulated-approved-vend.mdbcap.json` is a synthetic golden fixture used to prove event ordering and byte-exact export/import across fresh serializer instances. See [`examples/wafer-discovery-example.mdbcap.json`](examples/wafer-discovery-example.mdbcap.json) for another complete v1 example.
