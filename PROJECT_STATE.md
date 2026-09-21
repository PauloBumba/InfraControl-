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
- [x] RBAC de roles implementado com policies, sincronização Identity e proteção do último Admin
- [ ] CI configurado e validado remotamente (workflow criado; bloqueado por limite de minutos do GitHub Actions)

## Próxima fase

Workflow de CI criado para backend e frontend; a execução remota foi bloqueada antes dos steps porque a conta consumiu 2.000/2.000 minutos incluídos do GitHub Actions. A fundação de Identity e o Bootstrap Admin foram implementados e validados localmente; o uso do CI será renovado em 01/10/2026 ou após ajuste do orçamento da conta.

## Regra

Não considerar arquitetura ou código como concluído somente porque foi planejado. O estado deve refletir evidências reais.
