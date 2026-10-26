# F46-T004 — Integridade front↔banco + tempo real SignalR

> Fase: F46 (engenharia de produto) · Data: 2026-10-25 · Status: **concluída** (com **1 bug real corrigido**)
> Ambiente: host E2E real (`E2EHost`: `Program` completo + Postgres/Redis reais).

## 1. O que foi validado (3 testes E2E novos — `DataIntegrityRealtimeE2ETests`)

### 1.1 Escrita → outbox sem perda (mesma transação)
`POST /incidents` pela API:
- incidente persiste no banco ✅
- `OutboxMessage` com `IncidentCreatedDomainEvent` é gravado **na mesma transação** ✅
- payload do evento carrega o `id` do incidente (sem perda) ✅
- `ProcessedAt` nulo (o dispatcher é outro processo — design outbox ADR-005) ✅
- **`CorrelationId` preenchido** (ADR-021) — só após o bugfix do §2 ✅

### 1.2 Tempo real: evento → fila → worker → hub → cliente
Fluxo E2E real, sem mocks:
1. `NotificationEventConsumer` (o mesmo do RabbitMQ) enfileira delivery pendente para o usuário logado;
2. `NotificationSignalRWorker` (hosted service real da API, poll 2s) despacha;
3. **cliente SignalR autenticado por cookie** conectado ao `/hubs/notifications` recebe o push no grupo `user:{userId}` com payload correto (`deliveryId`, `notificationId`, `title`, `eventType`, `createdAt`) ✅;
4. delivery é marcada `Sent` no banco após o push ✅.

> O negotiate do SignalR exige o header `X-XSRF-TOKEN` (é um POST autenticado pelo `AntiforgeryCookieMiddleware`, ADR-017) — o front já envia (`services/signalr/connection.ts`); o teste replica o mesmo caminho.

### 1.3 Deduplicação (inbox)
Reentregar o MESMO domain event (retry de broker) → **1 única notification/delivery** e **1 registro** em `ProcessedMessages` — sem duplicação de efeito ✅.

## 2. Bug real encontrado e corrigido

**`HttpContextCorrelation` nunca era registrado no DI.**
- `AddInfrastructure` registra `NullCurrentCorrelation` como fallback (para workers);
- o comentário previa que "a API sobrescreve, registrada mais tarde no Program.cs" — **mas o registro não existia**;
- efeito: **todo `OutboxMessage` da API nascia com `CorrelationId = null`**, exatamente o rastro que o ADR-021 define como obrigatório entre API e workers.

Correção (`src/InfraControl.Api/Program.cs`):
```csharp
builder.Services.AddScoped<ICurrentCorrelation, HttpContextCorrelation>();
```
(validado pelo teste 1.1, que passou a exigir o campo).

## 3. Outros achados (sem ação imediata)

| Achado | Avaliação |
|---|---|
| Negociate do SignalR cai no antiforgery (POST autenticado) | Comportamento **correto e já contornado no front** (header X-XSRF-TOKEN). Documentado no teste. |
| Consumer com construtor compacto de 4 args é testável sem RabbitMQ | Bom para E2E; fluxo consumer→worker→hub 100% coberto. |
| Worker de canal roda dentro da API (`NotificationSignalRWorker`) | Em produção o poll (2s) é o limitante de latência do push — ok para notificações. |

## 4. Cobertura permanente

Suíte `DataIntegrityRealtimeE2ETests` (roda no CI de testes): regressões de outbox, correlation, dedup ou do caminho SignalR quebram o build de testes.
