# ADR-062: Canal WhatsApp gerenciado pela plataforma — config persistida em banco + conexão (QR), grupos e teste na UI

- **Status**: **aceito (desenho) — usuário direcionou "ajusta e começa a executar" em 2026-10-19; libera fatia 1 (backend)**
- **Data**: 2026-10-19
- **Relacionado**: ADR-059 (transporte via Evolution API, texto+gráfico), ADR-023/ADR-027 (eventos Zabbix), precedente de settings persistidas: `UniFiIntegrationSetting` (F26, `docs/36`) e `VCenterIntegrationSetting` (ADR-058 §3 — "espelho byte a byte do UniFi").

## Contexto

O canal WhatsApp já funciona de ponta a ponta (F33-T002..T005, ADR-059):
dispatcher real (sendText + sendMedia com gráfico Skia), worker com destino
grupo, consumer com dedup de broadcast e `Notification.DeviceId` para a série
do Zabbix. **Mas toda a configuração vive em `.env`/appsettings** (env), e a
conexão do WhatsApp (instância + QR) é feita **fora da plataforma**, no
manager UI da Evolution API.

O usuário quer que **tudo passe a viver dentro da plataforma**:

> "o grupo definido após criação fica no banco; tem que ter um processo
> gerenciando e comunicando com a Evolution API" — e a conexão (QR) também
> dentro da UI, sem depender do manager externo.

### Estado atual (o que existe hoje)

**Config**: `WhatsAppChannelOptions` (`Notifications:Channels:WhatsApp`: Phone,
GroupId, BaseUrl, ApiKey, InstanceId) é lida de **env** via
`IOptions<NotificationChannelsOptions>` em **4 pontos**:

| Ponto | Arquivo | O que lê |
|---|---|---|
| Worker | `src/InfraControl.Api/Workers/NotificationWhatsAppWorker.cs:30` | `GroupId ?? Phone` (destino) |
| Dispatcher | `src/InfraControl.Infrastructure/Services/EvolutionApiWhatsAppNotificationDispatcher.cs:73` | `BaseUrl`, `ApiKey`, `InstanceId`, `GroupId` |
| Factory DI | `src/InfraControl.Infrastructure/Services/DependencyInjection.cs:247` | `BaseUrl` (escolhe Logging vs Evolution) |
| Consumer | `src/InfraControl.Workers/Consumers/Notifications/NotificationEventConsumer.cs:391,449` | `IsSendable` (GroupId ou Phone) + `groupBroadcast` (dedup) |

**Conexão/QR**: hoje é manual, fora da plataforma:

```text
usuário abre manager UI da Evolution (http://localhost:18081/manager)
  → informa apikey
  → cria/conecta instância "Admin"
  → QR exibido no manager → escaneia no celular
  → connectionState vira "open"
```

A plataforma **não tem** nenhuma tela de WhatsApp, nenhum client de gestão
(só o dispatcher de envio), nenhum endpoint de settings.

### Endpoints da Evolution API já validados (spike F33-T001/T005)

| Endpoint | Método | Uso | Status spike |
|---|---|---|---|
| `/message/sendText/{instance}` | POST | envio texto (`{number,text}` + header `apikey`) | ✅ 201 |
| `/message/sendMedia/{instance}` | POST | envio mídia (`{number,mediatype,mimetype,media,caption,fileName}`) | ✅ 201 |
| `/group/fetchAllGroups/{instance}?getParticipants=false` | GET | lista grupos (`id`=`@g.us`, `subject`, `size`, `owner`, `desc`, `restrict`, `announce`) | ✅ 117 grupos |
| `/instance/connectionState/{instance}` | GET | estado (`open`/`connecting`/`close`) | ✅ poll ok |
| `/instance/connect/{instance}` | GET | gera QR (devolve `base64` = data-URI PNG) + pairing code | ✅ decodificado |
| `/instance/create` | POST | cria instância (`{instanceName,integration:"WHATSAPP-BAILEYS",qrcode:true}`) | ✅ (rota sem path param) |
| `/instance/logout/{instance}` | DELETE | desconecta (workaround #1408 p/ regenerar QR) | ✅ |

> Nota: a rota de criação real é `POST /instance/create` (body), **não**
> `/instance/create/{instance}` (404). Workaround #1408: `logout` → `connect`
> para regenerar o QR quando a instância fica `close`/`connecting`.

## Decisão

Criar **gestão completa do canal WhatsApp dentro da plataforma**, com config
persistida em banco (padrão singleton `*IntegrationSetting`, precedente UniFi/
Zabbix/VCenter) e uma tela de Integração → WhatsApp que conversa com a
Evolution API. A fonte de verdade do destino (GroupId) passa a ser o **banco**,
com **fallback para env** enquanto não houver linha (transição sem quebrar).

### Novas peças — visão geral

```text
┌───────────────────────────── FRONTEND (React) ─────────────────────────────┐
│  WhatsAppSettingsPage  /integrations/whatsapp                              │
│  ├─ Conexão: status (poll), botão "Gerar QR" (exibe PNG), "Desconectar"    │
│  ├─ Config: BaseUrl / ApiKey(write-only) / InstanceId / Phone              │
│  ├─ Grupos: botão "Carregar grupos" → lista (subject + @g.us + size)       │
│  │          → selecionar grupo → salva GroupId no banco                    │
│  └─ Teste: botão "Enviar teste" no grupo escolhido (201/erro na tela)      │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │ /api/v1/integrations/whatsapp (Administer)
┌─────────────────────────────────────▼───────────────────────────────────────┐
│  WhatsAppIntegrationController                                              │
│   GET  config            → GetWhatsAppSettingsQuery                         │
│   PUT  config            → SaveWhatsAppSettingsCommand                      │
│   GET  connection-state  → GetWhatsAppConnectionStateQuery                  │
│   POST connect (QR)      → GenerateWhatsAppQrCommand                        │
│   POST disconnect        → DisconnectWhatsAppInstanceCommand                │
│   GET  groups            → ListWhatsAppGroupsQuery                          │
│   POST test              → SendWhatsAppTestCommand                          │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │ CQRS (BaseQuery/BaseCommand + IAuthorizedRequest)
┌─────────────────────────────────────▼───────────────────────────────────────┐
│  Application/Integrations/WhatsApp/                                         │
│   WhatsAppSettingsCommands.cs (Get/Save/Disconnect/Connect)                 │
│   WhatsAppGroupsQueries.cs (ListGroups/Test/ConnectionState)                │
│   Ports/: IWhatsAppSettingsRepository, IWhatsAppOptionsResolver,            │
│           IEvolutionInstanceClient                                          │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │
┌─────────────────────────────────────▼───────────────────────────────────────┐
│  Infrastructure                                                             │
│   Integrations/WhatsApp/WhatsAppOptionsResolver.cs  (banco → fallback env)  │
│   Integrations/WhatsApp/EvolutionInstanceClient.cs  (HttpClient "evolution- │
│     api": create/connect/logout/connectionState/fetchAllGroups/sendText)    │
│   Repositories/WhatsAppSettingsRepository.cs        (auto-registro F0-T024) │
│   Domain/Entities/WhatsAppIntegrationSetting.cs     (singleton 1 linha)     │
│   Persistence/Configurations/WhatsAppIntegrationSettingConfiguration.cs      │
│   Persistence/Migrations/AddWhatsAppIntegrationSetting (+ DbSet)            │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Entidade `WhatsAppIntegrationSetting` (singleton — 1 linha)

| Coluna (snake_case) | Tipo | Obs |
|---|---|---|
| `id` | uuid | PK |
| `base_url` | varchar(512) | URL da Evolution (ex.: `http://evolution-api:8080`) |
| `api_key_encrypted` | varchar(2048) | **cifrada** AES-GCM (`ICredentialProtector`), write-only |
| `instance_id` | varchar(128) | nome da instância (ex.: `Admin`) |
| `group_id` | varchar(128) null | `@g.us` escolhido na UI — **destino** |
| `phone` | varchar(64) null | fallback individual (retrocompatível) |
| `connection_state` | varchar(32) null | último estado visto (`open`/`connecting`/`close`) |
| `last_tested_at_utc` | timestamptz null | |
| `last_test_ok` | bool | |
| `last_test_error` | varchar(1000) null | |
| `create_date_time` / `update_date_time` | timestamptz | da base `Entity` |

Validações de domínio: `BaseUrl` http/https absoluta; `ApiKeyEncrypted` nulo =
mantém a gravada (roteamento opcional, igual Zabbix); `GroupId` se preenchido
termina em `@g.us`.

### Accessor/Resolver (padrão `ZabbixOptionsResolver`)

`IWhatsAppOptionsResolver.ResolveAsync(ct)`:
- há linha no banco → devolve options materializadas (`BaseUrl`, `ApiKey`
  decifrada via `UnprotectQuietly`, `InstanceId`, `GroupId`, `Phone`)
- senão → `IOptions<NotificationChannelsOptions>.Value.WhatsApp` (env)

Registrado **Scoped**; o `EvolutionInstanceClient` e o dispatcher passam a
obter as options por esse resolver (por request), permitindo **trocar o grupo
sem restart**.

### Rewire dos 4 pontos de leitura (banco → env)

Hoje os 4 pontos leem `IOptions` estático. Passarão a ler via resolver
(Scoped), mantendo o fallback env:

1. **Worker** (`NotificationWhatsAppWorker`) — destino `GroupId ?? Phone` do resolver
2. **Dispatcher** (`EvolutionApiWhatsAppNotificationDispatcher`) — `BaseUrl/ApiKey/InstanceId/GroupId` do resolver (elegível a `IOptions` no ctor → resolver)
3. **Factory DI** (`DependencyInjection.cs:247`) — escolha Logging vs Evolution via resolver
4. **Consumer** (`NotificationEventConsumer`) — `IsSendable` + `groupBroadcast` via resolver

> Como o resolver é Scoped e o consumer/worker rodam por mensagem, a resolução
> é por delivery — troca de grupo no banco reflete na próxima mensagem.

### Fluxo de conexão (QR) dentro da plataforma

```text
UI "Gerar QR"
  → POST /api/v1/integrations/whatsapp/connect        [Administer]
  → GenerateWhatsAppQrCommand
  → EvolutionInstanceClient: logout (se close) → GET /instance/connect/{instance}
  → devolve base64 (data-URI PNG) + pairingCode
  → UI decodifica e exibe a imagem do QR
usuário escaneia no celular (WhatsApp → aparelhos conectados)
UI faz poll: GET /connection-state
  → GET /instance/connectionState/{instance} → "open"
  → grava connection_state = "open" no banco (e mostra "Conectado")
```

### Fluxo de seleção de grupo

```text
UI "Carregar grupos"
  → GET /api/v1/integrations/whatsapp/groups           [Administer]
  → ListWhatsAppGroupsQuery
  → EvolutionInstanceClient: GET /group/fetchAllGroups/{instance}?getParticipants=false
  → lista de DTOs { id(@g.us), subject, size, owner, desc, restrict, announce }
UI mostra a lista; usuário clica num grupo
  → PUT /config { ..., groupId: "1203...@g.us" }       [Administer]
  → SaveWhatsAppSettingsCommand → banco (upsert singleton)
```

### Fluxo de teste

```text
UI "Enviar teste"
  → POST /api/v1/integrations/whatsapp/test            [Administer]
  → SendWhatsAppTestCommand
  → resolver lê settings (banco/env) → alvo = GroupId ?? Phone
  → EvolutionInstanceClient: POST /message/sendText/{instance} {number, text}
  → 201 → grava last_test_ok=true; erro → last_test_error (pt-BR, sem ApiKey)
  → devolve resultado na UI
```

### Fluxo de envio de eventos (INALTERADO — só muda a fonte do destino)

O pipeline de notificação **não muda** (decisão 5 do ADR-059: proibido atalho).
Muda apenas que `GroupId`/`Phone` agora vêm do resolver (banco → env):

```text
Zabbix PROBLEM → POST /api/v1/integrations/zabbix/problems
  → IncidentCreatedDomainEvent / AlertCreatedDomainEvent
  → Outbox → RabbitMQ → NotificationEventConsumer (fanout/dedup groupBroadcast)
  → NotificationDelivery (WhatsApp) → NotificationWhatsAppWorker
  → IWhatsAppNotificationDispatcher (EvolutionApi)
      destino = resolver.GroupId ?? resolver.Phone     ← AGORA do banco
      gráfico (Chart) → sendMedia, senão sendText
```

## Decisões do usuário (2026-10-19)

1. **Grupo persiste no banco** após criação/seleção — confirmado.
2. **Conexão (QR) dentro da plataforma** — confirmado ("tudo dentro da
   plataforma; como vai ser o QR?").
3. **Fatias**: **fatia 1 = backend completo** (persistência + client + CQRS +
   controller + **conexão/QR** + rewire); **fatia 2 = frontend** (tela).
   QR/conexão entra já na fatia 1 (backend expõe os endpoints; a UI os usa
   na fatia 2).

## Alternativas consideradas

1. **Manter só `.env`** — rejeitado: exigiria reiniciar a API pra trocar grupo;
   não atende "gerenciar pela plataforma".
2. **Bot/webhook separado pra capturar group_id** — rejeitado pelo usuário:
   "não seria via webhook; a ideia é a plataforma conversar com a Evolution".
3. **Config por arquivo/editável sem banco** — rejeitado: sem auditoria, sem
   UI nativa, frágil em container.

## Riscos e mitigações

| Risco | Mitigação |
|---|---|
| Baileys instável (disconnect aleatório) | UI de status + botão reconectar/QR; runbook F33-T006 |
| ApiKey vazando em log/erro | write-only no DTO; `UnprotectQuietly`; nunca logar header `apikey` |
| Troca de grupo em produção afeta envio | resolver por request; fallback env se linha apagada |
| `fetchAllGroups` lento com muitos grupos | `getParticipants=false`; paginação futura se preciso |

## Itens a validar antes do código (spike desta ADR)

- [x] `POST /instance/create` (body) e `GET /instance/connect` retornam QR
      utilizável **a partir da plataforma** (já validado no spike manual).
- [ ] Resolver banco→env não quebra o envio existente (regressão F33-T002..T005).
- [x] `fetchAllGroups` com `getParticipants=false` responde em tempo aceitável.
- [x] Decisão: QR/conexão na **fatia 1 (backend)**.

## Referências

- ADR-059 — transporte Evolution API, texto + gráfico, decisões 1–7
- `docs/36-F26-INTEGRACAO-UNIFI.md` §2.2 — precedente `*IntegrationSetting`
- ADR-058 §3 — VCenter espelha UniFi (padrão formalizado)
- `src/InfraControl.Infrastructure/Integrations/Zabbix/ZabbixOptionsResolver.cs` — fallback banco→env
- `src/InfraControl.Api/Controllers/Integrations/ZabbixIntegrationController.cs` — controller settings (policy Administer)
