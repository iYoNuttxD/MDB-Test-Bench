# MDB Test Bench

[English](README.md) | [Português (Brasil)](README.pt-BR.md)

Aplicação desktop multiplataforma que atua conceitualmente como VMC/master para exercitar dispositivos cashless MDB. A versão `0.1.1` inclui encoder/decoder MDB Cashless, Simulador, Test Engine e Descoberta Wafer com captura raw e análise offline.

O Simulador não comprova conformidade MDB. O protocolo host ↔ Wafer MDB-RS232 da revisão informada `2022061K5` ainda depende de captura física. O envio estruturado ao hardware permanece desabilitado; nenhuma regra de framing, checksum ou polling da Wafer foi inventada.

## Download

Baixe o pacote self-contained do seu sistema em [GitHub Releases](https://github.com/iYoNuttxD/MDB-Test-Bench/releases/latest). Não é necessário instalar .NET, C# ou Avalonia. Confira o arquivo baixado usando o `SHA256SUMS.txt` publicado na mesma release.

Targets: Windows x64, macOS Apple Silicon, macOS Intel e Linux x64. Os bundles macOS são unsigned/not notarized e podem gerar alerta do Gatekeeper. No Linux, bibliotecas gráficas nativas do desktop ainda são necessárias.

## Desenvolvimento

```bash
dotnet restore MDBTestBench.sln
dotnet build MDBTestBench.sln --configuration Release --no-restore
dotnet test MDBTestBench.sln --configuration Release --no-build
dotnet run --project src/MdbTestBench.App/MdbTestBench.App.csproj
```

O aplicativo nunca abre uma porta serial nem envia bytes automaticamente. Comece pelo Simulador. Para a bancada, siga o fluxo de Descoberta e preserve o `.mdbcap.json` original.

Consulte o [Guia do Usuário](docs/pt-BR/GUIA_DO_USUARIO.md), o [checklist de hardware](docs/TESTING_WITH_HARDWARE.md), o [status da v0.1.1](docs/V0.1.1_STATUS.md) e a [documentação de localização](docs/LOCALIZATION.md).
