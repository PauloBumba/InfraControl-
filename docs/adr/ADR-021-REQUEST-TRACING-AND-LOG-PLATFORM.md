# ADR-021 — Rastreio de requisição e plataforma de logs

- **Status**: aceito
- **Data**: 2026-09-29
- **Contexto**: F1-T015 (antiforgery) e a integração com o dashboard

## Problema

A aplicação tem Serilog com sink Seq e OpenTelemetry com OTLP já configurados
(`appsettings.json`), e `CorrelationId` já existe em `AuditEntry`. Mesmo assim
o log da aplicação é inútil para caçar um problema:

1. **O log do worker não existe.** `InfraControl.Workers` não tem Serilog —
   só `ILogger` no console do container. Nada do que roda fora da API
   (OutboxDispatcher, Cleanup, Backup, Polling, consumers de notificação)
   chega a lugar nenhum.
2. **Não há log de requisição.** Status, rota, duração e usuário não viram
   evento. Para descobrir por que uma chamada deu 500 é preciso ligar o
   debugger ou caçar no console.
3. **O rastro não atravessa processos.** O `CorrelationId` morre no outbox: a
   API grava a linha, e quem publica no RabbitMQ é outro processo, que não tem
   como saber de onde o evento veio.
4. **Não há onde consultar.** A tela `/logs` do dashboard não tem endpoint, e o
   `docker-compose` não sobe o Seq — a config aponta para `localhost:5341`, que
   não existe em lugar nenhum.

## Decisão

### 1. Seq como plataforma

Manter o Seq, que já é o sink declarado. Não adotar Elastic, Loki ou Graylog:
trocar de plataforma agora seria jogar fora configuração e decisão anterior sem
ganho proporcional. O OTel (ADR-009) continua sendo o caminho de traces e
métricas; o Seq é o caminho de logs, e a busca por texto do dashboard aponta
para ele.

### 2. CorrelationId em toda requisição

`RequestLoggingMiddleware`, na primeira posição do pipeline:

- Se o cliente mandar `X-Correlation-Id`, ele é respeitado — é o que permite
  o suporte seguir um rastro que o usuário colou da tela.
- Senão, usa-se `HttpContext.TraceIdentifier`.
- O valor entra no `LogContext` do Serilog, então **toda** linha de log carrega
  o `CorrelationId` sem ninguém pedir no código.
- Sai no header de resposta `X-Correlation-Id`, para o frontend poder exibir e
  copiar.

### 3. Um evento de requisição, agrupado por rota

Ao terminar a requisição, um evento com método, **template da rota**, status,
duração em ms, id do usuário e tamanho da resposta. Nível `Information` para
2xx/3xx, `Warning` para 4xx, `Error` para 5xx.

Agrupar por **template** (`/api/v1/devices/{id}`) e não pelo path literal é
obrigatório: com path literal cada id vira uma linha nova e a plataforma fica
incapaz de agregar — que é a função dela.

**Nunca** entram corpo de requisição nem query string: é onde vivem refresh
token, senha, código de MFA e base URL de convite.

### 4. O rastro atravessa o outbox

`OutboxMessage` ganha `CorrelationId`, preenchido no momento em que a linha é
criada (dentro do `LogContext` da requisição). O `OutboxDispatcherWorker`
publica esse valor como header da mensagem, e o worker que consome o levanta
para o próprio `LogContext`.

Sem isso o rastro morre na fronteira assíncrona, que é exatamente onde os
problemas difíceis de reproduzir: o POST responde 200 e a entrega da notificação falha
minutos depois, em outro processo.

### 5. Workers entram no Serilog

Mesmo `LoggerConfiguration` da API, com `Application` distinto
(`InfraControl.Workers`) para que o admin filtre por processo. Sem isso o
`/logs` mostra só metade da aplicação.

## Consequências

**Bom**

- Um `X-Correlation-Id` dado pelo suporte encontra a requisição, o log da API e
  o log do worker que processou o evento.
- O admin tem onde pesquisar, com filtro por processo, nível, correlation id e
  texto livre.
- Falha de entrega de notificação deixa de ser invisível.

**Ruim**

- Uma migration (`CorrelationId` em `UserSessions`... em `outbox_messages`).
- Mais um container no compose e mais um salto na cadeia de logs.
- Log de requisição é o maior volume da aplicação; a retenção do Seq precisa
  ser curta (7 dias, padrão) e o `MinimumLevel` de `Microsoft` continua em
  `Warning` para não inundar.

## Alternativas rejeitadas

- **Elasticsearch/OpenSearch**: mais pesado, um operador a mais, sem ganho para
  o volume deste sistema. Revisitar se a busca deixar de ser suficiente.
- **Loki + Grafana**: ótimo para métrica, ruim para busca por texto livre, que
  é o caso de uso do suporte.
- **Tabela de logs no PostgreSQL**: escreveria no mesmo banco que a aplicação
  tenta diagnosticar, e o crescimento da tabela compete por disco e I/O com o
  dado do produto.
- **Só OTel**: trace e métrica não substituem log de aplicação; e o consumidor
  de trace (nenhum hoje) não foi decidido.
