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

1. `POST {BaseUrl}/api/auth/login` com `{"username","password"}` →
   `Set-Cookie: unifises=…` (a única sessão usada).
2. Todas as chamadas seguem com header `Cookie: unifises=…`.
3. Se o Controller responder **401**, o cliente re-autentica **uma vez** e
   repete a chamada; ainda 401 → `UnauthorizedAccessException`.
4. Credencial recusada no login (401/403) → `UnauthorizedAccessException`
   ("Credenciais do UniFi Controller inváladas ou sessão recusada.").

Endpoints consumidos:

| Uso | Endpoint |
| --- | --- |
| Login | `POST /api/auth/login` |
| Dispositivos (sync) | `GET /proxy/network/api/s/{site}/stat/device` → `{meta:{rc},data:[…]}` |
| Health | `GET /api/self` |

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
| `UNIFI_SITE` | `default` | site do Controller no caminho do proxy |

Registrado no `Program.cs` (`AddOptions<UnifiIntegrationOptions>().Bind`) e
no `docker-compose.yml` (`.env.example` §F20-T001).

## 6. Resiliência (Polly)

O named client **`unifi`** (`UnifiApiClient.HttpClientName`) usa a **mesma**
política do dispatcher n8n (`HttpWebhookNotificationDispatcher.ConfigureResilience`):
retry 3× backoff exponencial 1s/2s/4s com jitter + circuit breaker
(5 falhas/30s → aberto 60s). Login/sessão ficam no próprio client (a política
não substitui a re-autenticação em 401).

## 7. Limitações conhecidas / pendências

- **Sem UniFi Controller real em dev:** os testes usam `HttpMessageHandler`
  fake (`ScriptedHandler`) cobrindo login, cookie, re-auth 401, parse e
  health — a validação ponta-a-ponta com um Controller real depende de
  ambiente do usuário.
- Agregado persistido de integração (tabela + painel) = task própria
  (decisão 03/10/2026); telemetria sob demanda = F20-T003; webhooks de
  estado = F20-T002.
- `AlertCreated` (notificação de alerta novo) segue como pendência aberta da
  revisão F19/F20/F21 (TASKS §notificações).
