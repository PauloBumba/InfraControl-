# PROJECT_STATE

## Estado

**Fase:** Fundação técnica e execução incremental
**Status:** Em execução incremental
**Última atualização:** 2026-09-24

## Regra operacional

Uma única Task principal deve estar em `IN_PROGRESS` por vez, salvo autorização explícita para trabalho paralelo.

## Estado atual

- [x] Visão do produto definida
- [x] Arquitetura principal definida
- [x] PostgreSQL definido
- [x] Redis definido
- [x] RabbitMQ + Outbox definidos
- [x] CQRS definido
- [x] Vertical Slice definido
- [x] MediatR Behaviors definidos
- [x] OpenTelemetry definido
- [x] Identity/RBAC definido
- [x] Tasks/SLA/SLO definidos
- [x] Notifications definidas
- [x] Remote Access definido
- [x] Metodologia Anti-Vibe Coding definida
- [x] Divisão em Tasks/SubTasks definida
- [x] Solution criada
- [x] Estrutura de projetos definida
- [x] Solução de testes criada
- [x] Docker Compose configurado
- [x] PostgreSQL configurado
- [x] Redis configurado
- [x] RabbitMQ configurado (MassTransit)
- [x] Migrations configuradas
- [x] Observabilidade base (Serilog, Health Checks, OpenTelemetry)
- [x] Frontend estrutura (React + TypeScript + Vite + PrimeReact)
- [x] Primeiro projeto compilando
- [x] Primeiros testes
- [x] Primeiro módulo implementado (dashboard web inicial)
- [x] Fundação ASP.NET Core Identity implementada com domínio, eventos, roles e sessões
- [x] Bootstrap Admin implementado como caso de uso em Application com adaptador em Infrastructure
- [x] RBAC de roles implementado como Vertical Slice/CQRS com policies e sincronização Identity
- [x] Infrastructure reorganizada conforme Sentinel (`Auth`, `Caching`, `Persistence`, `Repositories` e `Services`)
- [x] Permissions implementadas no domínio com matriz por role, claims e policies granulares
- [x] Capacidades transversais do Sentinel documentadas
- [x] Behaviors MediatR base implementados (correlation, logging, performance e validation)
- [x] Idempotência opt-in implementada com aquisição atômica no Redis
- [x] Outbox Dispatcher implementado no projeto Workers
- [x] Health checks operacionais separados em live, ready e startup
- [x] Auditoria persistida no pipeline, com migration e redaction por metadados
- [x] Inbox deduplicada por evento e consumidor
- [x] Resiliência por provider com retry, timeout e circuit breaker
- [x] Cache com invalidação opt-in após sucesso do handler
- [x] Feature flags com configuração e override temporário no Redis
- [x] Registry extensível para providers de Discovery e Monitoring
- [x] BuildingBlocks compartilhado criado e integrado ao Domain, Application e Infrastructure
- [x] Identity backend com registro, login por cookie, refresh rotativo, logout e endpoint `me`
- [x] Recuperação e troca de senha com token single-use no Redis e revogação de sessões
- [x] Dashboard backend de usuários com consulta, ativação/desativação e administração de roles
- [x] API organizada em estrutura MVC modular alinhada ao Dashboard Sentinel, com Controllers, ViewModels, Services, HealthChecks, Middleware e Hubs
- [x] Pipeline MediatR ampliado com Authorization, Caching, Observability, Transaction e RateLimiting distribuído por Redis
- [x] Convites criados como agregado com token single-use (hash SHA256) e expiração
- [x] Retenção e cleanup operacional com worker separado e deletes em lote
- [ ] CI configurado e validado remotamente (workflow criado; bloqueado por limite de minutos do GitHub Actions)

## Fase 13 — Frontend UI/UX (UniFi-Style)

- [x] F13-T001 CSS Variables + Paleta UniFi
- [x] F13-T002 Sidebar colapsável (240px/64px, toggle, tooltips, localStorage)
- [x] F13-T003 Topbar escura + Side Panel Tabs
- [x] F13-T004 Multi-página routing (9 rotas + 404, lazy loading)
- [x] F13-T005 Connectivity Dots (WAN/WWW/WLAN/LAN/VPN)
- [x] F13-T006 Gateway Card (status, CPU, memória, throughput)
- [x] F13-T007 Traffic Chart (SVG, animação, time range tabs)
- [x] F13-T008 Top Clients + Recent Events (PrimeReact)
- [x] F13-T009 Device Cards (Gateway/Switch/AP, SVG icons)
- [x] F13-T010 Port Manager Visual (24 portas, PoE, tooltips)
- [x] F13-T011 Topologia SVG Interativa (pan/zoom, filtros)
- [x] F13-T012 Client List (DataTable, filtros, sorting)
- [x] F13-T013 Device Detail Panel (5 tabs, slide-over)
- [x] F13-T014 Alerts Page (severidades, filtros)
- [x] F13-T015 Settings Page (4 tabs, forms)
- [x] F13-T016 Health Checks Page (cards, status)
- [x] F13-T017 Logs Page (DataTable, filtros)
- [x] F13-T018 Responsividade UniFi (4 breakpoints)
- [x] Redux Toolkit configurado (uiSlice, navigationSlice)
- [x] PrimeReact integrado (DataTable, Tag, Dropdown, InputText, Button, TabView, ProgressBar, InputSwitch)

## Fase 14 — Frontend: completar áreas da plataforma

- [x] F14-T001 Users & Roles (página, rota /users, sidebar, 3 tabs: Usuários/Roles/Permissões)
- [x] F14-T002 Audit (página, rota /audit, mock alinhado a AuditEntrySummary, filtros action/outcome)
- [x] F14-T003 Incidents (página /incidents, severidades, status, SLA breach)
- [x] F14-T004 Notifications (página /notifications, feed + preferências de canal)
- [x] F14-T005 Locations & Racks (página /locations, 2 tabs, uso de U, potência, temperatura)
- [x] F14-T006 SLA/SLO & Services (página /sla, serviços, uptime, error budget)
- [x] F14-T007 Switches/Ports como áreas próprias (página /switches, cards + grid de portas + tabela)
- [x] F14 polish UI: Health no sidebar, chunks vendor/redux no Vite, accent unificado (#3b82f6)

## Alinhamento com o problema real (2026-09-23)

Problema do blueprint: gestão heterogênea on-premises com visibilidade centralizada.
Prioridade validada com o usuário: inventário por tipo → DeviceProfile (checks automáticos por tipo) → ping com alerta (estilo Zabbix) → SNMP impressora (toner/drum) → acesso remoto SSH (switch/servidor) e RDP (computador). Discovery/LLDP e notifications ficam depois do loop básico.
Ordem e comentários: seção "Ordem alinhada com o problema real" em `TASKS.md`. Roadmap: `docs/13-ROADMAP-MVP.md`.
Dados reais de rede (IPs, senhas, MACs) não são versionados no repositório.
Novas Tasks: `F2-T008 DeviceProfile` (ADR antes) e `F11-T006 Credential Store`.
F2-T003 Device concluído (2026-09-24) com seed fictício RFC 5737 em `docs/database/seed-fictitious-devices.sql`.
F2-T008 DeviceProfile concluído (2026-09-24) com ADR-014 Accepted; defaults por tipo materializados no create; override via `PUT /devices/{id}/profile`.
F5-T003 Ping concluído (2026-09-24): ping ICMP sob demanda (1 device + todos), provider `PingMonitoringProvider` com seam `IIcmpClient`, endpoints em `/api/v1/monitoring/ping*` (policy `MonitoringRead`).
Próxima Task planejada (quando autorizada): conformidade docs/21 concluída (MON-006/007/009); `F5-T007 Worker` (polling contínuo — F5-T001 e F5-T002 prontas, MON-010 é o próximo do docs/21) ou `F5-T005 HTTP/HTTPS` — decisão do usuário.

## Próxima fase

Workflow de CI criado para backend e frontend; a execução remota foi bloqueada antes dos steps porque a conta consumiu 2.000/2.000 minutos incluídos do GitHub Actions. A fundação de Identity, o Bootstrap Admin, a reorganização da Infrastructure conforme o Sentinel e Permissions foram implementados e validados localmente. O uso do CI será renovado em 01/10/2026 ou após ajuste do orçamento da conta.

### Evidências da última validação

- `dotnet build InfraControl.slnx --configuration Release`: aprovado, com 1 warning existente.
- `dotnet test InfraControl.slnx --configuration Release`: 12 testes aprovados, 0 falhas.
- Após Permissions: 16 testes aprovados, 0 falhas.
- Após capacidades transversais: 19 testes aprovados, 0 falhas.
- Após auditoria, Inbox, resiliência, cache, flags e providers: 23 testes aprovados, 0 falhas.
- `npm run build` em `frontend/InfraControl.Web`: aprovado.
- `git diff --check`: aprovado.
- Após Identity API e dashboard: `dotnet build` aprovado e 23 testes aprovados, 0 falhas.
- Após retenção e cleanup (F0-T017): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 21 unit + 5 arquitetura aprovados, 0 falhas.
- Após convites (F1-T005): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 29 unit + 5 arquitetura aprovados, 0 falhas.
- Após aceite de convites (F1-T006): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 34 unit + 5 arquitetura aprovados, 0 falhas.
- Após recusa de convites (F1-T007): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 37 unit + 5 arquitetura aprovados, 0 falhas.
- Após leitura de auditoria (F1-T010): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 40 unit + 5 arquitetura aprovados, 0 falhas.
- Após guard de ativação (F1-T013): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 72 unit + 5 arquitetura aprovados, 0 falhas (18 testes novos em UserActivationTests, InvitationActivationTests e AuthenticationActivationTests).
- Nenhuma referência aos namespaces antigos `Infrastructure.Common`, `Infrastructure.Identity` ou `Infrastructure.Migrations` foi encontrada no código.
- Após Fase 13 (F13-T001 a F13-T007): `npm run build` frontend OK (311KB JS, 552KB CSS).
- Após F14-T001 a F14-T007: `npm run build` frontend OK (133 módulos, index 357KB JS, 567KB CSS, 23 rotas). Erros TS pré-existentes em `DeviceDetailPage.tsx` e `DevicesPage.tsx` corrigidos durante F14-T001.
- Após polish F14 (sidebar Health, vendor/redux chunks no Vite, accent unificado `#3b82f6`/`--uni-accent`): `npm run build` frontend OK (133 módulos; vendor 50KB + redux 23KB separados; index 284KB JS, 567KB CSS; zero `#0088ff` restantes no src).
- Após padronização BuildingBlocks (F0-T022): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 73 unit + 8 arquitetura aprovados, 0 falhas (3 testes novos em BuildingBlocksStandardizationTests + isolamento de cache por usuário em MissingBehaviorTests).
- Após tratamento global de exceções (F0-T023): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 82 unit + 8 arquitetura aprovados, 0 falhas (GlobalExceptionHandler mapeia RequestValidation/Argument→400, KeyNotFound→404, UnauthorizedAccess→401/403, Duplicate/InvalidOperation→409, demais→500 genérico; ModelState também no envelope via InvalidModelStateResponseFactory; ~60 mensagens traduzidas para pt-BR em Domain/Application/Infrastructure/controllers; try/catch de erro removidos dos controllers; regra de arquitetura agora valida o handler como ponto único de envelope).
- Após F2-T001 Location: `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 104 unit + 8 arquitetura aprovados, 0 falhas (22 testes novos em LocationTests/LocationCommandTests; slice vertical completo: entidade Location, commands BaseCommand+IAuthorizedRequest com InventoryManage, queries IQuery sem cache com InventoryRead, repositório/porta, LocationConfiguration, LocationsController no envelope, migração AddLocations com índice único case-insensitive `LOWER(name)` via SQL bruto; 5 slices estendidos na regra de arquitetura).
- Após merge F2-T002 + alinhamento docs + BootstrapAdmin env (2026-09-23): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 132 unit + 8 arquitetura aprovados, 0 falhas. `global.json` usa `rollForward: latestFeature` (SDK local 10.0.111). Superadmin: `BootstrapAdmin__*` no `.env.example` (opt-in, idempotente); permissões por role via `RolePermissions` (F1-T004). (28 testes novos em RackTests/RackCommandTests; slice vertical: entidade Rack, nome único case-insensitive por local, CreateRack valida LocationId → 404 e duplicata → 409, DeleteLocation bloqueado com racks → 409, SearchRacks com filtro por locationId, RackRepository/RackConfiguration FK Restrict, RacksController no envelope, migração AddRacks com índice único (location_id, LOWER(name))).
- Após F2-T003 Device (2026-09-24): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 160 unit + 8 arquitetura aprovados, 0 falhas (28 testes novos em DeviceTests/DeviceCommandTests + DeleteRack com devices → 409; slice vertical Device com BaseCommand/IQuery/IAuthorizedRequest, DeviceType (11 tipos), nome único global case-insensitive, validação IP/MAC em pt-BR, FK Restrict location/rack, DevicesController com policies InventoryRead/InventoryManage, migração AddDevices + índice ux_devices_name, seed fictício RFC 5737 em docs/database/seed-fictitious-devices.sql, 5 slices Device adicionados na regra de arquitetura BuildingBlocks).
- Após F2-T008 DeviceProfile (2026-09-24): ADR-014 Accepted (`docs/adr/ADR-014-DEVICE-PROFILE.md`); `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 177 unit + 8 arquitetura aprovados, 0 falhas (17 testes novos em DeviceProfileTests/DeviceProfileCommandTests; `CheckKind` enum + `DeviceProfileDefaults` por tipo em código; materialização no `Device.Create` com `ProfileIsCustom=false`; `ApplyCustomProfile`/`ResetProfileToDefault` com mensagens pt-BR; `UpdateDeviceProfileCommand` (checks XOR useDefault → 400, unknown id → 404); `PUT /devices/{id}/profile` com parse de `string[]`→`CheckKind`; colunas `profile_is_custom` + `monitoring_checks` (CSV via ValueConverter/ValueComparer) + migração `AddDeviceProfile`; devices pré-existentes ficam sem checks até backfill/override conforme ADR).
- Após F5-T003 Ping (2026-09-24): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 219 unit + 8 arquitetura aprovados, 0 falhas (15 testes novos em PingMonitoringProviderTests/PingQueriesTests; `PingMonitoringProvider` chave "Ping" com seam `IIcmpClient` + `SystemNetIcmpClient` (System.Net.NetworkInformation, timeout 2s, mensagens ICMP em pt-BR); `PingDeviceQuery`/`GetPingStatusQuery` com `MonitoringRead`, IQuery+IAuthorizedRequest; device sem IP → `Up=null` + "Device sem IP cadastrado."; `MonitoringController` em `GET /api/v1/monitoring/ping/{id}` e `GET /api/v1/monitoring/ping`; `IDeviceRepository.GetAllOrderedByNameAsync`; DI `IIcmpClient`/`IMonitoringProvider`; 2 queries na regra de arquitetura BuildingBlocks; sem persistência (F5-T002) e sem worker (F5-T007) — escopo mínimo aprovado).
- Após F5-T004 SNMP Printer (2026-09-24): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 237 unit + 8 arquitetura aprovados, 0 falhas (18 testes novos em PrinterSuppliesProviderTests/GetPrinterSuppliesQueryTests; base `PrinterSuppliesProvider` com walk da prtMarkerSuppliesTable `1.3.6.1.2.1.43.11.1.1` (colunas 6/description, 8/max, 9/level), pior carretel em %, keys `CheckKind.SnmpToner`/`SnmpDrum`, erros pt-BR "Destino do SNMP é obrigatório."/"Nenhum toner encontrado na impressora."/"Falha ao consultar SNMP: ..."; seam `ISnmpClient` + `SharpSnmpSnmpClient` (Lextm.SharpSnmpLib 12.5.7, SNMPv2c `VersionCode.V2`, community `Monitoring:Snmp` default public, timeout via `CancellationTokenSource.CancelAfter`); `MonitoringProviderResult` + `Values` opcional retrocompatível; `GetPrinterSuppliesQuery` (só `DeviceType.Printer` → ArgumentException 400, sem IP → 2 supplies com "Device sem IP cadastrado.") em `GET /api/v1/monitoring/printers/{id}/supplies` (MonitoringRead); `SnmpTonerProvider`/`SnmpDrumProvider` no DI; 3ª query na regra de arquitetura BuildingBlocks; sem persistência (F5-T002), sem worker (F5-T007), credencial por device → F11).
- Após conformidade docs/21 MON-006/007 RunCheck (2026-09-24): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 252 unit + 8 arquitetura aprovados, 0 falhas (16 testes novos em `RunCheckCommandTests`/`RunAllChecksCommandTests`; queries de execução `PingDeviceQuery`/`GetPingStatusQuery`/`GetPrinterSuppliesQuery` e testes `PingQueriesTests`/`MonitoringResultWriteTests`/`GetPrinterSuppliesQueryTests` removidos — ADR-006: queries apenas leem; `RunCheckCommand`/`RunAllChecksCommand` (BaseCommand + `MonitoringRead`) executam o provider, gravam `MonitoringResult` (sem IP não grava) e logam `Information`/`Warning` — DoD observabilidade em `docs/17`; regra F5-T004 preservada: check SNMP em non-Printer → ArgumentException 400 "só se aplica a devices do tipo Printer"; API `POST /api/v1/monitoring/run-check/{id}?check=...` e `POST /api/v1/monitoring/run-check?check=...` substituem `GET /ping*` e `GET /printers/{id}/supplies`; `GET checks`/`GET results` inalterados; 2 commands na regra de arquitetura BuildingBlocks).
- Após conformidade docs/21 MON-009 IntegrationTest (2026-09-24): `dotnet test tests/InfraControl.IntegrationTests`: 4 integration testes aprovados, 0 falhas (novo `BaseIntegrationTest` com PostgreSQL real via Testcontainers imagem local `postgres:17-alpine` — pull do Docker Hub exige auth; pipeline MediatR do aplicativo com CorrelationId/Logging/Performance/Validation/Audit/Transaction/Authorization behaviors; repositórios Infrastructure reais + `PingMonitoringProvider` com `FakeIcmpClient` — nenhum teste usa rede; permissões vêm de `RolePermissions.ForRole` via `TestCurrentUserContext` (role → Permission → claim string, mesmo contrato do login); cenários: `RunCheck` com papel Viewer persiste histórico no Postgres e é lido por `GetDeviceResultsQuery`, sem `MonitoringRead` → `UnauthorizedAccessException` "Permissões ausentes: MonitoringRead", device sem IP não persiste, `RunAllChecksCommand` só grava devices com IP; pacote `Testcontainers.PostgreSql` 4.13.0; itens `Monitoring/` e `Common/` do esqueleto agora preenchidos).
- Após autorização por role nos controllers (2026-09-24, decisão do usuário): todos os controllers trocaram `[Authorize(Policy = "...")]` por `[Authorize(Roles = "...")]` mapeado da matriz `RolePermissions` — `Admin` (usuários/roles/convites/audit), `Admin,User` (gestão de inventário, leitura de usuários), `Admin,User,Viewer` (leitura de inventário e monitoramento); policies HTTP por permissão (`RequireClaim`) e legadas (`AdminOnly`/`UserAccess`/`ViewerAccess`) removidas do `Program.cs`; claims `permission` continuam no pipeline MediatR (`AuthorizationBehavior` como 2ª camada); ADR-008 atualizado com a seção "Autorização nos controllers" e `docs/05` (F1-T003/F1-T004/tabela admin) ajustado; build 0 erros, 252 unit + 8 arch + 4 integration aprovados, 0 falhas.
- Após F9-T001 Notification + fluxo Outbox→RabbitMQ→Consumer (2026-09-24, ADR-005/004/012, docs/06+07): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 263 unit + 8 arch + 4 integration aprovados, 0 falhas (12 testes novos: `NotificationTests` invariantes pt-BR, `OutboxDispatchHandlerTests` fila na mesma transação, `NotificationEventConsumerTests` cria notificação/única por evento/role na mensagem; entity `Notification` + `NotificationConfiguration` com índice único `event_id` + migração `AddNotifications`; `OutboxDispatchHandler<TEvent>` (handler MediatR aberto em `Application/Common/Events`) liga o dispatch do `AppDbContext` ao `IDomainEventService` — antes os domain events eram publicados "no vazio"; `NotificationEventConsumer` em `Workers/Consumers/Notifications` consome `UserCreated`/`UserDisabled`/`UserRoleAssigned`/`UserRoleRemoved`/`InvitationAccepted` com idempotência `IInboxStore` (`EventId`+`"NotificationConsumer"`) e logs; consumer registrado no MassTransit do Workers; payload outbox compartilhado com enums string (`OutboxJson`); testes Identity passaram a registrar `IDomainEventService` nos containers de teste).
- Após F5-T001 MonitoringCheck (2026-09-24): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 246 unit + 8 arquitetura aprovados, 0 falhas (9 testes novos em MonitoringCheckTests/MonitoringCheckMaterializationTests/GetDeviceChecksQueryTests; entity `MonitoringCheck` (DeviceId, Kind convertido para string, Enabled) com índice único `(device_id, kind)` + FK `ON DELETE CASCADE`; materialização no `CreateDeviceCommandHandler` e sincronização no `UpdateDeviceProfileCommandHandler` via `IMonitoringCheckRepository.SynchronizeFromProfileAsync` (adiciona faltantes com Enabled=true, remove as fora do perfil, preserva existentes, mesmo SaveChanges); `GetDeviceChecksQuery` (MonitoringRead, IQuery+IAuthorizedRequest, 404 pt-BR "Device '{id}' não encontrado.") em `GET /api/v1/monitoring/checks/{id}`; ordenação por Kind em memória — coluna string ordenaria alfabeticamente ("SnmpDrum" < "SnmpToner"); migração `AddMonitoringChecks`; 4ª query na regra de arquitetura BuildingBlocks; backfill de devices antigos continua fora (ADR-014 "não fazer agora")).
- Após F5-T002 MonitoringResult (2026-09-24): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 260 unit + 8 arquitetura aprovados, 0 falhas (14 testes novos em MonitoringResultTests/MonitoringResultWriteTests/GetDeviceResultsQueryTests; entity `MonitoringResult` com FK cascade para **Device** (e não MonitoringCheck — histórico sobrevive a mudanças de perfil), `Kind`, `Succeeded`, `DurationMs`, `Error` ≤ 500, `Values` JSON via ValueConverter+ValueComparer, `CheckedAt` = `CreateDateTime`; escrita nos polls on-demand: ping device/status (1 linha por device com IP, sem IP não grava) e suprimentos (1 linha por supply com Values levelPercent/description); `GetDeviceResultsQuery` (MonitoringRead, limit 1–500 default 50, ordem desc, 404 pt-BR) em `GET /api/v1/monitoring/results/{id}?limit=`; migração `AddMonitoringResults` com índice `(device_id, CreateDateTime)`; 5ª query na regra de arquitetura BuildingBlocks; pré-requisitos de F5-T007 Worker atendidos — seed de teste ajusta CreateDateTime via SQL porque `SaveChangesAsync` sobrescreve o campo (auditoria)).
- Após conformidade ADR-010/ADR-011 (2026-09-24, docs/08): `dotnet build InfraControl.slnx --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 263+13=276 unit + 8 arch + 4+2=6 integration aprovados, 0 falhas (13 testes novos: `HybridCacheServiceTests` (7) e `DistributedHttpRateLimiterTests` (6) + `HttpRateLimitPipelineTests` (2) em integration). **ADR-010**: `HybridCacheService` (`ICacheService` agora) com Redis primário via porta `IRedisCacheStore` + fallback em memória (`MemoryFallbackStore` singleton com TTL) — leitura/gravação/remoção/exists/try-acquire degradam com `LogWarning`, nunca exceção; conexão Redis com `AbortOnConnectFail=false` (API sobe com Redis fora); `IIdempotencyStore` permanece só no Redis (crítico → fail-closed, docs/08 "política mais restritiva"). **ADR-011**: camada HTTP — `DistributedHttpRateLimiter : PartitionedRateLimiter<HttpContext>` como `RateLimiterOptions.GlobalLimiter` (INCR+EXPIRE no Redis por categoria e partição `user:{sub}`/`ip:{addr}`; Redis indisponível → janela em memória); wiring extraído para `AddHttpRateLimiting` (Program.cs); rejeição 429 + Retry-After + envelope ApiResponse (F0-T023); seção `RateLimit` no appsettings (Default 120/60, Login 5/60, PasswordReset 3/600, Webhook, Ingestion); `[HttpRateLimit]` em login/refresh (Login) e forgot/reset-password (PasswordReset); camada MediatR (`RateLimitingBehavior`+`RedisRateLimiter`) inalterada. Pendência de ambiente pré-existente: API real não sobe sem `MT_LICENSE` (MassTransit 9) — fora do escopo destas Tasks.
- Após F11-T001 RemoteSession (2026-09-24, ADR-013, docs/10): `dotnet build InfraControl.slnx --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 292 unit + 8 arch + 8 integration aprovados, 0 falhas (16+2 testes novos: `RemoteSessionTests` ciclo de vida Open/Close/Fail com mensagens pt-BR, `DeviceSessionsQueryTests` ordem/404/limit + teste de reflexão garantindo que `RemoteSessionDto` não expõe password/secret/credential/token (docs/10 "nunca expor credenciais"), `RemoteSessionIntegrationTests` Admin lê/Viewer negado no PostgreSQL real). Entregues: entity `RemoteSession` + enums `RemoteProtocol`/`RemoteSessionStatus` (Domain/RemoteAccess), `Permission.RemoteSessionsRead` (= device.remote.read; Admin herda por `RolePermissions`, User/Viewer não — matriz connect/use fica para F11-T004), `GetDeviceSessionsQuery` (IQuery+IAuthorizedRequest, StartedAt desc, limit 1–500, 404 pt-BR), `GET /api/v1/inventory/devices/{id}/remote/sessions` com `[Authorize(Roles="Admin")]` (conservador), `remote_sessions` com FK device cascade + índice (device_id, started_at) + migração `AddRemoteSessions`, DI `IRemoteSessionRepository`, query na regra de arquitetura BuildingBlocks. Escopo deliberado: abrir/conectar sessão real só com provider SSH/RDP (F11-T002/T003) e AuditEntry explícito em F11-T005.
- Após F11-T002 SSH Provider (2026-09-24, ADR-013, docs/10): `dotnet build InfraControl.slnx --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 314 unit + 8 arch + 13 integration aprovados, 0 falhas (27 testes novos: `SshSessionCommandTests` (12) ciclo Open→Close com fakes — sessão Active persistida + conexão registrada, 404 device/sessão pt-BR, sem IP → ArgumentException 400, credencial ausente → InvalidOperationException 409 sem persistir, conexão falha → sessão Failed persistida (FailureReason truncada em 500) + 409, close de sessão de outro device → 404 sem fechar, já finalizada → 409, `RequiredPermissions` = RemoteSshConnect; `SshRemoteProviderTests` (5) CanHandle/Name + porta TCP fechada em loopback → `RemoteConnectResult` falho sem exceção vazando e sem senha no erro; `ConfigurationRemoteCredentialSourceTests` (3) credencial OK / vazia → "*não configurada*"; `SshSessionIntegrationTests` (5) Admin abre→fecha→histórico Closed no PostgreSQL real, Viewer negado em open/close com "*RemoteSshConnect*", sem IP → 400, device desconhecido → 404). Entregues: ports `IRemoteProvider` (`RemoteCredential`/`RemoteConnectResult`), `IRemoteCredentialSource`, `IRemoteConnectionRegistry` (Application/RemoteAccess/Sessions/Ports), `Permission.RemoteSshConnect` (= device.ssh.connect; Admin herda por `RolePermissions`, User/Viewer não — matriz completa em F11-T004), `OpenSshSessionCommand`/`CloseSshSessionCommand` (BaseCommand + IAuthorizedRequest; **sem restrição de tipo de device — decisão do usuário**, qualquer device com IP pode abrir SSH), `SshRemoteProvider` (SSH.NET **2026.0.0** — versão 2025.1.0 tinha advisories GHSA-mggc-4xg6-vcxf/GHSA-q939-rpr3-3284, NU1903; `Connect()` só existe síncrono → `Task.Run`, timeout em `ConnectionInfo.Timeout`, erro vira `RemoteConnectResult` pt-BR com senha mascarada), `ConfigurationRemoteCredentialSource` (seção `RemoteAccess:Ssh` provisória **vazia no git**, `Username`/`Password`/`Port`/`TimeoutSeconds` — F11-T006 Credential Store substitui), `RemoteConnectionRegistry` (singleton `ConcurrentDictionary`, dispose no close), DI + pacote no csproj, `POST` (201)/`DELETE` (200) em `RemoteSessionsController` (`[Authorize(Roles="Admin")]`), 2 commands na regra BuildingBlocks, `BaseIntegrationTest` com `FakeSshRemoteProvider` (nenhum teste abre rede). Falha de conexão persiste `RemoteSession` `Failed` antes do throw (AuditBehavior cuida da auditoria — F11-T005).

## Regra

Não considerar arquitetura ou código como concluído somente porque foi planejado. O estado deve refletir evidências reais.
