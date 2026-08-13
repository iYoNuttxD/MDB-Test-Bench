# MDB scope for v0.1.1

The application is modeled as the VMC/master side and represents addresses, direction, commands, subcommands, responses, raw payloads, interpreted payloads, high-resolution timestamps, Feature Levels 1–3, custom profiles, and independent capability status.

Implemented standard MDB/ICP 4.3 actions cover:

- Reset and Poll, including the ACK versus JUST RESET distinction;
- Setup Config and 16-bit Max/Min Prices;
- Reader Disable, Enable, and Cancel;
- Wait Session/Poll as a logical event whose physical ownership depends on transport settings;
- Vend Request, Cancel, Success, Failure, Session Complete, and Cash Sale;
- Level 2 Revalue Request and Revalue Limit Request;
- Expansion Request ID and Level 3 Enable Options.

The semantic command builder now produces both a typed command and an MDB block. Monetary values are scaled explicitly (the simulator/manual default is factor 1 with two decimal places until Reader Config negotiation provides other values). The bytes contain the standard MDB checksum and remain distinct from unknown Wafer host framing.

Typed response decoding covers ACK, NAK, JUST RESET, Reader Config Data, Display Request, Begin Session, Session Cancel Request, Vend Approved/Denied, End Session, Cancelled, Peripheral ID, Malfunction, Command Out of Sequence, Revalue Approved/Denied/Limit, and unknown data preservation. Level 3 32-bit response shapes are decoded only when expanded currency mode is explicitly selected.

The simulator supports initialization, approved and denied vending, cancellation, session completion, timeouts, malformed responses, unexpected responses, raw diagnostics, concurrency, and cancellation. Its output is always labelled SIMULATION.

Release integration tests connect an injected Core `VmcSimulator` through `SimulatedCashlessTransport` to `ScenarioRunner`. The scenario path invokes an injectable `IMdbCashlessEncoder`; the simulator decodes each command, encodes/decodes its response, advances the state machine, and emits structured MDB traffic logs. These tests establish application behavior without hardware; they do not validate MDB electrical, timing, or adapter-wire conformance.

Physical Structured sending remains disabled until `IWaferProtocolCodec` is validated. Physical raw transmission is available only in Wafer Discovery, where confirmation and byte-exact capture are mandatory. Application/status logs, structured MDB logs and raw adapter evidence are separate records.

This is not full MDB conformance. Level 2 time/date and obsolete user-file operations, most Level 3 transaction variants, FTL, diagnostics semantics, multi-message acknowledgement scheduling, electrical timing, and the mode/9th bit remain outside this version. A capability can be Unsupported, Supported, Experimental, or NotImplemented; Feature Level 3 never enables capabilities implicitly. See [MDB_IMPLEMENTATION_STATUS.md](MDB_IMPLEMENTATION_STATUS.md).
