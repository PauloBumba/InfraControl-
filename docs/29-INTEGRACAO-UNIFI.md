# 29. Integração UniFi Controller (saída)

- **Task:** F20-T001 — Cliente API REST UniFi Controller & Descoberta (telemetria leve)
- **Spec:** `TASKS.md` §Fase 20 + revisão F19/F20/F21 (regras 8 e 9)
- **Arquitetura:** `docs/adr/ADR-028-INTEGRATION-LAYER-ARCHITECTURE.md` (§2 cliente, §4 sync, §5 endpoints, §6 resiliência, §7 identidade)
- **Status:** concluída em 03/10/2026 (decisões do usuário no mesmo dia)

## 1. Escopo

Camada de **saída** para o UniFi Controller (UDM/USG/UXG + switches + APs):

| Entrega | Arquivo |
| --- | --- |
| Cliente HTTP (login + sessão + listagem + health) | `src/InfraControl.Infrastructure/Integrations/Unifi/UnifiApiClient.cs` |
| Porta + DTO + options | `src/InfraControl.Application/Integrations/Unifi/{Ports/IUnifiApiClient, UnifiDeviceDto, UnifiIntegrationOptions}.cs` |
| Sync idempotente de inventário | `src/InfraControl.Application/Integrations/Unifi/SyncUnifiInventoryCommand.cs` |
| Endpoints HTTP | `src/InfraControl.Api/Controllers/Integrations/UnifiIntegrationController.cs` |
| Testes | `tests/InfraControl.UnitTests/Integrations/{UnifiApiClientTests, SyncUnifiInventoryCommandTests, UnifiIntegrationControllerTests}.cs` (17) |

**REGRA 8 (regra de ouro desta fase):** o sync persiste **apenas** `Device` e
`ExternalDeviceReference`. Telemetria de massa (portas PoE, tráfego, clientes
Wi-Fi) **nunca** entra no PostgreSQL — consulta sob demanda é F20-T003
(`UniFi API → Redis → Frontend`).

## 2. Contrato HTTP

### `POST /api/v1/integrations/unifi/sync`

- **Auth:** usuário autenticado + nível **Operate** (roles declaradas no
  command via `IAuthorizedRequest`/`RoleAccess.Operate` — nenhum literal de
  role no controller, regra do arch test); CSRF pelo middleware global
  `AntiforgeryCookieMiddleware`.
- **Body:** vazio.
- **200 (envelope):**

```json
{ "isSuccess": true, "data": { "created": 3, "updated": 1, "references": 3, "errors": 0 } }
```

- `created`/`updated` = dispositivos; `references` = referências externas
  **novas** (referência existente do mesmo dono não conta); `errors` =
  dispositivos pulados (MAC inválido, divergência de dono… — um erro não
  derruba o restante do sync).

**Falhas** (saem pelo `GlobalExceptionHandler`):

| Situação | Exceção | Status |
| --- | --- | --- |
| Integração não configurada (`UNIFI_*` vazios) | `InvalidOperationException` | 409 (mensagem pt-BR) |
| Credencial/sessão recusada pelo Controller | `UnauthorizedAccessException` | 403 |
| Controller fora do ar / rede | `HttpRequestException` | 500 |

### `GET /api/v1/integrations/unifi/health`

Sondagem leve em tempo real (`GET /api/self` no Controller), **sem
persistência**:

- **200** `{"isSuccess":true,"data":{"status":"up"}}`
- **503** com motivo no envelope quando o Controller está indisponível ou a
  integração está desativada.

> Decisão do usuário (03/10/2026): o **agregado** de integração persistido
> (tabela + painel, ADR-028 §4) fica para task própria depois — o health desta
> task é só sondagem ao vivo.

## 3. Autenticação com o Controller

**Fase 1 (F26-T001, 2026-10-08): detecção automática do estilo do
controlador** — auditado em `docs/35-AUDITORIA-UNIFI-FASE0.md` §3:

1. Na **primeira** autenticação o cliente tenta `POST {BaseUrl}/api/auth/login`
   (estilo **UniFi OS** — UDM/Cloud Key). Sucesso → estilo `UniFiOs`.
2. Se rejeitado (**401/403** — standalone responde 401 até com credenciais
   válidas — ou **404/405** — endpoint inexistente), tenta
   `POST {BaseUrl}/api/login` (estilo **standalone** UniFi Network
   Application). Sucesso → estilo `Standalone`.
3. Payload `{"username","password"}` nos dois → `Set-Cookie: unifises=…`
   (a única sessão usada). Estilo ficou memorizado no cliente (log
   `Information` "identificado como {Style}").
4. Se **qualquer** tentativa devolve 401/403 → `UnauthorizedAccessException`
   ("Credenciais do UniFi Controller inváladas ou sessão recusada."). Se os
   dois endpoints não existem (404/405) → `InvalidOperationException`
   ("…não expôs API de login compatível…").
5. Todas as chamadas seguem com header `Cookie: unifises=…`.
6. Se o Controller responder **401** numa chamada, o cliente re-autentica
   **uma vez** (pelo endpoint do estilo detectado) e repete; ainda 401 →
   `UnauthorizedAccessException`.

Endpoints consumidos (prefixo pela API detectada):

| Uso | UniFi OS (`UniFiOs`) | Standalone (`Standalone`) |
| --- | --- | --- |
| Login | `POST /api/auth/login` | `POST /api/login` |
| Dispositivos (sync) | `GET /proxy/network/api/s/{site}/stat/device` | `GET /api/s/{site}/stat/device` |
| Health | `GET /api/self` (raiz, nos dois) | idem |

## 4. Mapeamento UniFi → cadastro local

| Campo UniFi | Destino |
| --- | --- |
| `mac` (lookup + `Device.Mac`) | `Device.Mac` (formato `aa:bb:…` normalizado pelo domínio) |
| `mac` (**identidade externa**) | `ExternalDeviceReference.ExternalId` = **lowercase SEM separadores** (`aabbccddee01`) — decisão ADR-028 §7 |
| `type` `udm`/`ugw`/`usg`/`uxg`/`usgw` | `DeviceType.Firewall` — **decisão do usuário 03/10/2026** (gateway UniFi = firewall) |
| `type` `usw` | `DeviceType.Switch` |
| `type` `uap` | `DeviceType.AccessPoint` |
| outro `type` | `DeviceType.Other` |
| `name` (fallback: `model` → `mac`) | `Device.Name` |
| `ip`, `model` | `Device.Ip`, `Device.Model` (preservam serial/vendor/vlan/descrição/rack locais) |
| — | `Location` **"UniFi"**, criada automaticamente se não existir — **decisão do usuário 03/10/2026** |

**Idempotência:** o device é localizado por MAC (`GetByMacAsync`, case e
separadores ignorados); a referência por `(Source=UniFi, ExternalId)`.
Reexecutar o sync não duplica nada. **Divergência de dono** (referência já
apontando para outro device) **não é realocada automaticamente** — conta como
`errors` e fica visível para o operador.

## 5. Configuração

| Env (compose → `Integrations:Unifi`) | Default | Efeito |
| --- | --- | --- |
| `UNIFI_BASE_URL` | vazio | **vazio = integração desligada** (nada é chamado) |
| `UNIFI_USERNAME` / `UNIFI_PASSWORD` | vazio | credencial do Controller — só via `.env`, nunca no repositório |
| `UNIFI_SITE` | `default` | site do Controller no caminho da API |
| `UNIFI_ALLOW_INVALID_TLS` | `false` | **Fase 1:** `true` aceita certificado TLS self-signed (controlador local — docs/35 §2); handler `UnifiHttpMessageHandler` só instala o bypass quando ligado; manter `false` em produção |

Registrado no `Program.cs` (`AddOptions<UnifiIntegrationOptions>().Bind`) e
no `docker-compose.yml` (`.env.example` §F20-T001).

## 6. Resiliência (Polly)

O named client **`unifi`** (`UnifiApiClient.HttpClientName`) usa a **mesma**
política do dispatcher n8n (`HttpWebhookNotificationDispatcher.ConfigureResilience`):
retry 3× backoff exponencial 1s/2s/4s com jitter + circuit breaker
(5 falhas/30s → aberto 60s). Login/sessão ficam no próprio client (a política
não substitui a re-autenticação em 401).

## 7. Limitações conhecidas / pendências

- **Validação real (Fase 1/11):** controlador local UniFi Network **10.4.57**
  auditado em `docs/35` com consultas `GET` autenticadas — a validação
  ponta-a-ponta do *sync* contra esse controlador (com `UNIFI_*` + TLS) é da
  Fase 11; os testes unitários seguem com `HttpMessageHandler` fake
  (`ScriptedHandler`) cobrindo os dois estilos de login, cookie, re-auth 401,
  parse, health e o handler TLS.
- Agregado persistido de integração (tabela + painel) = Fase 2 do plano
  `docs/34`; telemetria sob demanda = F20-T003; webhooks de estado =
  F20-T002.
- `AlertCreated` (notificação de alerta novo) segue como pendência aberta da
  revisão F19/F20/F21 (TASKS §notificações).
