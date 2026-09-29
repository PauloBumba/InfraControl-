# API e Contratos

## API

REST como interface principal.

SignalR para eventos e atualizações em tempo real.

## Envelope de resposta

Toda resposta HTTP com corpo usa o envelope `ApiResponse<T>` do BuildingBlocks (`docs/24-BUILDING-BLOCKS.md`):

```json
{ "isSuccess": true, "data": { }, "errors": [] }
{ "isSuccess": false, "data": null, "errors": ["mensagem"] }
```

Regras:

- sucesso com conteúdo → `200`/`201` + envelope;
- sucesso sem conteúdo → `204` sem corpo (HTTP não permite corpo em 204);
- erro → status `4xx`/`5xx` + envelope com `errors` preenchido (nunca `{ "error": "..." }`);
- mensagem de sucesso relevante para a UX → `data` no envelope de sucesso.

No frontend o shape correspondente é `ApiResponse<T>` em `frontend/InfraControl.Web/src/types/api.ts`.

### Linguagem das mensagens (F0-T023)

- Toda mensagem de erro visível ao usuário nasce em **pt-BR** no handler/serviço/domínio (regra da camada de aplicação).
- O transporte (controller/global handler) só **embrulha** — não traduz nem reescreve.
- O front repassa `errors[0]` diretamente para a UX, sem camada de tradução.

### Tratamento global de exceções (F0-T023)

`GlobalExceptionHandler` (`src/InfraControl.Api/Middleware/GlobalExceptionHandler.cs`) é o ponto único de erro:
controllers não fazem `try/catch` de falhas de negócio — só compõem sucesso.

| Exceção (nascida no handler/serviço/domínio) | Status | Origem típica |
|---|---|---|
| `RequestValidationException` | `400` | validação de regra (erros em `errors[]`) |
| `ArgumentException` | `400` | pré-condições (sufixo `(Parameter 'x')` removido) |
| `KeyNotFoundException` | `404` | recurso inexistente ("Convite '…' não encontrado.") |
| `UnauthorizedAccessException` | `401` anônimo / `403` autenticado | token/convite inválido, role insuficiente (`Roles ausentes: …`) |
| `DuplicateRequestException` | `409` | idempotência |
| `InvalidOperationException` | `409` | conflito de estado ("último admin", convite já aceito) |
| demais | `500` | mensagem genérica em pt-BR; detalhe só no log (TraceId) |

Notas:

- `RequestValidationException` e `DuplicateRequestException` herdam de `InvalidOperationException` e são casadas **antes** dele.
- Erros de ModelState (binding do ASP.NET) também usam o envelope via `InvalidModelStateResponseFactory` — a API nunca devolve ProblemDetails.
- `500` nunca vaza detalhe interno (stack, connection string, IP).

## Commands

Exemplos:

- RegisterDevice
- UpdateDevice
- MoveDevice
- CreateAlert
- AcknowledgeIncident
- CreateTask
- CreateInvitation

## Queries

Exemplos:

- GetDevice
- SearchDevices
- GetTopology
- GetAlerts
- GetDeviceMetrics
- GetTasks

## Eventos

- DeviceDiscovered
- DeviceStatusChanged
- PortStatusChanged
- MonitoringCheckFailed
- MonitoringCheckRecovered
- AlertCreated
- AlertRecovered
- IncidentCreated
- InvitationAccepted
- InvitationRejected
- UserDisabled
- TaskCreated
- NotificationCreated

Eventos usam Envelope Pattern.
