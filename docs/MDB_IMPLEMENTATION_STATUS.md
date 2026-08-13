# MDB cashless implementation status

Status definitions: **Implemented** means the listed v0.1.1 shape is encoded/decoded and covered by normative vectors; **Partial** means a confirmed subset exists; **Experimental** requires negotiated capabilities or physical verification; **Not Implemented** means no frame is generated.

Layer status is deliberately separate from Feature Level:

| Feature Level | Encoder | Decoder | Simulator | Physical Wafer |
|---|---|---|---|---|
| Level 1 | Implemented for rows below | Implemented for rows below | Implemented for v0.1.1 flows | Pending codec and bench validation |
| Level 2 | Partial, including Revalue | Partial typed response set | Partial | Pending codec and bench validation |
| Level 3 | Partial/experimental, capability-gated | Partial, expanded mode explicit | Partial | Pending codec and bench validation |

| Command / response family | L1 | L2 | L3 | Encoder | Decoder | Tests | Status |
|---|---|---|---|---|---|---|---|
| Addressing Cashless #1 / #2 | Yes | Yes | Yes | Yes | Yes | Yes | Implemented |
| RESET | Yes | Yes | Yes | Yes | ACK/JUST RESET | Yes | Implemented |
| SETUP Config | Yes | Yes | Yes | Yes | Reader Config | Yes | Implemented |
| SETUP Max/Min 16-bit | Yes | Yes | Yes without expanded mode | Yes | Command round trip | Yes | Implemented |
| SETUP Max/Min 32-bit + currency | No | No | Expanded currency | Yes | Command round trip | Yes | Experimental |
| POLL | Yes | Yes | Yes | Yes | typed response catalog subset | Yes | Partial |
| READER Disable / Enable / Cancel | Yes | Yes | Yes | Yes | ACK/Cancelled | Yes | Implemented |
| VEND Request / Cancel | Yes | Yes | standard L3 | Yes | Approved/Denied | Yes | Implemented |
| VEND Success / Failure | Yes | Yes | standard L3 | Yes | ACK/NAK | Yes | Implemented |
| VEND Session Complete | Yes | Yes | Yes | Yes | End Session | Yes | Implemented |
| VEND Cash Sale | capability bit | capability bit | standard L3 | Yes | ACK/NAK | Yes | Implemented |
| REVALUE Request / Limit 16-bit | No | Yes | without expanded mode | Yes | Yes | Yes | Implemented |
| REVALUE Request / Limit 32-bit | No | No | expanded currency | Yes | Yes | Yes | Experimental |
| EXPANSION Request ID / Peripheral ID | Yes | Yes | Yes + option bits | Yes | Yes | Yes | Implemented |
| EXPANSION Enable Options | No | No | Yes | Yes | command round trip | Yes | Experimental |
| Display Request | Yes | Yes | Yes | N/A reader response | Yes | length/error tests | Implemented |
| Begin Session | Yes | Yes | standard + expanded L3 | N/A reader response | Yes | Yes | Partial |
| Session Cancel Request | Yes | Yes | Yes | N/A reader response | Yes | response-code tests | Implemented |
| Malfunction / Command Out of Sequence | Yes | Yes | Yes | N/A reader response | Yes | malformed/known tests | Implemented |
| Time/Date | No | Optional | Optional | No | raw preserved | No | Not Implemented |
| Read/Write User File (obsolete) | No | Obsolete | Obsolete | No | raw preserved | No | Not Implemented |
| Negative Vend | No | No | capability | No | raw preserved | No | Not Implemented |
| Data Entry | No | No | capability | No | raw preserved | No | Not Implemented |
| Remote Vend / Selection Request | No | No | capability | No | raw preserved | No | Not Implemented |
| Basket / Partial Refund / Options Price | No | No | capability | No | raw preserved | No | Not Implemented |
| Coupon | No | No | capability | No | raw preserved | No | Not Implemented |
| FTL | No | No | capability | No | `UnknownExpansionResponse` | preservation test | Not Implemented |
| Manufacturer diagnostics | Manufacturer-specific | Manufacturer-specific | Manufacturer-specific | No | raw preserved | preservation test | Not Implemented |

Feature level and capabilities remain independent. A Level 3 reader reports an option mask; the application maps only reported bits and does not infer unsupported options from `FeatureLevel == Level3`.
