# Guia do Usuário — MDB Test Bench v0.1.1

## Primeiros passos e instalação

Baixe o pacote para Windows x64, macOS Apple Silicon, macOS Intel ou Linux x64 em [GitHub Releases](https://github.com/iYoNuttxD/MDB-Test-Bench/releases/latest). Valide-o com `SHA256SUMS.txt`, extraia e inicie `MDB-Test-Bench.exe`, `MDB Test Bench.app` ou `MDB-Test-Bench`. Os pacotes já incluem .NET. O bundle macOS é unsigned e pode gerar alerta do Gatekeeper; o Linux ainda precisa das bibliotecas gráficas descritas em `docs/RELEASING.md`.

O aplicativo inicia desconectado e nunca abre uma porta serial nem transmite bytes automaticamente. Configurações → Idioma alterna entre Português e Inglês e salva a preferência.

## Usando o Simulador

1. Abra Configurações e selecione Simulador.
2. Escolha Normal, Sempre aprovar, Sempre negar, Timeout, Resposta malformada ou Resposta inesperada.
3. Pressione Conectar e confirme o banner visível SIMULAÇÃO.
4. Use Manual ou Automático. Resultados do Simulador são evidência de desenvolvimento, não validação física da Wafer.

## Modo Manual

Estruturado cria o comando semântico e mostra os bytes MDB exatos, incluindo o checksum MDB. Informe preço/produto/valor somente quando o comando exigir. A máquina de estados bloqueia comandos incompatíveis. O envio estruturado funciona somente no Simulador.

Avançado / Raw Adapter nesta tela é um diagnóstico do simulador. A transmissão Raw Adapter física existe somente em Descoberta Wafer para que todo TX seja confirmado e capturado.

## Modo Automático

Selecione um cenário integrado e pressione Executar cenário. A tela informa cada etapa, resposta esperada/recebida, total, aprovados, falhas e duração. Cancelar encerra um cenário sem bloquear a UI. Os cenários usam o Simulador até existir um codec Wafer validado.

## Perfis

Os perfis Level 1, Level 2 e Level 3 são somente leitura. Duplique um deles para criar um perfil custom. O status da capability descreve o dispositivo e não afirma que a funcionalidade está implementada. Perfis custom podem ser criados, editados, duplicados, excluídos, importados e exportados como JSON validado.

## Logs

Logs da aplicação/MDB são separados da captura raw do adaptador. Filtre TX/RX/erros, pause somente a visualização, pesquise, copie linha ou HEX raw, limpe e exporte TXT/JSON. Limpar estes logs nunca exclui uma captura Discovery.

## Descoberta Wafer e exportação da captura

Descoberta preserva cada chunk de leitura serial antes da interpretação. No simulador: Iniciar captura, Adicionar marcador, informar HEX válido, revisar e confirmar, Enviar Raw Adapter, Parar e Exportar para análise. Abrir captura recarrega o `.mdbcap.json` offline sem retransmitir bytes. O JSON é a fonte de verdade; o TXT é apenas um resumo humano.

Para hardware, desconecte a sessão principal, selecione Serial / Wafer e a porta nas Configurações, mantenha inicialmente 9600/8/N/1 salvo instrução diferente do plano de bancada e inicie a captura. Observe passivamente antes de qualquer probe manual revisado. O limite padrão é 100 MB.

## Teste de hardware

Siga `docs/TESTING_WITH_HARDWARE.md`. O controle estruturado da Wafer revisão `2022061K5` está desabilitado porque framing host, responsabilidade do checksum, limites de mensagem e controle do polling não foram confirmados. Tráfego periódico é uma observação, não prova de que o adaptador executa MDB POLL.

## Releases e evidências para suporte

Ao relatar um problema, informe versão, sistema, arquitetura, passos e a captura privacy-safe somente após revisá-la. Não anexe caminhos pessoais ou logs alheios. Uma captura de simulador deve sempre ser identificada como evidência simulada.
