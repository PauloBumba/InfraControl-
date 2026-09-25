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

## Ordem alinhada com o problema real (2026-09-23)

Problema do blueprint: gestão heterogênea on-premises com visibilidade centralizada.
Caso de uso do usuário: ver cada porta/switch/impressora/AP; toner e cilindro da impressora; AP on/off + internet; ping tipo Zabbix com alerta; acesso remoto a switch (SSH), servidor (SSH) e computador (RDP).

Ordem de execução recomendada (uma Task IN_PROGRESS por vez):

1. `F2-T003` Device (tipos: Switch, Printer, AccessPoint, Camera, Server, Workstation, …)
2. `F2-T008` DeviceProfile — perfil de checks/thresholds padrão por DeviceType (ADR antes)
3. `F5-T003` Ping — up/down de todos (estilo Zabbix)
4. `F5-T004` SNMP Printer — toner, drum/cilindro (Printer MIB) e alertas de nível
5. `F5-T007` Worker de polling contínuo
6. `F6-T001`/`F6-T002` Alert + regras (down, toner/drum baixo, recovery)
7. `F3` Switch/Port/VLAN (liga “o que está em cada porta”)
8. `F11` Remote Access — SSH (switch/servidor), RDP (computador), sem expor credenciais
9. `F4` Discovery (LLDP/ARP/MAC) e `F9` Notifications só depois do loop acima verde

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
- [x] F0-T013 Implementar behaviors base, autorização, cache, transação, rate limiting, Outbox Dispatcher e health endpoints
- [x] F0-T014 Auditoria persistida pós-handler/commit, sem payloads sensíveis
- [x] F0-T015 Inbox e deduplicação de consumidores
- [x] F0-T016 Resiliência por provider (retry, timeout e circuit breaker)
- [x] F0-T017 Retenção e cleanup operacional
- [x] F0-T018 Cache com invalidação opt-in no pipeline
- [x] F0-T019 Feature flags com configuração e override temporário
- [x] F0-T020 Registry de providers extensíveis para Discovery e Monitoring
- [x] F0-T021 Criar BuildingBlocks Shared Kernel alinhado ao Sentinel
- [x] F0-T022 Migrar slices CQRS para ICommand/IQuery/IAuthorizedRequest quando cada módulo for implementado
- [x] F0-T023 Tratamento global de exceções → envelope ApiResponse com mensagens em pt-BR

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
- [x] F1-T013 Guard de ativação do usuário (registro inativo, aceite provisiona conta inativa, `set-password` ativa, login/reset exigem ativo)

## Fase 2 — Inventário

- [x] F2-T001 Location
- [x] F2-T002 Rack
- [x] F2-T003 Device  <!-- tipos Server/Workstation/Switch/Router/Firewall/AccessPoint/Printer/Camera/Phone/UPS/Other; campos ip/mac/serial/model/vendor/vlanId/locationId/rackId; nome único global case-insensitive; DeleteRack com devices → 409; FK Restrict location/rack; migração AddDevices + índice ux_devices_name; seed fictício docs/database/seed-fictitious-devices.sql (RFC 5737, sem dados reais); policies InventoryRead/InventoryManage; BaseCommand/IQuery/IAuthorizedRequest -->
- [x] F2-T004 NetworkInterface
- [ ] F2-T005 MAC
- [ ] F2-T006 IP  <!-- dependência F2-T004: bloquear exclusão de Interface que possua IPs (409) -->
- [ ] F2-T007 Busca global
- [x] F2-T008 DeviceProfile  <!-- ADR-014 Accepted; DeviceProfileDefaults por tipo em código (Printer→Ping+SnmpToner+SnmpDrum, AP→Ping+HttpWan, Switch/Server/Router/Firewall/UPS→Ping+SnmpSystem, demais→Ping); materializado no Device.Create com ProfileIsCustom=false; ApplyCustomProfile/ResetProfileToDefault (vazio/duplicado → 409/400 pt-BR); Update preserva override na troca de tipo; UpdateDeviceProfileCommand (checks XOR useDefault); PUT /devices/{id}/profile; colunas profile_is_custom + monitoring_checks (CSV) + migração AddDeviceProfile; EF ValueConverter List<CheckKind>→string; tests DeviceProfileTests + DeviceProfileCommandTests; 6 slices na regra de arquitetura -->

## Fase 3 — Network

- [ ] F3-T001 Switch  <!-- extensão de Device tipo Switch -->
- [ ] F3-T002 SwitchPort  <!-- portas físicas + link up/down + MAC aprendido depois (F4) -->
- [ ] F3-T003 VLAN  <!-- ex.: Default, Radiologia, Telefonia, Impressoras, Cameras, Unifi -->
- [ ] F3-T004 SwitchPortVlan  <!-- membros tagged/untagged -->
- [ ] F3-T005 Connection
- [ ] F3-T006 Topology  <!-- prioridade baixa para o problema atual -->

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

- [x] F5-T001 MonitoringCheck  <!-- instância por device; nasce do DeviceProfile (F2-T008/ADR-014 §4); entity MonitoringCheck (DeviceId, Kind string, Enabled) com índice único (device_id, kind) e FK cascade (delete do device remove rows); materialização em CreateDeviceCommandHandler e sincronização em UpdateDeviceProfileCommandHandler via IMonitoringCheckRepository.SynchronizeFromProfileAsync (adiciona faltantes Enabled=true, remove fora do perfil, preserva existentes — mesmo SaveChanges); GetDeviceChecksQuery (MonitoringRead, IQuery+IAuthorizedRequest, 404 pt-BR) em GET /monitoring/checks/{id}; ordenação por Kind em memória (coluna string ordenaria alfabeticamente); migração AddMonitoringChecks -->
- [x] F5-T002 MonitoringResult  <!-- histórico de polls; entity MonitoringResult (DeviceId FK cascade p/ Device, Kind, Succeeded, DurationMs, Error≤500, Values JSON opcional, CheckedAt=CreateDateTime) — FK no device e não no MonitoringCheck de propósito: histórico sobrevive a mudanças de perfil; escrita on-demand no RunCheckCommandHandler (docs/21 MON-006/007 — 1 linha por execução em device com IP; sem IP não grava; Values p/ checks SNMP); GetDeviceResultsQuery (MonitoringRead, limit 1..500 default 50, ordem CheckedAt desc, 404 pt-BR) em GET /monitoring/results/{id}; migração AddMonitoringResults com índice (device_id, CreateDateTime); SaveChangesAsync sobrescreve CreateDateTime (auditoria) — seeds testam via SQL -->
- [x] F5-T003 Ping  <!-- up/down sob demanda estilo Zabbix; PingMonitoringProvider (chave "Ping", timeout 2s) com seam IIcmpClient/SystemNetIcmpClient; execução via RunCheckCommand/RunAllChecksCommand (docs/21 MON-006/007, ADR-006) em POST /monitoring/run-check/{id}?check=Ping + POST /monitoring/run-check?check=Ping; device sem IP → Executed=false "Device sem IP cadastrado."; queries GET de execução removidas (GET fica só p/ leitura: checks/results); DI IIcmpClient+IMonitoringProvider; testes em RunCheckCommandTests/RunAllChecksCommandTests (provider falso/ICMP falso + Sqlite); histórico via F5-T002; sem worker (F5-T007) -->
- [x] F5-T004 SNMP  <!-- Printer MIB: toner e drum/cilindro % sob demanda; SnmpTonerProvider/SnmpDrumProvider (keys CheckKind.SnmpToner/Drum) com base PrinterSuppliesProvider — walk prtMarkerSuppliesTable OID 1.3.6.1.2.1.43.11.1.1 (colunas 6=desc, 8=max, 9=level), match por descrição (toner/ink/tinta; drum/cilindro/imaging), pior carretel em % via Values (levelPercent/description); seam ISnmpClient + SharpSnmpSnmpClient (Lextm.SharpSnmpLib 12.5.7, SNMPv2c, community configurável Monitoring:Snmp default public); MonitoringProviderResult ganhou Values opcional (retrocompatível); execução via RunCheckCommand (POST /monitoring/run-check/{id}?check=SnmpToner|SnmpDrum; check SNMP em non-Printer → 400 "só se aplica a devices do tipo Printer"; GET /monitoring/printers/{id}/supplies removido — histórico via GET /results com Values); credencial por device → F11 -->
- [ ] F5-T005 HTTP/HTTPS
- [x] MON-009 IntegrationTest  <!-- docs/21: testes de integração de monitoring em tests/InfraControl.IntegrationTests/Monitoring/; BaseIntegrationTest com PostgreSQL real via Testcontainers (imagem local postgres:17-alpine, sem pull), pipeline MediatR do aplicativo (CorrelationId/Logging/Performance/Validation/Audit/Transaction/Authorization behaviors) e repositórios Infrastructure reais; permissões vêm de RolePermissions (role → Permission → claim string) via TestCurrentUserContext; ICMP falso (sem rede); 4 testes: run-check com role Viewer persiste e lê de volta via GetDeviceResultsQuery, sem MonitoringRead → UnauthorizedAccessException pt-BR, sem IP não persiste, RunAllChecks só grava devices com IP -->
- [ ] F5-T006 TCP  <!-- opcional: câmera porta 37777 -->
- [ ] F5-T007 Worker  <!-- polling periódico; depende F5-T001/T002 -->
- [ ] F5-T008 Recovery  <!-- volta a responder → fecha alerta -->

## Fase 6 — Alerts/Incidents

- [ ] F6-T001 Alert  <!-- prioridade: device down, toner < 20%, drum < 10% (thresholds do perfil) -->
- [ ] F6-T002 Alert Rules  <!-- consecutive failures, dependência de upstream, anti alert-storm -->
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

- [x] F9-T001 Notification  <!-- docs/07 ADR-012: entity Notification (EventId único p/ idempotência visual, EventType, Title≤200, Message≤1000, audit fields) + NotificationConfiguration (índice único event_id) + migração AddNotifications; fluxo completo docs/06: OutboxDispatchHandler<TEvent> (handler MediatR aberto — todo domain event do AppDbContext vira OutboxMessage na mesma transação, ADR-005) → OutboxDispatcherWorker (já existia) → RabbitMQ → NotificationEventConsumer (UserCreated/UserDisabled/UserRoleAssigned/UserRoleRemoved/InvitationAccepted; idempotência via IInboxStore EventId+Consumer "NotificationConsumer"; logs Information; consumidor registrado no MassTransit do Workers); payload outbox com enums string (OutboxJson); destinatários F9-T002, canais F9-T004+ -->
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

- [x] F11-T001 RemoteSession  <!-- entity RemoteSession (Open/Close/Fail com invariantes pt-BR, FailureReason≤500) + Permission.RemoteSessionsRead (= docs/10 device.remote.read; Admin herda automaticamente, User/Viewer não — matriz de connect/use em F11-T004) + GetDeviceSessionsQuery (IQuery+IAuthorizedRequest, ordem StartedAt desc, limit 1–500, 404 pt-BR) + GET /api/v1/inventory/devices/{id}/remote/sessions ([Authorize(Roles="Admin")] conservador até F11-T004) + DTO RemoteSessionDto sem campos de credencial (teste de reflexão, docs/10) + tabela remote_sessions (FK device cascade, índices (device_id, started_at)) + migração AddRemoteSessions; abrir/conectar sessão real só com provider SSH/RDP em F11-T002/T003; AuditEntry explícito em F11-T005; 16 unit + 2 integration -->
- [x] F11-T002 SSH Provider  <!-- ports IRemoteProvider/IRemoteCredentialSource/IRemoteConnectionRegistry + Permission.RemoteSshConnect (= device.ssh.connect; Admin herda, User/Viewer não — matriz completa em F11-T004) + OpenSshSessionCommand/CloseSshSessionCommand (BaseCommand+IAuthorizedRequest, sem restrição de tipo — decisão do usuário; device sem IP → ArgumentException 400, credencial ausente → InvalidOperationException 409 sem persistir sessão, falha de conexão → sessão Failed persistida com FailureReason≤500 (senha mascarada) + 409, fechar sessão de outro device → 404, já finalizada → 409) + SshRemoteProvider (SSH.NET 2026.0.0 — 2025.1.0 tinha advisories NU1903; Connect() síncrono via Task.Run, ConnectionInfo.Timeout, erro vira RemoteConnectResult pt-BR sem exceção vazando) + ConfigurationRemoteCredentialSource (seção RemoteAccess:Ssh provisória, vazia no git — F11-T006 Credential Store) + RemoteConnectionRegistry (singleton em memória, dispose no close) + POST/DELETE /api/v1/inventory/devices/{id}/remote/sessions (201/200, [Authorize(Roles="Admin")]) + 22 unit + 5 integration + 2 commands na regra BuildingBlocks; provider é falso nos integration tests (nenhuma rede) -->
- [x] F11-T003 RDP Provider  <!-- probe TCP/X.224, decisão do usuário (sem autenticação — NLA/CredSSP fica para task futura; computador Windows/Workstation sem restrição de tipo): Permission.RemoteRdpConnect (= device.rdp.connect; Admin herda, User/Viewer não — matriz em F11-T004) + OpenRdpSessionCommand (BaseCommand+IAuthorizedRequest; SEM IRemoteCredentialSource — probe não usa credencial; device sem IP → ArgumentException 400, probe falho → sessão Failed persistida com FailureReason≤500 + 409 "Falha ao abrir sessão RDP em '{name}'") + RENOMEIO CloseSshSessionCommand → CloseRemoteSessionCommand (lógica era protocolo-agnóstica e há um único DELETE; RequiredPermissions = RemoteSshConnect+RemoteRdpConnect AND, Admin-only hoje — matriz real em F11-T004; refatora T002: helpers internos viram RemoteSessionFlow) + RdpRemoteProvider (TcpClient + X.224 CR com RDP Negotiation Request SSL|HYBRID sem cookie; parse do CC: Neg Response → sucesso mantendo socket como conexão da sessão, Neg Failure → erro pt-BR mapeado (SSL/NLA/etc), TPKT/lixo → "Endpoint não respondeu como RDP", porta fechada/timeout → RemoteConnectResult falho sem exceção vazando, ct cancelado → rethrow; RemoteRdpOptions RemoteAccess:Rdp Port=3389/TimeoutSeconds=10 sem segredo) + DI Configure+AddScoped + seção appsettings + POST .../remote/sessions/rdp (201, [Authorize(Roles="Admin")]) + 3 commands na regra BuildingBlocks (2 no lugar do antigo — rename contado) + 20 unit + 5 integration (RdpRemoteProviderTests com FakeRdpServer TcpListener loopback: CC válido → sucesso, Neg Failure NLA → pt-BR, resposta não-RDP → falha, porta fechada → falha, silêncio → timeout 1s, ct cancelado → throw; RdpSessionCommandTests ciclo Open→Close com fakes; PermissionTests User/Viewer NotContain; RdpSessionIntegrationTests Admin abre→fecha, Viewer negado "*RemoteRdpConnect*") — provider é falso nos integration tests (nenhuma rede) -->
- [ ] F11-T004 Permissions  <!-- device.remote.read, device.ssh.connect, device.rdp.connect, credential.use/manage -->
- [ ] F11-T005 Audit  <!-- integra com audit existente (F1-T010) -->
- [ ] F11-T006 Credential Store  <!-- secrets fora do git; env/secret manager; rotation -->

## Fase 12 — Operação

- [ ] F12-T001 Cleanup
- [ ] F12-T002 Retention
- [ ] F12-T003 Backup
- [ ] F12-T004 Restore Test
- [ ] F12-T005 Performance

## Fase 14 — Frontend: completar áreas da plataforma

Prototipação das páginas que faltam conforme `docs/14-UI-UX.md`, com mock data em `src/data/`, rota, item de sidebar e componentes PrimeReact. Backend alinha depois.

- [x] F14-T001 Users & Roles (prioridade: backend F1 já existe)
- [x] F14-T002 Audit
- [x] F14-T003 Incidents
- [x] F14-T004 Notifications
- [x] F14-T005 Locations & Racks
- [x] F14-T006 SLA/SLO & Services
- [x] F14-T007 Switches/Ports como áreas próprias (hoje embutidas em Network/Devices)
- [x] F14 polish UI (Health no sidebar, chunks vendor/redux no Vite, accent unificado)
