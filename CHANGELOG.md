# Changelog

All notable changes to MDB Test Bench are documented here.

## 0.1.1

### English

- Added the MDB Cashless encoder/decoder path, significant Level 1 coverage, partial Level 2/Level 3 structures, simulator integration and typed automatic scenarios.
- Added Wafer Discovery, bounded raw capture, monotonic timing, markers, conservative analysis, privacy-safe `.mdbcap.json` export/import and offline reanalysis.
- Fixed Windows capture spool finalization and file-sharing lifecycle. Stop now blocks new events, waits for in-flight writes, flushes/disposes the writer and analyzes only the immutable spool.
- Added complete `en-US` / `pt-BR` presentation resources, OS-culture fallback, immediate language selection, persisted preference, bilingual user guides and About diagnostics.
- Added self-contained Windows, macOS ARM64/x64 and Linux packages, native distribution smokes, controlled tag/manual release automation and `SHA256SUMS.txt`.

### Português (Brasil)

- Adicionado o fluxo de encoder/decoder MDB Cashless, cobertura significativa de Level 1, estruturas parciais de Level 2/Level 3, integração com o Simulador e cenários automáticos tipados.
- Adicionada a Descoberta Wafer, captura raw limitada, timing monotônico, marcadores, análise conservadora, exportação/importação privacy-safe `.mdbcap.json` e reanálise offline.
- Corrigida a finalização do arquivo de captura no Windows e o ciclo de vida do spool. O Stop bloqueia novos eventos, espera gravações em andamento, executa flush/dispose do writer e analisa somente o spool imutável.
- Adicionados recursos completos `en-US` / `pt-BR`, fallback pela cultura do sistema, seleção imediata de idioma, preferência persistida, guias bilíngues e diagnóstico Sobre.
- Adicionados pacotes self-contained para Windows, macOS ARM64/x64 e Linux, smokes nativos, automação controlada por tag/manual e `SHA256SUMS.txt`.

### Known limitation / Limitação conhecida

Structured control of Wafer MDB-RS232 `2022061K5` is not enabled. The adapter protocol still requires physical capture validation.

O controle estruturado do Wafer MDB-RS232 `2022061K5` não está habilitado. O protocolo do adaptador ainda depende da validação por captura física.
