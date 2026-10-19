# Plano — Canal WhatsApp com imagens de incidente (Zabbix → SkiaSharp → Evolution API)

> **Status**: plano aprovado em conversa (2026-10-18). Lote 1 = backend ponta a
> ponta. Implementação em fatias da **Fase 36** do TASKS.md.
> **Pedido original**: quando um incidente abrir no Zabbix, a plataforma monta
> um cartão profissional em PNG (SkiaSharp, sem IA, sem headless, sem serviço
> pago) com o gráfico histórico da métrica e envia imagem + legenda para o
> grupo de WhatsApp configurado — via **Evolution API**.

## 1. Realidade do repositório (Fase 1 — análise concluída 2026-10-18)

O pipeline genérico do plano **já existe** e é reutilizado sem duplicar:

| Componente do plano | Estado no repo | Referência |
| --- | --- | --- |
| Webhook Zabbix (validação, dedupe, recuperação) | **Pronto** — `IngestZabbixProblemCommand`, índice único `(source, externalEventId)`, `MarkRecovered` | ADR-023, ADR-027, docs/27 |
| Incidentes/Alertas + histórico + Outbox | **Pronto** — `Alert`/`MonitoringEvent`/`Incident`, `OutboxMessage`, `NotificationDelivery` | ADR-027, docs/03, docs/06 |
| Outbox transacional + dispatcher | **Pronto** — `OutboxDispatcherWorker` com distributed lock e batch | docs/06 |
| Fila/retentativas | **Parcial** — MassTransit/RabbitMQ + idempotência por `InboxStore`/`ProcessedMessage`; **retry progressivo e DLQ não estão configurados explicitamente** (gap F36-T006) | docs/06 |
| Fanout de notificação | **Pronto** — `NotificationEventConsumer` consome `IncidentCreatedDomainEvent`/`IncidentResolvedDomainEvent` e cria deliveries por canal (Operator+Admin) | ADR-012, docs/07 |
| Canal WhatsApp | **Pronto (texto)** — porta `IWhatsAppNotificationDispatcher` + worker; adaptador real = `LoggingWhatsAppNotificationDispatcher` (nenhuma chamada de API) | docs/07 §Canal WhatsApp (F9-T007) |
| Zabbix API — histórico p/ gráfico | **Pronto** — `IZabbixClient.GetHistoryAsync(itemId, from, to, limit)` | F27, docs/37 |
| **Evolution API (adaptador real)** | **Não existe** no repo — só a porta e o logging adapter | docs/07 §Canal WhatsApp |
| **SkiaSharp / PNG** | **Não existe** — nenhum pacote de imagem na solution | — |
| **Múltiplos destinos (grupo/número)** | **Não existe** — destino é `Notifications:Channels:WhatsApp:Phone` (número único global) | docs/07 §config |
| **Dados do gráfico ligados ao incidente** | **Gap** — o webhook não persiste trigger→item; o enriquecimento lookup na API Zabbix é trabalho novo | F36-T004 |

> **Nota sobre Evolution API**: não há MD dedicada no repo e o
> `docker-compose.yml` do projeto **não sobe o serviço** (só expõe
> `WHATSAPP_PHONE`). A instância da Evolution é **externa**, criada pelo
> operador na interface da própria Evolution API; o backend conversa com ela
> por HTTP (`Evolution:BaseUrl`/`ApiKey`/`InstanceName`). Uma doc canônica do
> canal evolui no `docs/07-NOTIFICACOES.md` à medida que o adaptador real
> entrar.

## 2. Decisões registradas (2026-10-18)

- **D1 — Evolution externa**: instância criada na interface da Evolution API;
  o compose do repo ganha apenas as envs do backend
  (`Evolution__BaseUrl`, `Evolution__ApiKey`, `Evolution__InstanceName`).
  Nenhuma instância nova é criada pelo backend e nenhum QR Code é exigido
  dela.
- **D2 — Vários destinos**: o canal WhatsApp passa a aceitar uma **lista de
  destinos** (`kind: phone | group`, valor = número ou `groupId` do WhatsApp).
  Lote 1: lista vem da config (`Notifications:Channels:WhatsApp:Destinations`);
  lote 2: UI de sincronização de grupos da instância Evolution + seleção
  persistida. `Phone` legado continua aceito (entry de config vira destino
  phone automático para não quebrar o que já existe).
- **D3 — Lote 1 = backend ponta a ponta** (F36-T001..T005): ADR-061 + renderer
  + adaptador Evolution + enriquecimento do gráfico + ligação
  incidente→imagem, com destinos vindos da config. UI fica para o lote 2.
- **D4 — ADR-061 obrigatória** (rascunho na implementação; aceite formal do
  usuário precedente ADR-060): o canal WhatsApp ganha mídia e provedor real —
  mudança de contrato de canal, não feature cosmética.

## 3. Arquitetura alvo (lote 1)

```text
Zabbix (webhook)
  → IngestZabbixProblemCommand (já existe — dedupe/recovery ADR-027)
  → Incident/Alert + OutboxMessage (mesma transação — já existe)
  → OutboxDispatcherWorker → RabbitMQ (MassTransit — já existe)
  → NotificationEventConsumer (idempotente — já existe)
      cria Notification + NotificationDelivery(WhatsApp) por destino
  → NotificationWhatsAppWorker (estendido)
      tipo = incidente/resolução?
        → IIncidentImageRenderer (NOVO — SkiaSharp, Infrastructure)
             dados: cartão + série histórica (F36-T004 enriquece via IZabbixClient)
        → IWhatsAppNotificationDispatcher (adaptador Evolution NOVO)
             envia imagem base64 + legenda para cada destino (phone/group)
      outro tipo de notificação
        → texto como hoje (compatível)
  → registro do resultado na NotificationDelivery (Sent/Failed + erro pt-BR)
```

Regras de reaproveitamento: **não** criar consumer novo, **não** criar Outbox
nova, **não** duplicar pipeline de notificação — o fanout e a idempotência já
existem (docs/06/07). O WhatsApp continua um canal entre os outros; falha de
WhatsApp não afeta Email/SignalR/Telegram/Webhook.

## 4. Fases de implementação (Fase 36 — TASKS.md)

### F36-T001 — ADR-061 (rascunho → aceite)
Contrato do canal WhatsApp v2: mídia opcional, provedor Evolution (HTTP,
sem SDK), destinos múltiplos (phone|group), idempotência de envio, cooldown.
Critério: ADR escrita e aceita pelo usuário antes do código de produção.

### F36-T002 — Renderer SkiaSharp (isolado, dados fictícios)
- Porta `IIncidentImageRenderer` em Application (entrada `IncidentImageData`,
  saída `byte[]` PNG); implementação `SkiaIncidentImageRenderer` em
  Infrastructure (pacotes `SkiaSharp` + `SkiaSharp.NativeAssets.Linux`).
- Cartões: **incidente** (vermelho), **aviso** (laranja), **resolução**
  (verde). Conteúdo: host, problema, severidade, valor atual, mínimo/máximo,
  linha tracejada do limite configurado, série histórica com escala/tempo,
  data/hora do evento, identidade visual escura uniforme.
- Sem dados de histórico suficiente → cartão completo sem gráfico, com aviso
  de "dados indisponíveis" (regra 6).
- Testes unit: PNG magic bytes, dimensões esperadas, os 3 kinds, série vazia,
  tema estável (TimeProvider fixo).

### F36-T003 — Adaptador Evolution API real
- `EvolutionWhatsAppNotificationDispatcher` substitui o Logging adapter na DI
  (só ativa quando `Evolution:BaseUrl` preenchido; senão mantém o logging com
  Warning — espelho do fallback do adaptador de e-mail).
- Mensagem de texto: endpoint de envio para número; mídia: envio de imagem
  base64 + caption para número **ou** `groupId` conforme o destino.
- Resposta/erro da Evolution registrados na delivery (Sent/Failed, correlation
  id ADR-021 no log). Credenciais só em env — nunca em log/exceção.
- Testes: contrato HTTP com stub (precedente do smoke F-T011c com HttpListener
  / fakes), config vazia = degradação, erro HTTP = Failed pt-BR.

### F36-T004 — Enriquecimento do gráfico (incidente → série)
- No despacho WhatsApp de um delivery de incidente/resolução: resolver o
  trigger do incidente → item → `IZabbixClient.GetHistoryAsync` (janela
  configurável, ex. últimas 2h) + valor/límite do trigger.
- Persistir o mínimo necessário no evento/incidente (triggerId/itemId quando
  disponíveis no payload do webhook) para o lookup não depender de parsing de
  título.
- Zabbix fora do ar ou sem histórico → segue o envio com cartão sem gráfico
  (nunca bloquear a notificação).
- Testes: enriquecimento ok, Zabbix indisponível, histórico vazio.

### F36-T005 — Ligações incidente→imagem→WhatsApp
- `NotificationWhatsAppWorker`: deliveries cuja notificação veio de
  `IncidentCreatedDomainEvent`/`IncidentResolvedDomainEvent` ganham mídia
  (render + envio); demais tipos continuam texto puro.
- Múltiplos destinos: uma delivery por destino já nasce do fanout (ou loop
  por destino no worker — decisão de implementação registrada na ADR);
  idempotência: mesma delivery nunca enviada duas vezes com sucesso
  (estado Sent é barreira).
- Testes integração: incidente aberto → PNG gerado → chamada Evolution stub
  com legenda+imagem para cada destino; resolução → cartão verde.

### F36-T006 — Cooldown, retry e DLQ
- Cooldown configurável por incidente (evitar rajada de PROBLEM repeats já
  deduplicados no ingest, mas cobrir reaberturas em janela curta).
- Retry progressivo + dead-letter no fluxo de notificação (configuração
  MassTransit; verificar estado atual — não achado explícito hoje).
- Testes: segunda ocorrência dentro do cooldown = suppress; retry estoura →
  DLQ + log.

### F36-T007 — UI de grupos (lote 2)
- Endpoint Administer: sincronizar/listar grupos da instância Evolution
  (fetch na API Evolution), seleção e persistência dos destinos
  (`IAuditableRequest`, envelope `ApiResponse`, policy Administer).
- Página/configuração no front com os grupos disponíveis e estado de envio de
  teste (exige autorização explícita, como pedido no plano original).

### F36-T008 — Laboratório antes da produção
Checklist do plano original, adaptado: host de laboratório; simular incidente,
recuperação, duplicata e falha de Evolution; validar gráficos e limites;
reiniciar containers e confirmar persistência da sessão Evolution; logs,
filas, latência; só então habilitar para os equipamentos de produção.

## 5. Regras de funcionamento

| Situação | Comportamento |
| --- | --- |
| Incidente crítico novo | Cartão vermelho (imagem + legenda) para cada destino |
| Evento repetido (dedupe no ingest) | Não gera novo incidente → sem novo envio |
| Incidente ativo com reabertura | Respeitar cooldown (F36-T006) |
| Incidente resolvido | Cartão verde, se o canal de resolução estiver habilitado |
| Evolution indisponível | Delivery `Failed` + erro pt-BR; worker segue; demais canais intactos |
| Zabbix sem histórico | Cartão sem gráfico, identificando dados indisponíveis |
| Destino sem grupo configurado | Não envia; erro de configuração claro |
| Outros canais | Independentes (fanout atual) |

> Nota do plano original mantida: Outbox+fila dão confiabilidade de
> **at-least-once**; entrega **exatamente-once** no WhatsApp não é garantível —
> a barreira é o estado da `NotificationDelivery` (Sent nunca reenvia).

## 6. Segurança

- Evolution URL/key/instance só em env (precedente ICredentialProtector não
  se aplica hoje — valor de config, não por-usuário).
- Webhook Zabbix continua autenticado (ADR-023, X-Zabbix-Token).
- Auditoria: comando de configuração de destinos usa `IAuditableRequest`
  (quem configurou o grupo, quando).
- Logs nunca imprimem chave da API nem payload completo de credencial.

## 7. Checklist de conclusão

- [ ] F36-T001 ADR-061 aceita
- [ ] F36-T002 Renderer SkiaSharp com os 3 cartões + testes
- [ ] F36-T003 Adaptador Evolution real com mídia e múltiplos destinos
- [ ] F36-T004 Enriquecimento trigger→histórico com fallback seguro
- [ ] F36-T005 Incidente/resolução → imagem → WhatsApp e2e (stub)
- [ ] F36-T006 Cooldown + retry/DLQ
- [ ] F36-T007 UI de seleção de grupos (lote 2)
- [ ] F36-T008 Laboratório aprovado antes da produção

## 8. Fora de escopo / pendências explícitas

- Criação de instância Evolution pelo backend (D1 — é externa).
- IA, navegador headless ou serviço pago de imagem (SkiaSharp puro).
- Mudança no pipeline Outbox/Rabbit (reutilizar o existente).
- Outros canais (Email/Telegram/SignalR/Webhook) — intocados.
- Publicação em grupo com menções/interações — futura.

## 9. Referências internas

- `docs/07-NOTIFICACOES.md` — canais, fanout, §Canal WhatsApp (F9-T007)
- `docs/06-MESSAGING-OUTBOX-RABBITMQ.md` — Outbox, MassTransit, inbox
- `docs/27-INTEGRACAO-ZABBIX.md`, `docs/37-F27-MONITORAMENTO-ZABBIX.md`
- `docs/adr/ADR-012-NOTIFICATIONS.md`, `ADR-023-ZABBIX-INBOUND-WEBHOOK.md`,
  `ADR-027-MONITORING-EVENT-INGESTION.md`
- ADR-061 (a criar com a F36-T001)
