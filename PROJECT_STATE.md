# PROJECT_STATE

## Estado

**Fase:** Fundação técnica e execução incremental
**Status:** Em execução incremental
**Última atualização:** 2026-09-23

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
Próxima Task planejada (quando autorizada): `F2-T003 Device` com seed fictício.

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

## Regra

Não considerar arquitetura ou código como concluído somente porque foi planejado. O estado deve refletir evidências reais.
