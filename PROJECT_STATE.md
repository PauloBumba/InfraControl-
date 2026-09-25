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
Próxima Task planejada (quando autorizada): `F5-T002 MonitoringResult` (histórico de polls, pré-requisito do worker) ou `F5-T005 HTTP/HTTPS` — decisão do usuário.

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
- Após F5-T001 MonitoringCheck (2026-09-24): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 246 unit + 8 arquitetura aprovados, 0 falhas (9 testes novos em MonitoringCheckTests/MonitoringCheckMaterializationTests/GetDeviceChecksQueryTests; entity `MonitoringCheck` (DeviceId, Kind convertido para string, Enabled) com índice único `(device_id, kind)` + FK `ON DELETE CASCADE`; materialização no `CreateDeviceCommandHandler` e sincronização no `UpdateDeviceProfileCommandHandler` via `IMonitoringCheckRepository.SynchronizeFromProfileAsync` (adiciona faltantes com Enabled=true, remove as fora do perfil, preserva existentes, mesmo SaveChanges); `GetDeviceChecksQuery` (MonitoringRead, IQuery+IAuthorizedRequest, 404 pt-BR "Device '{id}' não encontrado.") em `GET /api/v1/monitoring/checks/{id}`; ordenação por Kind em memória — coluna string ordenaria alfabeticamente ("SnmpDrum" < "SnmpToner"); migração `AddMonitoringChecks`; 4ª query na regra de arquitetura BuildingBlocks; backfill de devices antigos continua fora (ADR-014 "não fazer agora")).
- Após F2-T005 MAC (2026-09-25): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 273 unit + 8 arquitetura aprovados, 0 falhas (26 testes novos em MacAddressTests/MacAddressCommandTests/NetworkInterfaceCommandTests; slice vertical MacAddress ancorado em NetworkInterface — decisão do usuário — com MAC único GLOBAL case-insensitive: pré-check 409 "Já existe um cadastro com o MAC '{mac}'." + índice `ux_mac_addresses_mac (LOWER(mac))` via SQL bruto na migração `AddMacAddresses`; normalização pt-BR aa:bb:cc:dd:ee:ff aceitando `:`, `-`, `.` ou 12 hex (400 vazio/inválido); interface inexistente → 404; DeleteNetworkInterface com MACs → 409 fechando a cadeia Device→Interface→MAC; busca por interface/device(subquery)/termo ignorando separadores; MacAddressesController em api/v1/inventory/mac-addresses; 5 slices MacAddresses na regra de arquitetura). OBS: trabalho paralelo de outra sessão (F2-T008/F5-T001/F5-T003/F5-T004) conflitou via stash em 4 arquivos (AppDbContext, DependencyInjection, snapshot, regra de arquitetura) — conflitos resolvidos mantendo os dois lados, 0 marcadores restantes).
- Após F2-T006 IP (2026-09-25): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 300 unit + 8 arquitetura aprovados, 0 falhas (27 testes novos em IpAddressTests/IpAddressCommandTests/NetworkInterfaceCommandTests; slice vertical IpAddress ancorado em NetworkInterface com IP único GLOBAL e normalização canônica v4/v6 (pré-check 409 "Já existe um cadastro com o IP '{ip}'." + índice `IX_ip_addresses_ip UNIQUE` gerado via LINQ — sem SQL bruto); mensagens 400 "O IP é obrigatório."/"O IP informado é inválido."; interface inexistente → 404; DeleteNetworkInterface com IPs → 409 fecha a dependência da F2-T004 (cadeia Device→Interface→MAC/IP completa); busca por interfaceId/deviceId(subquery)/termo; IpAddressesController em api/v1/inventory/ip-addresses; migração AddIpAddresses com FK Restrict; 5 slices IpAddresses na regra de arquitetura).
- Após F2-T007 Busca global (2026-09-25): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 307 unit + 8 arquitetura aprovados, 0 falhas (7 casos novos em GlobalSearchQueryTests — 5 Facts + Theory com 2 InlineData; escopo definido pelo usuário: as 6 fontes do inventário, somente backend — barra de busca fica para task F14 futura; `GET /api/v1/inventory/search?term=&limit=` com `InventoryRead`; `GlobalSearchQuery` (IQuery+IAuthorizedRequest, sem cache) faz fan-out sobre os seis `SearchAsync` existentes — nenhuma consulta SQL própria, sem migração; termo vazio/branco → 400 "O termo de busca é obrigatório."; `Limit` clamp 1..50 vale POR fonte e `TotalCount` soma o total real de cada fonte; ordem fixa location→rack→device→interface→mac→ip para resposta determinística; `GlobalSearchItemDto(type,id,name,subtitle)` com subtitle=IP do device; `GlobalSearchController` em `api/v1/inventory/search`; 1 slice GlobalSearchQuery na regra de arquitetura).
- Após F3-T001 Switch (2026-09-25): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: 313 unit + 8 arquitetura aprovados, 0 falhas (6 testes novos em Network/SwitchQueryTests — reuso do fluxo Device F2-T003 conforme docs/03, sem entidade Switch nova e sem migração; `SearchSwitchesQuery` delega ao `SearchDevicesQueryHandler` com `Type: "Switch"` fixo (reusa clamping/parse/ToDto do F2-T003, passa termo/locationId/rackId); `GetSwitchQuery` valida `DeviceType.Switch` → 404 "Switch '{id}' não encontrado." tanto para id desconhecido quanto para device de outro tipo; `DeviceDto` + mapper `CreateDeviceCommandHandler.ToDto` reaproveitados; `SwitchesController` em `GET api/v1/network/switches` e `/{id}` com InventoryRead; módulo `Application.Network.Switches` aberto para F3-T002 (ports); 2 slices na regra de arquitetura).
- Após Fase 1 global usings (2026-09-25): convenção para não poluir arquivos novos com os mesmos `using` — registros `<Using Include>` nos csproj de Application (MediatR, BuildingBlocks.Application, Application.Common.Interfaces, Domain.Authorization), Api (MediatR, Mvc, Authorization, BuildingBlocks.Application.Response), Infrastructure (Microsoft.EntityFrameworkCore) e UnitTests (FluentAssertions, EFCore, Data.Sqlite, Infrastructure.Persistence), seguindo o padrão `Xunit` que já existia nos testes; `dotnet build --configuration Release` 0 erros (sem CS0104/CS0121 de ambiguidade, nenhum aviso novo), 8 arquitetura + 313 unit aprovados, 0 falhas; nenhum arquivo `.cs` alterado (só 4 csproj, +30 linhas); documentado em docs/24-BUILDING-BLOCKS.md §Global usings; Fase 2 (remover `using` redundantes dos ~200 arquivos existentes) fica para ciclo futuro opcional; push `8773edf` realizado no mesmo ciclo da F3-T002 (anotação pendente registrada neste checkpoint).
- Após F3-T002 SwitchPort (2026-09-25): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: **339 unit + 8 arquitetura** aprovados, 0 falhas (26 testes novos: 8 domínio `SwitchPortTests` — Create/Update com validações pt-BR, nome ≤60, descrição ≤500, device id vazio; 17 `SwitchPortCommandTests` — create 404 switch inexistente/não-switch reusando `GetSwitchQueryHandler`, 409 nome repetido por switch, update próprio nome, delete 404, search guard + paginação + ordenação lexicográfica, get 404; 1 `DeviceCommandTests` — delete com portas → 409 "Não é possível excluir o device: existem portas vinculadas." antes do check de interfaces); migração `20260925161824_AddSwitchPorts` (tabela `switch_ports`, FK Restrict p/ devices + índice único bruto `ux_switch_ports_device_name (device_id, LOWER(name))`); entidade `SwitchPort` em Domain com `LinkUp bool?` null = não observado (F4 SNMP preenche); rotas `GET/POST api/v1/network/switches/{switchId}/ports` e `GET/PUT/DELETE api/v1/network/switch-ports/{id}`; 5 tipos novos `Network.SwitchPorts.*` na regra de arquitetura; campos operacionais (velocidade/PoE/VLAN/tráfego) ficam para F4/F3-T004 conforme decisão do usuário.
- Após F0-T024 Registro automático de repositórios (2026-09-25): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: **343 unit + 8 arquitetura** aprovados, 0 falhas (4 testes novos `RepositoryRegistrationTests` — registro completo Scoped das 11 interfaces `I*Repository` do Application, fail-fast com nome da interface sem implementação, prevalência do registro manual via TryAdd sem descriptor duplicado, resolução concreta pelo provider com SQLite em memória); `RepositoryRegistrar.AddRepositoryRegistrations` varre Application↔Infrastructure estilo `RegisterServicesFromAssembly` do MediatR (casa por atribuição, cobre `IUserRoleRepository`→`IdentityUserRoleRepository`); 11 linhas `AddScoped<I*Repository>` manuais removidas do `DependencyInjection.cs` (só sobra a chamada única); casos especiais seguem manuais (IUnitOfWork, IConnectionFactory, IIdempotencyStore, ICacheService, IMonitoringProvider multi-impl, IEventBus, IInvitationAccountPort); documentado em docs/24 §Registro automático de repositórios; nova task criada e executada com autorização do usuário.
- Após F3-T003 VLAN (2026-09-25): `dotnet build --configuration Release` aprovado, 0 erros; `dotnet test --configuration Release`: **371 unit + 8 arquitetura** aprovados, 0 falhas (28 testes novos: 11 `VlanTests` domínio — Create/Update, nome ≤120, tag 802.1Q 1..4094, mensagens pt-BR; 17 `VlanCommandTests` — create 409 número e 409 nome case-insensitive, search ordenada por `VlanNumber` (ordem operacional 802.1Q) com filtro nome/descrição e clamp de paginação, get/update/delete 404, update 409 para número/nome de outra VLAN e ok nos próprios valores); migração `20260925175849_AddVlans` (tabela `vlans`, sem FK, `IX_vlans_vlan_number` unique via LINQ + índice bruto `ux_vlans_name (LOWER(name))`); slice `Application.Network.Vlans` com 5 tipos na regra de arquitetura; rotas `GET/POST api/v1/network/vlans` e `GET/PUT/DELETE .../vlans/{id}`; **`IVlanRepository`/`VlanRepository` registrados automaticamente pelo F0-T024 — zero linhas novas de DI** (primeira prova em produção da varredura, coberta pelo teste dinâmico de RepositoryRegistrationTests); N:N SwitchPortVlan fica para F3-T004; `devices.vlan_id (Guid?)` permanece sem FK — decisão de amarrar FK pendente.

## Regra

Não considerar arquitetura ou código como concluído somente porque foi planejado. O estado deve refletir evidências reais.
