# Physical hardware test record

Target adapter: Wafer MDB-RS232, reported revision `2022061K5`  
Date: ____________________  
Operator: ____________________  
Machine / OS: ____________________  
USB-RS232 model and driver: ____________________  
Cashless firmware/build: ____________________
MDB Test Bench package/version (`0.1.1` expected): ____________________
Package SHA-256: ____________________

## Safety preflight

- [ ] Confirm pinout, voltage levels, ground, isolation, and power sequencing.
- [ ] Confirm the adapter and DUT are safe to connect.
- [ ] Preserve a known-good recovery/power-down procedure.
- [ ] Start with Structured hardware commands disabled; use Adapter Debug only with reviewed bytes.

## Discovery-first checklist and evidence

| # | Check | Configuration / action | Observed result | Evidence | Status |
|---|---|---|---|---|---|
| 1 | Open application | Confirm v0.1.1 and no automatic serial connection | ____________________ | ____________________ | ☐ |
| 2 | Simulator sanity test | Run Discovery simulator start/marker/raw TX/stop/export/open | ____________________ | ____________________ | ☐ |
| 3 | Connect USB-RS232 | Refresh and record exact OS port name | ____________________ | ____________________ | ☐ |
| 4 | Select port | Do not open the normal workbench connection | ____________________ | ____________________ | ☐ |
| 5 | Select initial settings | 9600 / 8 / N / 1, handshake None, AdapterManaged | ____________________ | ____________________ | ☐ |
| 6 | Start capture | Wafer Discovery → Start Capture | ____________________ | ____________________ | ☐ |
| 7 | Connect/power Wafer | Follow approved electrical/power sequence | ____________________ | ____________________ | ☐ |
| 8 | Observe without TX | Preserve unsolicited read chunks and timing | ____________________ | ____________________ | ☐ |
| 9 | Add marker | Record `Powered Wafer` and relevant observation | ____________________ | ____________________ | ☐ |
| 10 | Observe cashless LED | Add a marker for each state change | ____________________ | ____________________ | ☐ |
| 11 | Connect Zilog | Follow approved sequence; transmit nothing | ____________________ | ____________________ | ☐ |
| 12 | Add marker | Record `Connected Zilog` | ____________________ | ____________________ | ☐ |
| 13 | Observe traffic | Record chunks, gaps, lengths and errors | ____________________ | ____________________ | ☐ |
| 14 | Reviewed probes only | Confirm each Raw Adapter TX; compare formats/terminators only with approval | ____________________ | ____________________ | ☐ |
| 15 | Stop capture | Confirm automatic summary and size | ____________________ | ____________________ | ☐ |
| 16 | Export for Analysis | Save/reopen `.mdbcap.json`; attach hash and preserve original | ____________________ | ____________________ | ☐ |

## Error observations

Port removed during read: ________________________________________________  
Port removed during write: _______________________________________________  
Permission denied behavior: ______________________________________________  
Port occupied behavior: __________________________________________________  
Timeout/no response: _____________________________________________________  
Incomplete or malformed frame: ___________________________________________  
Unknown response: ________________________________________________________

## Codec decision record

Confirmed byte representation: ___________________________________________  
Confirmed framing/terminator: ____________________________________________  
Confirmed checksum/error rule: ___________________________________________  
Confirmed polling owner: _________________________________________________  
Authoritative source or capture reference: ________________________________  
Approved to implement `IWaferProtocolCodec` by: ___________________________

Do not implement a production codec until the evidence above is repeatable and reviewed.

## Exact execution order for the next lab session

1. Open the application and confirm version `0.1.1`; no port should open.
2. Select Simulator and complete the Wafer Discovery sanity flow: Start Capture, Add Marker, confirmed Raw TX, observe simulated RX chunks, Stop, Export for Analysis, Open Capture.
3. Power down and inspect pinout, voltage, ground, isolation and cabling with the hardware owner.
4. Connect USB-RS232, refresh ports and record the exact discovered name and driver.
5. Select Serial / Wafer and begin at 9600/8/N/1, handshake None, AdapterManaged and BinaryBytes. Keep the normal workbench disconnected.
6. Open Wafer Discovery and start capture.
7. Connect/power the Wafer using the approved sequence.
8. Observe traffic without transmitting.
9. Add marker `Powered Wafer` and record visible state.
10. Observe the cashless LED and add a marker for every relevant change.
11. Connect the Zilog/cashless device using the approved sequence.
12. Add marker `Connected Zilog`.
13. Continue passive observation; do not interpret periodicity as proof of polling.
14. Execute probes only after individual byte review and confirmation. Any BinaryBytes/AsciiHex or terminator comparison is a controlled experiment.
15. Stop capture and review summary, errors and size.
16. Export for Analysis, reopen the same `.mdbcap.json`, verify event/chunk counts, calculate a file hash, preserve the original and share it with the notes. Only then plan repeatable RESET/SETUP/ENABLE/vend experiments.

Before leaving the simulator preflight, the packaged CLI may generate and reopen a portable sample without a GUI:

```bash
MDB-Test-Bench --discovery-smoke-test --capture-output=/absolute/path/sample-simulator.mdbcap.json
```

A zero exit code proves that simulated RX, confirmed TX and a marker survived export/import. It does not validate serial hardware or the Wafer protocol.
