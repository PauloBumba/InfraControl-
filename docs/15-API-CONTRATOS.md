# API e Contratos

## API

REST como interface principal.

SignalR para eventos e atualizações em tempo real.

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
