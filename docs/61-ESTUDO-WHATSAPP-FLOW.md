# Estudo — Fluxo de Integração WhatsApp via Evolution API

> **Data**: 2026-10-19
> **Estado**: Em estudo — pré-implementação
> **Objetivo**: Mapear o fluxo completo do canal WhatsApp, os endpoints da Evolution API e o desenho de gestão dentro da plataforma

---

## 1. Estado Atual (resumo)

| Componente | Estado | Ficheiro |
|---|---|---|
| Evolution API no compose | ✅ Implementado (profile `evolution`) | `docker-compose.yml` |
| Dispatcher real (sendText) | ✅ Implementado (F33-T002) | `EvolutionApiWhatsAppNotificationDispatcher.cs` |
| Consumer (fanout + dedup) | ✅ Implementado (F33-T003) | `NotificationEventConsumer.cs` |
| Worker (retry + DLQ) | ✅ Implementado (F9-T009) | `NotificationWhatsAppWorker.cs` |
| Config WhatsApp (env/appsettings) | ✅ Implementado | `appsettings.json` + `.env` |
| **Gestão de grupos (fetchAllGroups)** | ❌ Não existe | — |
| **Gestão de conexão (QR + status)** | ❌ Não existe | — |
| **Persistência de settings no banco** | ❌ Não existe | — |
| **Tela de gestão WhatsApp no frontend** | ❌ Não existe | — |

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

### 4.2 Esquema proposto da tabela

```sql
CREATE TABLE whatsapp_integration_settings (
    id UUID PRIMARY KEY,
    base_url VARCHAR(500) NOT NULL,
    api_key_encrypted TEXT NOT NULL,
    instance_id VARCHAR(200) NOT NULL,
    group_id VARCHAR(200),          -- <id>@g.us escolhido na UI
    phone VARCHAR(50),              -- fallback individual
    connection_state VARCHAR(50),   -- open | connecting | close
    last_tested_at_utc TIMESTAMPTZ,
    last_test_ok BOOLEAN,
    last_test_error TEXT,
    created_at_utc TIMESTAMPTZ NOT NULL,
    updated_at_utc TIMESTAMPTZ NOT NULL
);
```

### 4.3 Endpoints da API do InfraControl (propostos)

| Operação | Método | Endpoint | Papel |
|---|---|---|---|
| Obter settings | `GET` | `/api/v1/integrations/whatsapp/settings` | Admin |
| Guardar settings | `PUT` | `/api/v1/integrations/whatsapp/settings` | Admin |
| Listar grupos | `GET` | `/api/v1/integrations/whatsapp/groups` | Admin |
| Estado da conexão | `GET` | `/api/v1/integrations/whatsapp/status` | Admin |
| Gerar QR | `POST` | `/api/v1/integrations/whatsapp/qr` | Admin |
| Desconectar | `POST` | `/api/v1/integrations/whatsapp/logout` | Admin |
| Enviar teste | `POST` | `/api/v1/integrations/whatsapp/test` | Admin |

---

## 5. Mapeamento de Endpoints para Classes .NET

| Endpoint Evolução | Classe .NET proposta | Camada |
|---|---|---|
| `GET /instance/connectionState/{instance}` | `IEvolutionClient.GetConnectionStateAsync()` | Infrastructure |
| `GET /instance/connect/{instance}` | `IEvolutionClient.GetQrCodeAsync()` | Infrastructure |
| `DELETE /instance/logout/{instance}` | `IEvolutionClient.LogoutAsync()` | Infrastructure |
| `GET /group/fetchAllGroups/{instance}` | `IEvolutionClient.ListGroupsAsync()` | Infrastructure |
| `POST /message/sendText/{instance}` | `IEvolutionClient.SendTextAsync()` | Infrastructure |
| `POST /message/sendMedia/{instance}` | `IEvolutionClient.SendMediaAsync()` | Infrastructure |

---

## 6. UI Proposta — WhatsAppSettingsPage

```
┌─────────────────────────────────────────────────┐
│  Integração WhatsApp                            │
├─────────────────────────────────────────────────┤
│ [Status da instância: ● Conectado / ○ Desconectado] │
│                                                 │
│ Configuração                                    │
│   BaseUrl: [____________________]               │
│   ApiKey:  [____________________] (cifrada)     │
│   InstanceId: [____________________]             │
│                                                 │
│ Conexão                                         │
│   [Gerar QR Code]  [Desconectar]               │
│   ┌─────────┐                                   │
│   │ ███████ │  <imagem QR>                      │
│   │ █ █ █ █ │                                   │
│   │ ███████ │                                   │
│   └─────────┘                                   │
│                                                 │
│ Destino do grupo                                │
│   [Carregar grupos]                              │
│   ┌─────────────────────────────────┐           │
│   │ ○ Estudo Arquitetura            │           │
│   │ ● Bnnnn (120363...@g.us)         │ ← escolhido│
│   │ ○ Outro grupo...                │           │
│   └─────────────────────────────────┘           │
│                                                 │
│ [Enviar mensagem de teste]                      │
│                                                 │
│ [Guardar configuração]                          │
└─────────────────────────────────────────────────┘
```

---

## 7. Seq de Conexão (QR)

```
1. Admin abre a tela → clica "Gerar QR"
2. Plataforma chama GET /instance/connect/{instance}
3. Evolution retorna { base64: "data:image/png;base64,..." }
4. UI decodifica e mostra a imagem
5. Admin escaneia com o celular (WhatsApp → Aparelhos conectados)
6. UI faz poll a GET /instance/connectionState/{instance} (a cada 2s)
7. Estado muda para "open" → UI mostra "Conectado"
8. Admin clica "Guardar" → settings persistidas no banco
```

---

## 8. Referências

- Documentação oficial: https://doc.evolution-api.com/v2/
- Repo GitHub: https://github.com/EvolutionAPI/evolution-api
- ADR-059 (canal WhatsApp aceito): `docs/adr/ADR-059-WHATSAPP-EVOLUTION-API.md`
- F33-T005 (gráfico + sendMedia): `docs/56-PLANO-CANAL-WHATSAPP-IMAGEM.md`
- Zabbix settings (padrão a espelhar): `ZabbixIntegrationSetting` no código
