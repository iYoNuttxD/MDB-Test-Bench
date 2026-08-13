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

## Checklist and evidence

| # | Check | Configuration / action | Observed result | Evidence | Status |
|---|---|---|---|---|---|
| 1 | Detect USB-RS232 | Refresh ports; record exact OS port name | ____________________ | ____________________ | ☐ |
| 2 | Open port | Select the discovered port; Connect | ____________________ | ____________________ | ☐ |
| 3 | Validate serial parameters | Test baud, data bits, parity, stop bits, timeout | ____________________ | ____________________ | ☐ |
| 4 | Observe Wafer data | Capture untouched receive bytes and timestamps | ____________________ | ____________________ | ☐ |
| 5 | Identify wire representation | Compare `BinaryBytes` versus `AsciiHex` | ____________________ | ____________________ | ☐ |
| 6 | Confirm terminator/framing | Test None, CR, LF, CRLF; identify boundaries; determine whether Wafer expects the MDB checksum emitted by Core | ____________________ | ____________________ | ☐ |
| 7 | Confirm POLL ownership | Compare AdapterManaged and HostManaged without duplicate polling | ____________________ | ____________________ | ☐ |
| 8 | RESET | Record exact TX/RX, delay, and state change | ____________________ | ____________________ | ☐ |
| 9 | SETUP | Record config and max/min price exchanges | ____________________ | ____________________ | ☐ |
| 10 | ENABLE | Record exact response and idle behavior | ____________________ | ____________________ | ☐ |
| 11 | Begin session | Record who initiates and all received fields | ____________________ | ____________________ | ☐ |
| 12 | VEND REQUEST | Use controlled price/product; record approve/deny | ____________________ | ____________________ | ☐ |
| 13 | Complete session | Record vend result, session complete, and end session | ____________________ | ____________________ | ☐ |

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

1. Verify the downloaded package hash and start the native package for the test computer.
2. Confirm version `0.1.1`, Simulator selected, and no serial port opened.
3. Run L1 Initialization in Simulator as a software preflight; export the session log.
4. Power down and inspect pinout, voltage, ground, isolation, and cabling with the hardware owner.
5. Connect USB-RS232, refresh ports, and record the exact discovered name and driver.
6. Select Serial / Wafer and begin at 9600/8/N/1, AdapterManaged, BinaryBytes; do not use Structured mode.
7. Connect and capture all unsolicited Wafer traffic with timestamps before transmitting.
8. Send only reviewed Adapter Debug probes; compare BinaryBytes and AsciiHex with None, CR, LF, and CRLF.
9. Determine and record frame boundaries, terminator, checksum/error rule, maximum response, and incomplete-response behavior.
10. Compare AdapterManaged and HostManaged carefully; stop immediately if duplicate POLL traffic is observed.
11. Only after the wire format is repeatable, record RESET, SETUP, ENABLE, begin session, VEND REQUEST, vend result, SESSION COMPLETE, and end-session exchanges.
12. Reproduce each exchange, preserve raw logs/captures, and obtain review approval before implementing `IWaferProtocolCodec`.
