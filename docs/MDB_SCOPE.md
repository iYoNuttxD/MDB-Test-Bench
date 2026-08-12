# MDB scope for v0.1

The application is modeled as the VMC/master side. This foundation represents command, subcommand, response, addressing, direction, raw payload, interpreted payload, timestamps, Feature Levels 1–3, custom profiles, and independent capabilities.

The simulator supports a development-oriented logical flow: connect, reset, setup, enable, begin session, vend request, approved or denied result, vend success, session complete, and end session. This is intended to exercise state management, UI integration, logs, test scenarios, concurrency, cancellation, and timeouts.

It is not a claim of full MDB conformance. Exact MDB field encoding, timing compliance, mode-bit handling, checksums, peripheral variants, and behaviors requiring licensed or validated specifications remain outside this initial foundation. They must be implemented from authoritative specifications and verified against hardware.

Level alone never infers every capability. Profiles explicitly declare capabilities so mixed real-world behavior can be represented without pretending it belongs to a standard level.
