# F46-T006 — Saúde dos serviços na plataforma (containers + links + SignalR)

> Fase: F46 (engenharia de produto) · Data: 2026-10-25 · Status: **concluída**
> Escopo: agregador de saúde de todos os serviços alcançáveis + links de console na HealthPage + aviso de mudança em tempo real.

## 1. Antes (estado auditado)

- `/health` cobria **apenas** `postgresql`, `redis`, `ai-runtime` + o `masstransit-bus` já existente;
- **nenhum** serviço irmão (frontend, RabbitMQ, Zabbix, Guacamole, Evolution, Adminer) era sondado;
- workers não tinham checagem alguma (processo separado, sem porta);
- HealthPage exibia os checks que `/health` devolvia, **sem links**;
- nenhum aviso quando o estado mudava (só refresh manual).

## 2. Depois (o que foi entregue)

### 2.1 Novos health checks (backend)

| Check | Como verifica | Tag |
|---|---|---|
| `outbox-backlog` | Mensagens não processadas há >5 min = workers atrasados (**Degraded**); ≥100 = **Unhealthy** (problema reportado no `Data`) | `workers` |
| `svc-{nome}` (um por serviço) | HTTP GET (timeout 5s) via `Health:Services` (Name/Url/Link) — registrado com `HealthCheckRegistration` | `services` |
| `postgresql`, `redis`, `ai-runtime`, `masstransit-bus` | já existentes (inalterados) | — |

`/health` agora devolve também `link` (do `Data` do check) quando o serviço declara console.

### 2.2 Composição (docker-compose.yml — `Health__Services__*`)

Sondagem pela **rede interna** (DNS por nome de serviço, §48) + link público na HealthPage:

| Serviço | Sonda interna | Console (link) |
|---|---|---|
| frontend | `http://frontend` | `https://localhost:8443` |
| rabbitmq | `http://rabbitmq:15672` | `http://localhost:15672` |
| zabbix-web | `http://zabbix-web` | `http://localhost:8086` |
| evolution | `http://evolution-api:8080` | `http://localhost:8081` |
| guacamole | `http://guacamole:8080/guacamole` | — (sem porta pública) |
| adminer | `http://adminer` | `http://localhost:8082` |

Documentado em `.env.example` (padrão `Health__Services__N__Name/Url/Link`).

### 2.3 Aviso em tempo real (SignalR)

`HealthStatusNotifier` (hosted service da API): avalia o agregado a cada **60s**; quando o **status global muda**, publica `health-status` no hub (`{status, previous, changedAt, unhealthy[]}`).

Frontend:
- `services/signalr/notifications.ts`: novo barramento `onHealthStatusChange` no **mesmo hub singleton** (1 conexão por aba);
- `HealthPage`: ao receber o evento → recarrega os dados e mostra banner "Status da saúde mudou: X → Y (problemas: …)" com "Dispensar".

### 2.4 HealthPage

- cards de check com âncora **"Abrir console ↗"** quando o serviço tem link (target blank);
- banner de mudança de saúde (SignalR);
- estrutura anterior (Zabbix/UniFi/alertas) intacta.

## 3. Testes

| Teste | Resultado |
|---|---|
| `HealthAggregationE2ETests` (E2E): `/health` cobre `postgresql`, `redis`, `outbox-backlog` com backlog Zero (Healthy); aceita 503 agregado no host de teste (falta RabbitMQ real — `masstransit-bus`) | ✅ 1/1 |
| `HealthPage.test.tsx` (front) | ✅ 5/5 |
| Scope do hub do front (`AppLayout`, `NotificationsPage`, `useOperationalRefresh`) | ✅ 28/28 |
| `tsc --noEmit` | ✅ 0 erros |

## 4. Pendências/observações

- **Workers (processo Docker `workers`)**: cobertos indiretamente pelo `outbox-backlog` (sem porta exposta); se um dia expuserem `/health`, promover a check direto.
- **Evolution DB / Guacamole DB / Zabbix DB**: Postgres secundários dos perfis — fora do agregado por padrão (perfil `remote`/`wa` opcionais); acrescentáveis via `Health__Services__` sem código.
- `OperationsCenterPage.test.tsx` está com OOM/lentidão no worker do vitest — arquivo editado pela **sessão paralela** (F44/landing), fora do escopo desta task (não toquei).
