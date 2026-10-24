# Estudo — Fluxo de Integração WhatsApp via Evolution API

> **Data**: 2026-10-19
> **Atualizado**: 2026-10-23
> **Estado**: ✅ Fluxo de gestão reforçado pela F38-T005 — configuração, ativação, instância, QR, grupos dinâmicos e teste dentro da plataforma; ver §10
> **Objetivo**: Mapear o fluxo completo do canal WhatsApp, os endpoints da Evolution API e o desenho de gestão dentro da plataforma

---

## 1. Estado Atual (resumo)

| Componente | Estado | Ficheiro |
|---|---|---|
| Evolution API no compose | ✅ Implementado (profile `evolution`) | `docker-compose.yml` |
| Dispatcher real (sendText) | ✅ Implementado (F33-T002) | `EvolutionApiWhatsAppNotificationDispatcher.cs` |
| Consumer (fanout + dedup) | ✅ Implementado (F33-T003) | `NotificationEventConsumer.cs` |
| Worker (retry + DLQ) | ✅ Implementado (F9-T009) | `NotificationWhatsAppWorker.cs` |
| Config WhatsApp (env/appsettings) | ✅ Implementado (fallback) | `appsettings.json` + `.env` |
| **Gestão de grupos (fetchAllGroups)** | ✅ Implementado (ADR-062) | `WhatsAppIntegrationController` + `EvolutionInstanceClient` |
| **Gestão de conexão (QR + status)** | ✅ Implementado (ADR-062) | `GenerateWhatsAppQrCommand` / `GetWhatsAppConnectionStateQuery` |
| **Persistência de settings no banco** | ✅ Implementado (migração `AddWhatsAppIntegrationSetting`) | `WhatsAppIntegrationSetting` + `WhatsAppOptionsResolver` |
| **Tela de gestão WhatsApp no frontend** | ✅ Implementado | `WhatsAppSettingsPage.tsx` (`/integrations/whatsapp`) |

---

## 2. Fluxo Atual (Zabbix → WhatsApp)

```
Zabbix (PROBLEM/OK)
  → POST /api/v1/integrations/zabbix/problems   [F16-T001]
  → MonitoringEventIngestor → Alert + Incident   [F19]
  → IncidentCreatedDomainEvent / IncidentResolvedDomainEvent / AlertResolvedDomainEvent
  → Outbox → RabbitMQ → NotificationEventConsumer [idempotente via Inbox]
  → NotificationDelivery (canal WhatsApp)
  → NotificationWhatsAppWorker (Api, retry 5× + DLQ)
  → IWhatsAppNotificationDispatcher
       → EvolutionApiWhatsAppNotificationDispatcher (REAL)
       → LoggingWhatsAppNotificationDispatcher (fallback sem BaseUrl)
```

---

## 3. Endpoints da Evolution API (mapeamento completo)

### 3.1 Gestão de Instância

| Operação | Método | Endpoint | Parâmetros | Resposta |
|---|---|---|---|---|
| **Criar instância** | `POST` | `/instance/create` | Body: `{ instanceName, integration, qrcode?, token?, number? }` | `201` — `{ instance, hash.apikey, settings }` |
| **Gerar QR** | `GET` | `/instance/connect/{instance}` | Query: `number?` | `200` — `{ pairingCode, code, base64, count }` (QR em base64) |
| **Estado da conexão** | `GET` | `/instance/connectionState/{instance}` | — | `200` — `{ state: "open" | "connecting" | "close" }` |
| **Reiniciar** | `POST` | `/instance/restart/{instance}` | — | `200` |
| **Desconectar** | `DELETE` | `/instance/logout/{instance}` | — | `200` (mantém instância, limpa credenciais) |
| **Eliminar** | `DELETE` | `/instance/delete/{instance}` | — | `200` (remove instância) |
| **Listar instâncias** | `GET` | `/instance/fetchInstances` | — | `200` — array |
| **Presença** | `POST` | `/instance/setPresence/{instance}` | Body: `{ presence }` | `201` |

### 3.2 Envio de Mensagens

| Operação | Método | Endpoint | Body obrigatório | Resposta |
|---|---|---|---|---|
| **Texto** | `POST` | `/message/sendText/{instance}` | `{ number, text }` | `201` — `{ key.remoteJid, message, status }` |
| **Mídia** | `POST` | `/message/sendMedia/{instance}` | `{ number, mediatype, mimetype, caption, media, fileName }` | `201` — `{ key, message.imageMessage }` |
| **Áudio** | `POST` | `/message/sendWhatsAppAudio/{instance}` | `{ number, audio }` | `201` |
| **Sticker** | `POST` | `/message/sendSticker/{instance}` | `{ number, sticker }` | `201` |
| **Localização** | `POST` | `/message/sendLocation/{instance}` | `{ number, latitude, longitude, name, address }` | `201` |
| **Contacto** | `POST` | `/message/sendContact/{instance}` | `{ number, contact[] }` | `201` |
| **Reação** | `POST` | `/message/sendReaction/{instance}` | `{ key, reaction }` | `201` |
| **Voto** | `POST` | `/message/sendPoll/{instance}` | `{ number, name, values[] }` | `201` |
| **Lista** | `POST` | `/message/sendList/{instance}` | `{ number, title, sections[] }` | `201` |
| **Botões** | `POST` | `/message/sendButtons/{instance}` | `{ number, title, buttons[] }` | `201` |

### 3.3 Gestão de Grupos

| Operação | Método | Endpoint | Parâmetros |
|---|---|---|---|
| **Listar grupos** | `GET` | `/group/fetchAllGroups/{instance}` | Query: `getParticipants` (boolean) |
| **Criar grupo** | `POST` | `/group/create/{instance}` | `{ subject, participants[] }` |
| **Renomear** | `POST` | `/group/updateGroupSubject/{instance}` | `{ groupJid, subject }` |
| **Participantes** | `GET` | `/group/participants/{instance}` | `{ groupJid }` |
| **Convidar** | `POST` | `/group/sendInvite/{instance}` | `{ groupJid, numbers[] }` |
| **Sair do grupo** | `DELETE` | `/group/leaveGroup/{instance}` | `{ groupJid }` |

---

## 4. Decisões de Arquitetura

### 4.1 Configuração persistida no banco (mesmo padrão do Zabbix)

O grupo escolhido na UI persiste no banco (`whatsapp_integration_settings`); o dispatcher/worker passa a resolver banco → fallback `.env` (hot-swap sem restart). A API key fica cifrada (AES-GCM, `ICredentialProtector` reutilizado).

### 4.2 Esquema real da tabela (migração `AddWhatsAppIntegrationSetting`)

```sql
CREATE TABLE whatsapp_integration_settings (
    id UUID PRIMARY KEY,
    base_url VARCHAR(512) NOT NULL,
    api_key_encrypted VARCHAR(2048) NOT NULL,   -- AES-GCM (ICredentialProtector)
    instance_id VARCHAR(128) NOT NULL,
    group_id VARCHAR(128),                      -- <id>@g.us escolhido na UI
    phone VARCHAR(64),                          -- fallback individual
    connection_state VARCHAR(32),               -- open | connecting | close
    last_tested_at_utc TIMESTAMPTZ,
    last_test_ok BOOLEAN NOT NULL,
    last_test_error VARCHAR(1000),
    notifications_enabled BOOLEAN NULL,             -- NULL = seguir fanout legado do ambiente
    create_date_time TIMESTAMPTZ NOT NULL,
    update_date_time TIMESTAMPTZ NOT NULL
);
-- A MESMA migração cria a coluna DomainUsers.FullName (varchar(200), nullable)
-- usada pelo auto-serviço de perfil (/auth/profile + /auth/me).
```

### 4.3 Endpoints da API do InfraControl — IMPLEMENTADOS

`WhatsAppIntegrationController` (`[Authorize(Policy = Administer)]` em todos, ADR-062):

| Operação | Método | Endpoint | Command / Query | Resposta |
|---|---|---|---|---|
| Obter config | `GET` | `/api/v1/integrations/whatsapp/config` | `GetWhatsAppSettingsQuery` | `WhatsAppSettingsDto` |
| Guardar config | `PUT` | `/api/v1/integrations/whatsapp/config` | `SaveWhatsAppSettingsCommand` | `WhatsAppSettingsDto` |
| Estado da conexão | `GET` | `/api/v1/integrations/whatsapp/connection-state` | `GetWhatsAppConnectionStateQuery` | `{ state }` |
| Gerar QR | `POST` | `/api/v1/integrations/whatsapp/connect` | `GenerateWhatsAppQrCommand` | `{ png (base64), pairingCode, state }` |
| Desconectar | `POST` | `/api/v1/integrations/whatsapp/disconnect` | `DisconnectWhatsAppInstanceCommand` | `{ state }` |
| Criar instância | `POST` | `/api/v1/integrations/whatsapp/instance` | `CreateWhatsAppInstanceCommand` | `{ instanceId, created, state }` |
| Listar grupos | `GET` | `/api/v1/integrations/whatsapp/groups` | `ListWhatsAppGroupsQuery` | `WhatsAppGroupDto[]` |
| Enviar teste | `POST` | `/api/v1/integrations/whatsapp/test` | `SendWhatsAppTestCommand` | `{ ok, error }` |

Contrato de gravação (`SaveWhatsAppSettingsRequest`): `BaseUrl`, `InstanceId`, `ApiKey?`,
`GroupId?`, `Phone?`, `NotificationsEnabled?` — **`ApiKey` omitida mantém a gravada**
(write-only, cifrada no servidor); ativação nullable preserva o fanout legado até escolha explícita.

---

## 5. Mapeamento de Endpoints para Classes .NET (implementado)

| Endpoint Evolução | Classe .NET | Camada |
|---|---|---|
| `GET /instance/connectionState/{instance}` | `IEvolutionInstanceClient.GetConnectionStateAsync()` → `EvolutionInstanceClient` | Infrastructure |
| `GET /instance/connect/{instance}` | `IEvolutionInstanceClient.ConnectAsync()` → `EvolutionQrResult(Base64, PairingCode)` | Infrastructure |
| `DELETE /instance/logout/{instance}` | `IEvolutionInstanceClient.LogoutAsync()` | Infrastructure |
| `POST /instance/create` (body) | `IEvolutionInstanceClient.CreateInstanceAsync()` | Infrastructure |
| `GET /group/fetchAllGroups/{instance}` | `IEvolutionInstanceClient.ListGroupsAsync()` → `EvolutionGroupDto` | Infrastructure |
| `POST /message/sendText/{instance}` | `IEvolutionInstanceClient.SendTextAsync()` (dispatcher, ADR-059) | Infrastructure |

Camada de aplicação: `WhatsAppSettingsCommands.cs` (config + teste) e
`WhatsAppInstanceCommands.cs` (QR, estado, instância, grupos) — todos
`BaseCommand`/`BaseQuery` + `IAuthorizedRequest` (ADR-062 §política), com tag
de cache `whatsapp-settings` na query e `CacheKeysToInvalidate` no save.
Resolução efetiva da config (banco → fallback `.env`) = `WhatsAppOptionsResolver`.

---

## 6. UI — WhatsAppSettingsPage (implementada)

Rota `/integrations/whatsapp` (sidebar **Administração → WhatsApp**, nível
`administer` em `access.ts`, espelhando a policy do controller). Serviço:
`src/services/api/whatsapp.ts`.

```
┌─────────────────────────────────────────────────┐
│  WhatsApp                                      │
│  Administração → Integrações · Evolution API    │
├─────────────────────────────────────────────────┤
│ Conexão                                         │
│   Estado: [Conectado|Conectando…|Desconectado]  │  ← poll 5s (para em "open")
│   [Gerar QR Code] [Criar instância] [Desconectar]│
│   ┌─────────┐                                   │
│   │ ███████ │  data:image/png;base64 (PNG)      │
│   │ █ █ █ █ │  código de pareamento (opcional)  │
│   │ ███████ │                                   │
│   └─────────┘                                   │
├─────────────────────────────────────────────────┤
│ Configuração                                    │
│   URL da Evolution API │ Nome da instância       │
│   ApiKey (write-only)  │ Telefone (fallback)    │
│   [ ] Ativar notificações automáticas            │
│   [Salvar]  [Enviar mensagem de teste]          │
├─────────────────────────────────────────────────┤
│ Grupos do número        [Carregar grupos]       │
│   (radio) ● Equipe atual — 120363…@g.us · 12    │
│   (radio) ○ Estudo Arquitetura …                │
│   Destino atual: 120363…@g.us — grave a config. │
├─────────────────────────────────────────────────┤
│ Estado                                          │
│   Fonte (banco/env) · ApiKey · Notificações ·   │
│   Grupo de destino · Último teste · Atualização │
└─────────────────────────────────────────────────┘
```

Regras da tela (trancadas em `WhatsAppSettingsPage.test.tsx`):
- ApiKey **nunca** populada a partir do servidor (só o indicador `apiKeySet`);
- criar/conectar/carregar grupos exige configuração persistida e válida;
- criação identifica a instância e diferencia criada de já existente; HTTP 400
  é falha, não confirmação;
- QR exibido como `data:image/png;base64,${png}` (o `byte[]` do DTO chega
  como string base64 no JSON);
- grupo selecionado vai no `PUT /config`; chave em branco vira `null` (mantém);
- grupos não são pré-cadastrados: vêm da Evolution conectada; destino pendente
  não é tratado como destino salvo;
- toggle automático é persistido e muda o fanout sem editar `NOTIFY_DEFAULT_*`;
- falha do teste aparece explícita (`role="status"`), não como sucesso.

---

## 7. Seq de Conexão (QR) — fluxo implementado

```
1. Admin abre /integrations/whatsapp
   → GET /config (banco → fallback legado env)
   → GET /connection-state (poll de 5s enquanto state ≠ "open")
2. Admin informa URL, ApiKey da Evolution e nome escolhido; salva no banco
   → operações ficam habilitadas somente quando essa configuração está salva
3. Clica "Criar instância" → POST /integrations/whatsapp/instance
   → `{ instanceId, created, state }`; erro HTTP é exibido, sem sucesso falso
4. Clica "Gerar QR" → POST /integrations/whatsapp/connect
   → GenerateWhatsAppQrCommand: logout best-effort + GET /instance/connect/{instance}
     (workaround #1408 do spike F33-T001) → { png (base64), pairingCode }
5. UI monta data:image/png;base64 e exibe a imagem; admin escaneia no celular
6. O poll em /connection-state passa a "open" → chip "Conectado"
7. "Carregar grupos" → GET /groups → grupos reais da Evolution; o admin marca
   o grupo → "Salvar" persiste `groupId` e a ativação de notificações desejada
8. "Enviar mensagem de teste" → POST /test → Grava last_tested_at_utc /
   last_test_ok / last_test_error na linha de configuração (voltam no /config)
9. Ativação explícita adiciona/remove WhatsApp no fanout sem restart; valor
   `NULL` no banco mantém compatibilidade com `Notifications:Channels:Default`.
```

---

## 8. Referências

- Documentação oficial: https://doc.evolution-api.com/v2/
- Repo GitHub: https://github.com/EvolutionAPI/evolution-api
- ADR-059 (canal WhatsApp aceito): `docs/adr/ADR-059-WHATSAPP-EVOLUTION-API.md`
- ADR-062 (gestão dentro da plataforma): `docs/adr/ADR-062-WHATSAPP-PLATFORM-MANAGEMENT.md`
- F33-T005 (gráfico + sendMedia): `docs/56-PLANO-CANAL-WHATSAPP-IMAGEM.md`
- Runbook operacional: `docs/60-RUNBOOK-WHATSAPP-EVOLUTION.md`
- Zabbix settings (padrão a espelhar): `ZabbixIntegrationSetting` no código

---

## 9. O que mudou desde o estudo (2026-10-22)

**Backend (ADR-062) — implementado em sessão anterior:**

- Entidade `WhatsAppIntegrationSetting` + `IWhatsAppSettingsRepository`/`WhatsAppSettingsRepository`
  (auto-registro) + migração `20261021011907_AddWhatsAppIntegrationSetting`
  (cria `whatsapp_integration_settings` **e** a coluna `DomainUsers.FullName`);
- portas `IEvolutionInstanceClient`, `IWhatsAppOptionsResolver` (+ resolver
  banco→env `WhatsAppOptionsResolver` e `WhatsAppEnvOptionsAccessor`);
- CQRS: `WhatsAppSettingsCommands.cs` (get/save/test) e
  `WhatsAppInstanceCommands.cs` (QR/estado/instância/grupos), todos com
  `BaseCommand`/`BaseQuery` + `RoleAccess.Administer`;
- `WhatsAppIntegrationController` com os 8 endpoints reais (§4.3);
- DTOs: `WhatsAppSettingsDto`, `WhatsAppQrDto`, `WhatsAppConnectionStateDto`,
  `WhatsAppGroupDto`, `WhatsAppTestResultDto`.

**Frontend — implementado agora:**

- `services/api/whatsapp.ts` (8 funções espelhando os endpoints);
- `pages/WhatsAppSettingsPage.tsx` + rota `/integrations/whatsapp`
  (lazy import, `ROUTE_ACCESS["/integrations/whatsapp"] = "administer"`, item
  **Administração → WhatsApp** com `pi pi-whatsapp` no `dashboardData.ts`);
- `WhatsAppSettingsPage.test.tsx` (7 testes: ApiKey write-only, QR base64 →
  data-URI, envio do groupId, save desabilitado sem baseUrl, lista de grupos,
  falha do teste explícita).

**Pendências conhecidas:**

1. `FullName` foi ligado ao auto-serviço (`/auth/profile` + `/auth/me` via claim
   `fullName` + campo em Minha Conta), mas o **PUT /admin/users/{id} ainda não
   edita** o nome completo — o handler apenas preserva o valor existente;
2. smoke em ambiente real (instância `Admin` + grupo "Estudo Arquitetura")
   depende de Evolution/Zabbix ativos — mesmo pendência do F33-T006;
3. A conclusão do fluxo depende ainda de smoke test no ambiente real
   (instância Evolution conectada e grupo disponível).

## 10. F38-T005 — conclusão do fluxo de gestão (2026-10-23)

- A criação agora retorna `{ instanceId, created, state }`; `409` é
  reconhecido como instância existente e HTTP 400/erros restantes falham.
- A tela exige configuração salva antes de criar/conectar/buscar grupos,
  mostra claramente o nome/resultado da instância e protege a sessão conectada
  de logout acidental ao gerar QR.
- A substituição do nome da instância exige confirmação explícita. A confirmação
  explica que a instância remota antiga não é apagada; o novo cadastro limpa
  grupo/telefone anteriores e mantém notificações automáticas desligadas até
  uma ativação posterior pelo administrador.
- O grupo precisa vir da consulta ao vivo da Evolution e a tela distingue
  seleção não salva de destino persistido.
- `notifications_enabled` nullable foi adicionado por migração. A escolha
  explícita da plataforma controla o fanout WhatsApp sem reiniciar serviços;
  `NULL` mantém o comportamento legado de ambiente.
- `.env.example` não fixa mais `WHATSAPP_INSTANCE=Admin`; URL, nome e grupo
  continuam editáveis na tela. O Compose continua responsável pela instalação
  dos containers e a ApiKey informada na plataforma deve coincidir com a
  configurada na Evolution.
- Escopo: singleton (uma instância/número por instalação). Múltiplas
  instâncias e gestão do ciclo de vida dos containers não fazem parte da F38-T005.

### Validação no ambiente Docker de desenvolvimento

- Ajuste de substituição documentado na seção 11 da ADR-062: confirmação,
  limpeza do destino antigo, automação inicialmente desligada, sem apagar a
  instância anterior da Evolution.
- Testes frontend focados: 12/12; lint e build concluídos. A imagem e o
  container Frontend foram atualizados novamente; container saudável e a rota
  da tela WhatsApp responde HTTP 200.
- Migration `20261023170132_AddWhatsAppNotificationToggle` aplicada após
  geração e validação de backup do banco.
- Imagens e containers `api`, `workers` e `frontend` reconstruídos; os três
  ficaram saudáveis e `/health/ready` respondeu HTTP 200.
- `evolution-api` mantida na imagem pinada `evoapicloud/evolution-api:v2.3.7`;
  endpoint respondeu HTTP 200, porém nenhuma instância está conectada.
- A configuração persistida mantém notificações automáticas desativadas.
  Nenhuma mensagem de teste foi enviada. O smoke end-to-end depende de parear
  o WhatsApp, selecionar/confirmar o grupo e executar o teste pela tela.

### Trocar o número configurado

1. Na configuração, altere o nome da instância e salve. A tela pedirá
   confirmação da substituição.
2. Confirmar salva a nova instância sem reutilizar grupo/telefone anteriores e
   desativa notificações automáticas; cancelar não grava a alteração.
3. Após confirmar, crie/identifique a instância, gere o QR e pareie o novo
   número.
4. Carregue os grupos desse número conectado, selecione o destino, salve e
   envie o teste. Só então, se desejado, habilite notificações automáticas.
5. A instância antiga permanece na Evolution; a plataforma não a apaga nem
   desconecta automaticamente.
