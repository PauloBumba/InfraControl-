# F46-T001 — Seed mock determinístico com marcação + purge

**Data:** 2026-10-24 · **Status:** concluída.

## Obvio

Ambiente de demonstração com dados realistas sem poluir o banco real, marcados de forma que possam ser removidos.

## O que foi feito

| Peça | Detalhe |
|---|---|
| `MockDataOptions` | Seção `MockData`: `Marker="[MOCK]"`, `EmailDomain="mock.test"`, senha única forte |
| `MockDataSeeder` | `Infrastructure/Persistence/Seed` — espelha o padrão dos seeders do boot, **idempotente por entidade** |
| Dados | 3 usuários Identity+Domain (Admin/Operator/Viewer, logáveis), 2 locations, 2 services, 8 devices (2 locais, IPs `10.99.x`), 4 incidents (severidades/status variados), 3 alerts (`CheckKind.Ping`), 4 tasks (kinds/priorities), 1 audit |
| `Purge` | Remove **apenas** o marcado, ordem inversa das FKs + órfãos de usuário (sessions/settings/roles/preferences/recipients) preservando dados reais |
| Boot | `Program.cs` roda **Purge OU Enabled** (nunca as duas); desligado = intocado |
| Compose/env | `MockData__Enabled`/`Purge` via env; `.env` dev com `MOCK_DATA_ENABLED=true`; `.env.example` documentado |

## Testes

`MockDataSeederTests` **4/4**: popula tudo, idempotente ×2, usuários logáveis (`CheckPassword`+`IsInRole`), purge idempotente preserva dados reais.

## Smoke dev

Log de boot `users 3, locations 2, services 2, devices 8, incidents 4, alerts 3, tasks 4, audits 1` + contagens conferidas no Postgres (`DomainUsers` e `AspNetUsers` espelhados).

## Achado derivado (levado à T008)

O seed ampliou o volume de deliveries/entidades e **expôs** o ruído do poll dos workers no teste de orçamento de queries da T003 — resolvido na T008 com `QuietE2EHost` (workers desligados só nessa classe de teste).

## Relatórios F46

[T002](F46-T002-varredura-e2e.md) · [T003](F46-T003-latencia.md) · [T004](F46-T004-integridade-signalr.md) · [T006](F46-T006-saude-servicos.md) · [T007](F46-T007-zabbix.md) · [T008](F46-T008-correcoes.md) · [Consolidado](F46-RELATORIO-CONSOLIDADO.md)
