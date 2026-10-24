# ADR-059: Canal WhatsApp real via Evolution API — transporte de eventos do Zabbix para grupo

- **Status**: **aceito — aprovado pelo usuário em 2026-10-18 (inclui a decisão 7: texto + imagem do gráfico); libera F33-T002+** (F33-T000/T001)
- **Data**: 2026-10-18
- **Contexto**: Fase 33. O canal WhatsApp existe desde o F9-T007 como porta +
  worker + retry/DLQ, mas o dispatcher é um adaptador de logging — nenhuma
  chamada real é feita. O usuário quer usar a **Evolution API** para levar os
  eventos do Zabbix (já ingeridos via F16-T001/F19, ADR-023/ADR-027) ao grupo
  de WhatsApp da empresa ("Estudo Arquitetura") como canal de transporte.

## Contexto

O fluxo de notificação de eventos do Zabbix já está completo e validado
ponta-a-ponta (PROJECT_STATE, 2026-10-03):

```text
Zabbix (PROBLEM/OK)
  → POST /api/v1/integrations/zabbix/problems   [F16-T001, ADR-023]
  → MonitoringEventIngestor → Alert + Incident  [F19, ADR-027]
  → IncidentCreatedDomainEvent / IncidentResolvedDomainEvent / AlertResolvedDomainEvent
  → Outbox → RabbitMQ → NotificationEventConsumer [idempotente via Inbox]
  → NotificationDelivery (canal WhatsApp)        [destino global, 1 delivery/usuário]
  → NotificationWhatsAppWorker (Api, retry 5× + DLQ — F9-T009)
  → IWhatsAppNotificationDispatcher              ← LoggingWhatsAppNotificationDispatcher (só loga)
```

A única peça faltante é o **dispatcher real**. O precedente exato é o
`HttpWebhookNotificationDispatcher` (F19-T007): porta em Application,
adaptador em Infrastructure com `IHttpClientFactory` + Polly, worker sobre
`NotificationChannelWorkerBase`, DI registrada em `DependencyInjection.cs`.

A Evolution API é a escolha do usuário para o transporte: API REST que
exporiza o WhatsApp Web (conexão não-oficial via QR code), roda
on-premises em Docker (compatível com a postura da plataforma — ADR-022) e
envia mensagem **para grupo** via `POST /message/sendText/{instance}` com
`number = <groupId>@g.us`.

### Decisões do usuário (2026-10-18)

1. **Infra**: Evolution API no compose deste repo (profile `evolution`).
2. **Eventos ao grupo**: Incidente aberto + Incidente resolvido + Alerta
   resolvido + Alerta criado (Critical).
3. **Duplicidade aceita**: "Alerta criado (Critical)" **e** "Incidente
   aberto" são enviados como 2 mensagens distintas para o mesmo problema —
   decisão explícita do usuário (mais contexto no grupo, aceitando o
   ruído). Alternativa (só alerta sem incidente) foi proposta e recusada.
4. **Remetente**: número **pessoal** do usuário (não há chip dedicado
   disponível). Risco de banimento registrado e mitigado no §6.
5. **Caminho do envio — lembrete do usuário (2026-10-18)**: toda mensagem
   de evento sai **exclusivamente pelo pipeline existente, começando no
   Outbox**: `Outbox → RabbitMQ → NotificationEventConsumer (fanout/dedup)
   → NotificationWhatsAppWorker → IWhatsAppNotificationDispatcher
   (EvolutionApiWhatsAppNotificationDispatcher)`. **Proibido** qualquer
   atalho que chame a Evolution fora desse fluxo (o curl/manager do spike
   foi validação manual, não caminho de produto).
6. **Documentação integrada**: código e configuração do canal são
   documentados **junto com a plataforma e o Zabbix** em F33-T006 —
   docs/07 (canal + tabela de eventos), docs/27 (config do webhook do
   Zabbix que dispara o fluxo), `.env.example`/compose e runbook.
7. **Formato da mensagem — texto + imagem do gráfico** (resposta do
   usuário 2026-10-18; referência: card da plataforma em `image.png` do
   desktop): a mensagem no grupo espelha o card da notificação web —
   **caption** com status/data/hora, dispositivo, título e métricas +
   **gráfico da métrica como imagem** (PNG gerado pelo servidor) anexado
   via `POST /message/sendMedia`. Mídia **restrita** a essa imagem de
   gráfico (decisão 7 altera a proibição de mídia da §1); texto puro
   (`sendText`) permanece o fallback. Geração do PNG = trabalho novo
   (F33-T005: spike de fonte de dados + lib de renderização).

## Decisão

### 1. Escopo — transporte de saída, sem comando de volta

| Permitido neste ADR | Proibido neste ADR |
|---|---|
| Enviar texto para o grupo do WhatsApp | O WhatsApp/n8n/qualquer chat decidir estado de Incident/Alert |
| Opt-in por config (`Notifications:Channels`) | Webhook de entrada do WhatsApp (mensagens recebidas) |
| Destino global por canal (grupo) | Destino por usuário (o grupo é único destinatário) |
| Retry/DLQ no worker existente | Mídia diversa da imagem de gráfico dos eventos (fotos, vídeos, PDFs, áudios), template comercial ou sessão de marketing |
| Imagem de gráfico (PNG gerado pelo servidor) anexada à mensagem de evento — **decisão 7**, F33-T005 | Gerar/anexar mídia fora do payload do evento (ex.: print arbitrário, logo) |

A regra 7 da revisão F19/F20/F21 (ADR-027 §6) permanece: o InfraControl é
o único dono do estado do Incidente; este canal é **só saída de
notificação** — a Evolution API não recebe nenhum webhook de volta.

### 2. Infraestrutura — profile `evolution` no compose (ADR-022)

Três containers dedicados na rede `infracontrol` (padrão do `zabbix-db`,
F18-T001):

| Serviço | Imagem (pinada) | Porta host | Papel |
|---|---|---|---|
| `evolution-api` | `evoapicloud/evolution-api:v2.3.7` (validado no spike) | `EVOLUTION_API_PORT` (8081) | API REST + instância WhatsApp |
| `evolution-db` | `postgres:17-alpine` | `EVOLUTION_DB_PORT` (5435) | Estado das instâncias/sessões |
| `evolution-redis` | `redis:7-alpine` | `EVOLUTION_REDIS_PORT` (6380) | Cache/sessão da Evolution |

- Sobe **só** com `docker compose --profile evolution up -d`; a API do
  InfraControl **não depende** dele (opt-in §4 — espelho do profile `ai`).
- `AUTHENTICATION_API_KEY` (header `apikey`) e credenciais do banco
  **somente via `.env`** (AGENTS.md: nunca credencial real no repositório).
- Imagem **pinada por tag**: contrato da Evolution muda entre majors
  (v1/v2) — pin + smoke obrigatório (F33-T006). Spike confirmou: as docs
  oficiais apontam `atendai/evolution-api`, **que não existe** (404) —
  repo real `evoapicloud/evolution-api`; `latest` aponta build mais
  antigo que `v2.3.7` (por isso o pin).
- Conexão do WhatsApp: QR **1× por instância** (pairing code está quebrado
  na v2.3.7 — spike; runbook F33-T006 usa QR + manager UI
  `http://localhost:8081/manager`). O JID do grupo (`<id>@g.us`) é
  descoberto via `GET /group/fetchAllGroups/{instance}?getParticipants=false`
  (rota real validada; o `/group/find-groups` das docs NÃO existe) e
  configurado no `.env`.

### 3. Configuração — `WhatsAppChannelOptions` estendida

Seção `Notifications:Channels:WhatsApp` ganha (espelho das demais integrações
por env):

| Campo | Env | Papel |
|---|---|---|
| `Phone` (já existe) | `WHATSAPP_PHONE` | Destino individual (mantido, retrocompatível) |
| `GroupId` (novo) | `WHATSAPP_GROUP_ID` | Destino grupo (`<id>@g.us`) — **prioritário sobre Phone** |
| `BaseUrl` (novo) | `EVOLUTION_BASE_URL` | URL da Evolution (`http://evolution-api:8080` no compose) |
| `ApiKey` (novo) | `EVOLUTION_API_KEY` | Header `apikey` — fora de DTO/log/resposta |
| `InstanceId` (novo) | `WHATSAPP_INSTANCE` | Nome da instância Evolution |

- `IsSendable` (consumer) passa a aceitar o canal com **GroupId ou Phone**
  preenchido; sem nenhum dos dois continua Warning sem delivery (F9-T007).
- Kill switch: remover `WhatsApp` de `Notifications:Channels:Default`
  desliga o canal **sem deploy** (novo `NOTIFY_DEFAULT_2=WhatsApp` para
  ativar — default continua Email+SignalR, zero mudança de comportamento
  para quem não configurar).

### 4. Dispatcher real — espelho F19-T007

`EvolutionApiWhatsAppNotificationDispatcher` (Infrastructure/Services)
substitui o adaptador de log **condicionalmente**:

- `Evolution:BaseUrl` **vazio** → mantém
  `LoggingWhatsAppNotificationDispatcher` (dev/teste sem Evolution não
  quebra — linha de DI condicional, registro manual prevalece no
  RepositoryRegistrar/F0-T024).
- `Evolution:BaseUrl` preenchido → dispatcher real:
  `POST {BaseUrl}/message/sendText/{InstanceId}` com header `apikey`,
  corpo `{ number: <GroupId|Phone>, text: <mensagem> }` (**contrato real
  validado no spike — campo `text` plano; o formato `textMessage.text`
  das docs v1 não existe na v2.3.7**), texto =
  título + mensagem da notification + data (formato WhatsApp simples).
- Com o gráfico habilitado (F33-T005, decisão 7), o dispatcher envia via
  `POST /message/sendMedia/{InstanceId}` (`mediatype=image`,
  `mimetype=image/png`, `media=<base64>`, `caption=<texto>`, `fileName`)
  — contrato exato a validar no spike da T005 (rota **confirmada viva**
  na v2.3.7: probe `{}` → 400 "requires number/mediatype"). **Fallback
  automático para `sendText`** quando a geração do PNG falhar ou o
  recurso estiver desabilitado — mensagem nunca fica por enviar por
  causa do gráfico.
- Resiliência **idêntica ao n8n** (policies Polly no handler, client
  nomeado `evolution-api`): retry 3× backoff exponencial 1s/2s/4s com
  jitter + circuit breaker (5 falhas em 30s → aberto 60s); erro de rede e
  5xx fazem retry, **4xx não** (erro do chamador); resposta final não-2xx →
  exceção → delivery `Failed` com erro pt-BR (worker decide retry/DLQ).
- Nenhum SDK da Evolution — só `HttpClient` (padrão do repo: sem
  dependência alienígena; ADR-048 §4 espírito).

### 5. Fanout — dedup por notificação para destino WhatsApp (grupo)

Hoje o consumer enfileira **1 delivery por usuário final** resolvido das
roles Operator/Admin. Com destino **global em grupo**, N usuários = N
mensagens **idênticas** no grupo — bug latente do design F9-T006 para
destino não-individual.

- **Decisão**: no `QueueDeliveriesAsync`, o canal WhatsApp passa a criar
  **1 delivery por notificação** quando houver destino de grupo
  (`GroupId` preenchido), se ≥1 destinatário tiver o canal habilitado
  (preferência desligada de todos = sem delivery, comportamento F9-T003
  preservado).
- Phone individual (sem GroupId) **mantém** 1 delivery/usuário — o destino
  é o número de cada um, não faz sentido dedup.
- Telegram/Slack/Webhook têm o mesmo padrão (destino global) e **não são
  alterados** neste ADR — o n8n recebe payload por usuário (F19-T007) e
  mudar isso é decisão futura, separada.

### 6. Risco do número pessoal — registro e mitigação

A Evolution API usa conexão **não-oficial** (WhatsApp Web/Baileys). A Meta
pode banir o número conectado. O usuário decidiu usar o número pessoal
(2026-10-18) ciente do risco. Mitigações obrigatórias:

- **Volume mínimo**: só 4 tipos de evento (§decisões 2/3) — sem alerta
  Warning, sem spam de polling; o streak/anti-storm do F6-T001/F6-T002 já
  limita a abertura de alertas.
- **Sem loop de disparada**: retry/DLQ do F9-T009 (5 tentativas, depois
  para); circuit breaker derruba a chamada quando a Evolution cai.
- **Kill switch sem deploy** (§3).
- **Runbook de emergência** (F33-T006): desativar o canal = remover
  `WhatsApp` do fanout; reconexão = novo QR na instância.
- **Migração futura registrada**: chip dedicado (número novo) elimina o
  risco ao usuário — quando disponível, só muda `WHATSAPP_GROUP_ID`
  (o número conectado é da instância, não do InfraControl).

### 7. RBAC e segurança

| Controle | Decisão |
|---|---|
| Config (env/seção) | Operador via `.env` + compose (sem endpoint de settings nesta fase — espelho Zabbix env, F18-T001) |
| ApiKey | Só env; nunca em DTO, log ou resposta (REGRA 8 / ADR-048) |
| Destino | Grupo único global por canal (F9-T006) |
| Entrada do WhatsApp | **Não existe** — sem webhook, sem superfície de ataque nova na API |
| Comando via chat | Proibido por este ADR (§1) |

### 8. Consequências

- **Positivo**: eventos do Zabbix (incidentes/alertas) chegam ao grupo de
  WhatsApp da empresa sem ferramenta extra; reaproveita 100% do pipeline
  já testado (Outbox→RabbitMQ→consumer→worker→retry/DLQ); on-premises;
  kill switch instantâneo.
- **Negativo**: dependência nova (Evolution API + 3 containers + versão
  pinada); contrato WhatsApp não-oficial sujeito a mudança/banimento;
  2 mensagens por evento Critical (decisão §decisões 3); dedup de fanout
  cria divergência de comportamento entre canais (WhatsApp ×
  Telegram/Slack/Webhook §5) até decisão futura.
- **Negativo (adição da decisão 7, 2026-10-18)**: mensagem com imagem
  exige **geração de PNG no servidor** — biblioteca nova de renderização,
  fonte da série histórica a definir no spike F33-T005 (o payload do
  Zabbix, docs/27, não traz histórico) e custo extra de CPU/tamanho no
  worker; mitigação = fallback automático para texto (§4).
- **Risco aceito**: banimento do número pessoal (§6) — mitigado, com
  migração documentada para chip dedicado.

## Alternativas consideradas

1. **Meta Cloud API oficial** — contrato estável e "legal", mas exige
   número dedicado com Business API, verificação de conta e custo por
   conversa; usuário não tem chip disponível (decisão 4). Fica como
   evolução futura: só trocar o dispatcher, a porta não muda.
2. **n8n → WhatsApp** (caminho já previsto em ADR-027 §6/F19-T007) — o
   webhook n8n já entrega payload com `eventType`; o n8n poderia chamar a
   Evolution. **Rejeitado pelo usuário**: quer direto InfraControl →
   Evolution, sem dependência do n8n para este fluxo.
3. **Canal novo "WhatsAppGroup" separado do WhatsApp** — mais um enum,
  mais um worker; rejeitado: o destino é configuração do canal (F9-T006),
  não natureza diferente — `GroupId` cobre o caso sem novo enum/migração.
4. **Enviar só Incidente (sem AlertCreated)** — proposto para evitar
   duplicidade; recusado pelo usuário (decisões 3: sempre as 2 mensagens).
5. **Destino por usuário (cada Operator recebe no seu número)** — exigiria
   telefone cadastrado por usuário (não existe no Identity hoje) e seria
   1 chip por destinatário; fora do pedido (grupo é o canal).
6. **Imagem em mensagem separada** (sendText + sendMedia como 2
   mensagens) — rejeitado (decisão 7): poluiria o grupo com mais 2
   entradas por evento Critical (que já são 2 mensagens); o `caption` do
   `sendMedia` une texto + gráfico numa mensagem só, com aparência de
   card igual à referência do usuário.

## Itens a validar no spike (F33-T001 — antes do código seguir)

- [x] Tag estável da imagem `evoapicloud/evolution-api` e variáveis de
      ambiente obrigatórias (`AUTHENTICATION_API_KEY`, Postgres, Redis).
- [x] Contrato real de envio: `POST /message/sendText/{instance}` — corpo
      exato para **grupo** (`number` com `@g.us`), header `apikey`.
- [x] Descoberta do JID do grupo (`/group/fetchAllGroups` — o
      `/group/find-groups` das docs não existe na v2.3.7).
- [x] QR/pairing code e persistência da sessão após restart do container.
- [x] Resultado do spike **atualizado neste ADR** (seção abaixo).
- [x] **Aprovação humana**: **concedida em 2026-10-18** ("sim" do usuário,
      cobrindo a decisão 7 do gráfico) → status "aceito", libera F33-T002+.

## Itens a validar no spike F33-T005 (gráfico — antes do código seguir)

- [x] Contrato real de `sendMedia` na v2.3.7: campos exatos (`mediatype`,
      `mimetype`, `media` em base64, `caption`, `fileName`), limite de
      tamanho (WhatsApp ~5MB) e comportamento com destino de **grupo**
      (`number` `@g.us`).
- [x] **Fonte de dados do gráfico — DEFINIDA pelo usuário (2026-10-18)**:
      "os Zabbix têm essa informação" → fonte = **API de `history` do
      Zabbix** (o payload do webhook, docs/27, não traz histórico — será
      consultado no momento do envio). Validar no spike: endpoint e auth
      da API do Zabbix a partir do worker, janela/retenção do gráfico,
      item/trigger do device e o comportamento **sem histórico**
      (fallback = só texto — decisão 7 + §4).
- [x] Lib .NET **headless** de renderização de PNG (sem dependência
      alienígena — espírito ADR-048 §4) + tamanho e tempo de geração
      aceitáveis dentro do worker.
- [x] Resultado do spike **atualizado neste ADR** (seção abaixo).

## Resultados do spike F33-T005 (gráfico — 2026-10-19)

Executado nesta máquina (Docker 29.7.2) contra a Evolution `v2.3.7` real.

| Item | Resultado |
|---|---|
| Rota e método | `POST /message/sendMedia/{InstanceId}` com header `apikey` |
| Campos do corpo | `number` (grupo `@g.us` ou número), `mediatype=image`, `mimetype=image/png`, `media` (PNG em base64 puro, **sem** prefixo `data:`), `caption` (texto completo da mensagem), `fileName` (ex.: `infracontrol-evento.png`) |
| Destino de grupo | `number=120363431225988863@g.us` aceito (200/201 como no `sendText`) |
| Limite de tamanho | ~5MB de mídia (WhatsApp Business) — o spike original mediu PNG 800×450 em ~30–80KB; F38-T006 mudou para 1024×768 e o tamanho atual ainda não foi medido |
| Fonte da série | API `history` do Zabbix via `IZabbixClient.GetHistoryAsync(itemId, from, to, limit)` — janela 2h, até 240 pontos; comportamento original de seleção genérica foi substituído pela F38-T006 abaixo |
| Lib de renderização | **SkiaSharp 4.153.1** + `SkiaSharp.NativeAssets.Linux` (headless, managed); fonte TTF via `fonts-dejavu-core` no Dockerfile multi-stage de API/Workers |
| Comportamento sem histórico | `IWhatsAppChartFactory.TryCreateAsync` devolve `null` (Zabbix sem config, device sem host, série vazia, ou erro de rede — nunca lança); worker envia `message.Chart = null` → dispatcher `sendText` puro |
| Fallback obrigatório | Dispatcher: `try RenderPng + sendMedia` → `catch` (não-cancel) loga Warning e reenvia como `sendText`; mídia que falha **nunca** deixa o evento por enviar (decisão 7 + §4) |
| Integração | `Notification.DeviceId` (migração `AddNotificationDeviceId`, coluna `device_id` nullable) viaja do evento até o worker (`PendingDeliveryItem.DeviceId` → `WhatsAppNotificationMessage.Chart`); DI `AddSingleton<IIncidentImageRenderer, SkiaIncidentImageRenderer>` + `AddScoped<IWhatsAppChartFactory, ZabbixWhatsAppChartFactory>` |

## Ajuste F38-T006 — fidelidade dos cartões WhatsApp (2026-10-23)

As quatro imagens compartilhadas pelo usuário nesta data passam a ser a
referência visual dos cartões enviados com os alertas. O pipeline de geração e
envio de PNG da F33-T005 já existe; este ajuste refina apresentação e seleção
de dados, sem alterar Outbox/RabbitMQ/Evolution nem remover o fallback para
texto.

### Plano de implementação e critérios de aceite

1. **Cartão no padrão das referências**: fundo escuro; badge de estado colorido
   (`INCIDENTE`, `AVISO` ou `RESOLVIDO`); data/hora no topo; dispositivo/host;
   título do alerta; resumo de valor atual, mínimo e máximo; gráfico histórico
   com eixos/horários legíveis; limite tracejado e legenda quando o limite
   verdadeiro estiver disponível.
2. **Cores e estado coerentes**: incidente em vermelho, aviso em âmbar e
   resolução em verde, seguindo os exemplos. Um mesmo layout deve funcionar
   para métricas com unidade (°C, %) e métricas sem unidade (uptime/contagem).
3. **Série correta**: priorizar o item Zabbix relacionado ao alerta e ao
   título/mensagem, incluindo unidades do item; não escolher silenciosamente
   uma métrica de CPU/load/ping sem relação apenas por preferência genérica.
4. **Não inventar dados**: só exibir unidade, limite ou estatística que venha
   da série/item ou de fonte de threshold comprovada. Sem série utilizável,
   sem host/item correspondente ou sem Zabbix configurado, preservar o
   comportamento atual de envio de texto. Sem limite conhecido, não desenhar
   nem legendar um limite fictício.
5. **Compatibilidade e qualidade**: PNG continua sendo enviado por
   `sendMedia` com a mesma caption; falha de renderização/envio de imagem
   continua tendo fallback para `sendText`; cobrir os quatro estados visuais,
   unidade, limite presente/ausente, série vazia/um ponto e envio/fallback com
   testes.
6. **Configuração de servidor**: confirmar e documentar que API e Workers têm
   fontes e runtime nativo SkiaSharp necessários; documentar requisitos
   efetivamente ausentes, se encontrados, sem colocar ApiKey/segredos em
   documentação nem exigir manager UI da Evolution.

### Limites

Não introduzir editor de layout ou regras de alerta na plataforma nesta tarefa.
Não alterar a origem do evento nem os critérios do fanout. Campos calculados
devem vir do Zabbix/alerta; se a fonte não fornecer um limite verdadeiro, o
cartão o omite. O contrato atual de `ZabbixItemDto` não fornece a expressão
do trigger nem seu threshold; portanto, não há linha de limite nos cartões
até uma fonte confiável ser conectada. F38-T006 em `TASKS.md` rastreia o
trabalho e seus testes.

### Resultado da implementação (2026-10-23)

- O renderer agora produz PNG 1024×768 no layout das referências: badge
  preenchido por estado, timestamp, dispositivo, título real do alerta,
  resumo atual/mínimo/máximo e gráfico com grade, eixos e rótulos de hora.
- A factory usa o título extraído da mensagem da notificação, seleciona item
  somente quando os termos do alerta correspondem de forma única ao nome/chave
  do item e transmite `units` do Zabbix. Série sem correspondência inequívoca
  mantém o envio de texto, evitando gráfico de métrica alheia.
- Valores não finitos são descartados; unidade `%`, `°C`, outras unidades e
  `uptime` têm formatação correspondente. A linha tracejada e sua legenda só
  aparecem quando `LimitValue` existir; nenhuma expressão de trigger é
  convertida heurísticamente em threshold.
- API e Workers já incluem `SkiaSharp.NativeAssets.Linux` e as fontes
  `fonts-dejavu-core` no Dockerfile; nenhuma configuração adicional de servidor
  foi necessária. API e Workers foram reconstruídos e atualizados em
  2026-10-23; ambos ficaram healthy. RabbitMQ permanece `unhealthy` no
  healthcheck do Docker (timeouts durante o build), embora
  `rabbitmq-diagnostics ping` e `check_port_connectivity` tenham passado e o
  bus MassTransit tenha iniciado. A Evolution permaneceu sem alteração.
- Testes direcionados: 62 aprovados, incluindo seleção/ambiguidade de item,
  unidade, cores, PNG 1024×768 e série de um ponto. Envio Evolution real e
  revisão visual no grupo continuam pendentes até haver sessão WhatsApp
  conectada e destino de teste confirmado.

## Resultados do spike (F33-T001 — 2026-10-18)

Executado ponta a ponta nesta máquina (Docker 29.7.2): instância real
conectada ao número pessoal do usuário e mensagem entregue em grupo.

| Item | Resultado |
|---|---|
| Imagem | Docs oficiais apontam `atendai/evolution-api` — **repo inexistente** (404). Repo real `evoapicloud/evolution-api`; tag `latest` é mais antiga que `v2.3.7` (2025-12) e `2.4.0` é RC → **pin `evoapicloud/evolution-api:v2.3.7`** |
| Env obrigatória | `AUTHENTICATION_API_KEY`, `DATABASE_PROVIDER=postgresql` + `DATABASE_CONNECTION_URI`, `CACHE_REDIS_ENABLED` + `CACHE_REDIS_URI`, `SERVER_URL`, `SERVER_PORT=8080` (interno, `http://evolution-api:8080`) |
| Healthcheck | `/ping` documentado **não existe** em v2.3.7 (404) → usado `/` (200 sem auth) |
| Sessão | Persiste restart do container **sem novo QR** (creds em Postgres; `CACHE_REDIS_SAVE_INSTANCES=false`) — validado com `--force-recreate` → `state=open` em ~44s |
| QR | `POST /instance/create` com `qrcode:true` devolve `qrcode.base64` síncrono (~6s); `GET /instance/connect/{instance}` devolve `{pairingCode, code, base64, count}` **flat**. QR expira ~20-45s; limite `QRCODE_LIMIT=30` renovações; cada chamada de `connect` reinicia o socket (QR anterior morre) |
| Pairing code | **Quebrado na v2.3.7** (retorna vazio — issue #1408) → só QR |
| Bug #1408 | Sem workaround, o socket entra em loop de reconexão (~3m31s) e o QR nunca conecta. **Workaround validado: `create → DELETE /instance/logout/{instance} → connect`** |
| Grupos | `GET /group/find-groups` → 404 (não existe). Rota real: **`GET /group/fetchAllGroups/{instance}?getParticipants=false`** (200 em ~58s) |
| Envio | **`POST /message/sendText/{instance}` com `{ number: "<id>@g.us", text }` + header `apikey` → 201** (`key.remoteJid` do grupo) — contrato do §4 corrigido |
| Mídia | `POST /message/sendMedia/{instance}` **existe na v2.3.7** (probe `{}` → 400 "instance requires property number/mediatype" — rota viva e validando, diferente das rotas de grupo que dão 404). Contrato completo (base64, caption, limites de tamanho, destino `@g.us`) a validar no spike **F33-T005** (decisão 7) |
| Manager UI | `http://localhost:8081/manager` — interface web da Evolution (observar/reconectar instância; entra no runbook F33-T006). Login = API key global |
| Instância operacional | **`Admin`** — criada e conectada pelo usuário via manager UI (2026-10-18). `infracontrol` (criada no spike) e `bumba` (validação de envio, 201 no grupo) foram **apagadas pelo usuário na interface** no mesmo dia (1 número = 1 conexão ativa — conflito `device_removed`). **`WHATSAPP_INSTANCE=Admin`** |
| Log | `LOG_LEVEL=DEBUG` usado só na diagnóstico e revertido para `INFO` (instância conectada a N grupos infla o log do Docker) |
| Grupo destino | "Estudo Arquitetura" **não consta** nos 117 grupos do número (2 buscas). Usuário definiu grupo de teste **`Bnnnn` → `120363431225988863@g.us`** (mensagem entregue e confirmada). Grupo de produção: definir nome/existência → trocar `WHATSAPP_GROUP_ID` no `.env` |

## Referências

- docs/07 (notificações, canais, tabela de eventos) · docs/27 (runbook Zabbix)
- ADR-012 (notificações) · ADR-022 (compose/profiles) · ADR-023 (webhook Zabbix)
  · ADR-027 (ingestão de eventos) · ADR-028 (camada de integração/n8n)
  · ADR-048 (arquitetura de integrações) · ADR-016 (RBAC) · ADR-005 (Outbox)
- F9-T004/T006/T007/T009 (canal WhatsApp, fanout, retry/DLQ) ·
  F16-T001/F19-T007 (webhook Zabbix, dispatcher n8n — precedente do código)
- Evolution API: `POST /message/sendText/{instance}` (corpo `{number, text}`
  validado no spike), `GET /group/fetchAllGroups/{instance}?getParticipants=false`,
  header `apikey`, workaround issue #1408 (create → logout → connect)
