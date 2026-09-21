# TASKS

## Como usar

Cada Task deve ser pequena, verificável e ter dependências explícitas.

### Estados

- BACKLOG
- READY
- PLANNING
- IN_PROGRESS
- BLOCKED
- TESTING
- REVIEW
- VALIDATION
- DONE
- CANCELLED

## Definition of Done

```text
[ ] Objetivo atendido
[ ] Escopo respeitado
[ ] Dependências resolvidas
[ ] Testes necessários criados
[ ] Testes passando
[ ] Build passando
[ ] Segurança verificada
[ ] Observabilidade verificada
[ ] Documentação atualizada
[ ] Review concluído
[ ] Evidências registradas
[ ] PROJECT_STATE atualizado
[ ] Commit realizado
```

## Fase 0 — Fundação

- [x] F0-T001 Criar solução .NET
- [x] F0-T002 Criar estrutura de projetos
- [x] F0-T003 Criar solução de testes
- [x] F0-T004 Configurar Docker Compose
- [ ] F0-T005 Configurar PostgreSQL
- [ ] F0-T006 Configurar Redis
- [ ] F0-T007 Configurar RabbitMQ
- [ ] F0-T008 Configurar migrations
- [ ] F0-T009 Configurar observabilidade base
- [ ] F0-T010 Configurar CI

## Fase 1 — Identity

- [ ] F1-T001 ASP.NET Core Identity
- [ ] F1-T002 Bootstrap Admin
- [ ] F1-T003 Roles
- [ ] F1-T004 Permissions
- [ ] F1-T005 Invitation
- [ ] F1-T006 Accept Invitation
- [ ] F1-T007 Reject Invitation
- [ ] F1-T008 Password Change
- [ ] F1-T009 Password Reset
- [ ] F1-T010 Audit

## Fase 2 — Inventário

- [ ] F2-T001 Location
- [ ] F2-T002 Rack
- [ ] F2-T003 Device
- [ ] F2-T004 NetworkInterface
- [ ] F2-T005 MAC
- [ ] F2-T006 IP
- [ ] F2-T007 Busca global

## Fase 3 — Network

- [ ] F3-T001 Switch
- [ ] F3-T002 SwitchPort
- [ ] F3-T003 VLAN
- [ ] F3-T004 SwitchPortVlan
- [ ] F3-T005 Connection
- [ ] F3-T006 Topology

## Fase 4 — Discovery

- [ ] F4-T001 DiscoveryRun
- [ ] F4-T002 DiscoveryResult
- [ ] F4-T003 ICMP Provider
- [ ] F4-T004 SNMP Provider
- [ ] F4-T005 LLDP
- [ ] F4-T006 CDP
- [ ] F4-T007 ARP
- [ ] F4-T008 DHCP
- [ ] F4-T009 MAC Tables
- [ ] F4-T010 New Device Detection

## Fase 5 — Monitoring

- [ ] F5-T001 MonitoringCheck
- [ ] F5-T002 MonitoringResult
- [ ] F5-T003 Ping
- [ ] F5-T004 SNMP
- [ ] F5-T005 HTTP/HTTPS
- [ ] F5-T006 TCP
- [ ] F5-T007 Worker
- [ ] F5-T008 Recovery

## Fase 6 — Alerts/Incidents

- [ ] F6-T001 Alert
- [ ] F6-T002 Alert Rules
- [ ] F6-T003 Incident
- [ ] F6-T004 Correlation
- [ ] F6-T005 Root Cause
- [ ] F6-T006 Recovery

## Fase 7 — Tasks

- [ ] F7-T001 Task
- [ ] F7-T002 TaskAssignee
- [ ] F7-T003 TaskDevice
- [ ] F7-T004 TaskIncident
- [ ] F7-T005 TaskComment
- [ ] F7-T006 TaskHistory
- [ ] F7-T007 Task Workflow

## Fase 8 — SLA/SLO

- [ ] F8-T001 Service
- [ ] F8-T002 SlaPolicy
- [ ] F8-T003 TaskSla
- [ ] F8-T004 TaskSlaPause
- [ ] F8-T005 Slo
- [ ] F8-T006 SloMeasurement
- [ ] F8-T007 SloBudget

## Fase 9 — Notifications

- [ ] F9-T001 Notification
- [ ] F9-T002 Recipient
- [ ] F9-T003 Preferences
- [ ] F9-T004 SignalR
- [ ] F9-T005 Email
- [ ] F9-T006 Telegram
- [ ] F9-T007 WhatsApp
- [ ] F9-T008 Webhook
- [ ] F9-T009 Retry/DLQ

## Fase 10 — Knowledge

- [ ] F10-T001 Category
- [ ] F10-T002 Article
- [ ] F10-T003 Publishing
- [ ] F10-T004 Relationships

## Fase 11 — Remote Access

- [ ] F11-T001 RemoteSession
- [ ] F11-T002 SSH Provider
- [ ] F11-T003 RDP Provider
- [ ] F11-T004 Permissions
- [ ] F11-T005 Audit

## Fase 12 — Operação

- [ ] F12-T001 Cleanup
- [ ] F12-T002 Retention
- [ ] F12-T003 Backup
- [ ] F12-T004 Restore Test
- [ ] F12-T005 Performance
