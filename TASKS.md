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
- [x] F0-T005 Configurar PostgreSQL
- [x] F0-T006 Configurar Redis
- [x] F0-T007 Configurar RabbitMQ
- [x] F0-T008 Configurar migrations
- [x] F0-T009 Configurar observabilidade base
- [ ] F0-T010 Configurar CI (BLOCKED: conta atingiu 2.000/2.000 minutos do GitHub Actions; reset em 01/10/2026)
- [x] F0-T011 Criar estrutura Frontend e dashboard inicial
- [x] F0-T012 Documentar capacidades extraídas do Sentinel e critérios de adoção
- [x] F0-T013 Implementar behaviors base, idempotência, Outbox Dispatcher e health endpoints
- [x] F0-T014 Auditoria persistida pós-handler/commit, sem payloads sensíveis
- [x] F0-T015 Inbox e deduplicação de consumidores
- [x] F0-T016 Resiliência por provider (retry, timeout e circuit breaker)
- [x] F0-T017 Retenção e cleanup operacional
- [x] F0-T018 Cache com invalidação opt-in no pipeline
- [x] F0-T019 Feature flags com configuração e override temporário
- [x] F0-T020 Registry de providers extensíveis para Discovery e Monitoring
- [x] F0-T021 Criar BuildingBlocks Shared Kernel alinhado ao Sentinel
- [ ] F0-T022 Migrar slices CQRS para ICommand/IQuery/IAuthorizedRequest quando cada módulo for implementado

## Fase 1 — Identity

- [x] F1-T001 ASP.NET Core Identity (DDD, agregados, eventos, roles, sessões e migration)
- [x] F1-T002 Bootstrap Admin (opt-in, idempotente, transacional e com segredo externo)
- [x] F1-T003 Roles (Vertical Slice/CQRS, policies, sincronização Identity e proteção do último Admin)
- [x] F1-T003A Alinhar Infrastructure ao Sentinel (Auth, Caching, Persistence, Repositories e Services)
- [x] F1-T004 Permissions (catálogo no domínio, matriz por role, claims e policies granulares)
- [x] F1-T008 Password Change (endpoint autenticado, revogação de sessões e auditoria)
- [x] F1-T009 Password Reset (token single-use Redis, expiração e resposta anti-enumeração)
- [x] F1-T011 Auth API e dashboard de usuários (registro, login, refresh, logout, `me`, status e roles)
- [x] F1-T012 Estrutura MVC modular da API alinhada ao Dashboard Sentinel
- [x] F1-T005 Invitation
- [x] F1-T006 Accept Invitation
- [x] F1-T007 Reject Invitation
- [x] F1-T010 Audit

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
