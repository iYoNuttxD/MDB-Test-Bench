# MDB/ICP reference used by v0.1.1

## Authoritative source

Implementation reference: [NAMA Multi-Drop Bus / Internal Communication Protocol, Version 4.3](https://www.namanow.org/wp-content/uploads/Multi-Drop-Bus-and-Internal-Communication-Protocol.pdf), Cashless Payment Devices, Section 7, plus common physical/data-link rules in Section 2.

The official NAMA-hosted PDF has an edition-label inconsistency worth preserving in review records: the front matter identifies Version 4.3 as July 2019, while the Section 7 page footers identify Version 4.3 as September 2020. This project refers to the currently hosted PDF as “MDB/ICP 4.3” and records section/page evidence rather than inventing a different revision label.

## Sections applied

| Topic | MDB/ICP 4.3 section | Project implementation |
|---|---:|---|
| Bus blocks, mode bit, ACK/NAK/RET, checksum, max block | 2.1–2.3 | `MdbChecksum`, block validation; mode bit documented as adapter responsibility |
| Cashless addressing and command/response table | 7.2–7.3 | `MdbCashlessAddressing`, command/response codes |
| RESET | 7.4.1 | `MdbResetCommand`; ACK followed by POLL/JUST RESET flow in simulator |
| SETUP Config and Max/Min | 7.4.2–7.4.3 | 16-bit L1/L2 and confirmed expanded L3 Max/Min format |
| POLL and reader responses | 7.4.4 | typed decoder for the v0.1.1 response set |
| VEND Request/Cancel/Success/Failure/Session Complete/Cash Sale | 7.4.5–7.4.10 | standard 16-bit L1/L2 command formats |
| READER Disable/Enable/Cancel | 7.4.14–7.4.16 | standard subcommands |
| REVALUE Request/Limit | 7.4.18–7.4.19 | 16-bit L2 and confirmed 32-bit expanded request/response shapes |
| EXPANSION Request ID | 7.4.20 | fixed ASCII/packed-BCD VMC identity and typed Peripheral ID response |
| EXPANSION Enable Options | 7.4.24 | Level 3 32-bit option mask, bits 0–10 only |

## Confirmed encoding rules

- Cashless #1 command bytes are `10`, `11`, `12`, `13`, `14`, `15`, and `17`; Cashless #2 uses `60`, `61`, `62`, `63`, `64`, `65`, and `67`.
- A VMC command block is the command/address byte, zero or more data bytes, and an 8-bit checksum equal to the byte sum with carry discarded. ACK (`00`), NAK (`FF`), and RET (`AA`, VMC only) do not receive an additional checksum.
- Reader informational responses contain data plus checksum. Therefore `00 00` is JUST RESET, while the single byte `00` is ACK.
- Multi-byte numeric fields implemented here are most-significant byte first.
- Monetary fields are scaled: actual value = scaled value × scale factor × 10^-decimal-places.
- Country/currency fields use packed BCD. A leading `1` selects the ISO 4217 numeric currency form (`18 40` for USD and `19 78` for EUR in the specification); `FF FF` means unknown where allowed.
- The physical mode/9th bit is not a ninth byte and is not produced by Core. A physical MDB adapter owns it.

## Test-vector provenance

The byte vectors in `MdbCashlessEncoderTests`, `MdbCashlessDecoderTests`, and `MdbCashlessRoundTripTests` are calculated directly from the field tables above and the Section 2 checksum rule. They are not Wafer captures and do not include proprietary adapter framing. Currency and scale examples reproduce the normative numeric examples in Section 7.4.2.

## Intentionally incomplete

- Level 2 Time/Date and obsolete Read/Write User File commands.
- Level 3 Negative Vend, Data Entry, Remote Vend, basket/partial-refund transaction layouts, coupon exchanges, enhanced item information, and FTL command payloads.
- Manufacturer-specific diagnostics (`FF`) beyond preserving the raw response.
- Scheduling of all delayed/multiple reader responses and the VMC ACK/RET follow-up exchange.
- MDB electrical timing and physical mode-bit generation.
- Every Wafer host-side byte, envelope, terminator, checksum decision, and polling behavior.

Unknown response codes and unknown Expansion/FTL/diagnostic response data are preserved, not discarded. Unsupported commands are rejected or reported as not implemented rather than guessed.
