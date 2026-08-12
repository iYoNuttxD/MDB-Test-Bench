# MDB scope for v0.1

The application is modeled as the VMC/master side and represents addresses, direction, commands, subcommands, responses, raw payloads, interpreted payloads, high-resolution timestamps, Feature Levels 1–3, custom profiles, and independent capability status.

Structured v0.1 actions cover:

- Reset;
- Setup Config and Max/Min Prices as semantic operations;
- Reader Disable, Enable, and Cancel;
- Wait Session/Poll as a logical event whose physical ownership depends on transport settings;
- Vend Request, Cancel, Success, Failure, and Session Complete;
- an Expansion extension point without unconfirmed command details.

The semantic command builder deliberately produces no Wafer wire bytes. Price/product/value fields appear as interpreted logical payload until an authoritative MDB encoder and validated Wafer codec are available.

The simulator supports initialization, approved and denied vending, cancellation, session completion, timeouts, malformed responses, unexpected responses, raw diagnostics, concurrency, and cancellation. Its output is always labelled SIMULATION.

This is not full MDB conformance. Exact field encoding, timing compliance, mode-bit handling, checksums, peripheral variants, and behaviors requiring authoritative specifications remain outside this version. A capability can be Unsupported, Supported, Experimental, or NotImplemented; listing it never asserts implementation.
