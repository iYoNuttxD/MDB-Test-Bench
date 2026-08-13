# Localization

## Supported cultures

MDB Test Bench v0.1.1 supports `en-US` and `pt-BR`. On first launch, any `pt-*` operating-system culture maps to `pt-BR`; every other or unknown culture maps to `en-US`. A saved valid preference takes precedence. Settings → Language changes the UI immediately and saves the selected culture in the per-user `settings.json`.

## Resource structure

Presentation strings are embedded JSON dictionaries:

```text
src/MdbTestBench.App/Localization/en-US.json
src/MdbTestBench.App/Localization/pt-BR.json
```

`ILocalizationService` resolves and formats strings. Avalonia Views use `{DynamicResource Key}` so a culture change refreshes the visual tree. ViewModels use the same service for status, validation and error presentation. Navigation decisions use stable IDs, never translated labels.

Automated tests require both dictionaries to contain exactly the same key set, verify fallback rules, and reject new literal user-facing attributes in Views. To add a language:

1. add a complete embedded dictionary using an explicit culture name;
2. add it to `LocalizationService` and the Settings selector;
3. define its first-launch/fallback behavior;
4. keep the key set identical;
5. run all tests and manually navigate every page in that culture.

## Technical and invariant data

Translate descriptions, controls, statuses and friendly errors. Do not translate MDB identifiers such as RESET, SETUP, POLL, VEND, ACK, NAK, TX, RX, HEX, VMC, Wafer, Cashless or Feature Level when translation would obscure their protocol meaning.

Localization is presentation-only. These remain invariant:

- MDB and Wafer bytes;
- HEX rendering;
- enum/property values serialized in settings, profiles and captures;
- capture direction and operation identifiers;
- ISO timestamps and numeric JSON representation;
- `.mdbcap.json` field names, format and version.

Visible dates, times and numbers use the active culture where appropriate. Protocol calculations, checksums, persisted JSON and filenames use invariant formats. A capture exported in Portuguese must be byte-for-byte equivalent in raw evidence to one exported in English.
