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
- erro → status `4xx`/`5xx` + envelope com `errors` preenchido (nunca `{ "error": "..." }`).

No frontend o shape correspondente é `ApiResponse<T>` em `frontend/InfraControl.Web/src/types/api.ts`.

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
