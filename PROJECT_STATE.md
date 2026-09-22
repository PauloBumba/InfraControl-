# PROJECT_STATE

## Estado

**Fase:** Fundação técnica e execução incremental
**Status:** Em execução incremental
**Última atualização:** 2026-09-21

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
- [ ] Retenção e cleanup operacional (próximo slice)
- [ ] CI configurado e validado remotamente (workflow criado; bloqueado por limite de minutos do GitHub Actions)

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
- Nenhuma referência aos namespaces antigos `Infrastructure.Common`, `Infrastructure.Identity` ou `Infrastructure.Migrations` foi encontrada no código.

## Regra

Não considerar arquitetura ou código como concluído somente porque foi planejado. O estado deve refletir evidências reais.
