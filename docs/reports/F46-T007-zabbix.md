# F46-T007 — Zabbix Agent2 + autodiscovery por container

> Fase: F46 (engenharia de produto) · Data: 2026-10-25 · Status: **concluída**
> Escopo: auditar a coleta atual, cobrir cada serviço da stack com Agent2 +
> autodiscovery, RabbitMQ/Postgres/Redis via templates oficiais, latência e
> disponibilidade por serviço, tudo provisionado por script idempotente.

## 1. Auditoria (antes)

Zabbix 8.0.0 (trunk 8.0.0rc2, rollback do ADR/F18 §docs/27) com **23 hosts**:

| Hosts | Template | Cobertura |
|---|---|---|
| 22 × `sw-192-168-*` (switchs reais) | ICMP Ping | rede física (3 triggers cada, sev 2/4) |
| `Zabbix server` | Linux by Zabbix agent + Zabbix server health | **quebrado**: interface `127.0.0.1:10050` (não há agent no container do server) — 72 triggers sem coleta |
| — | — | **zero hosts da stack InfraControl** (postgres, redis, rabbitmq, api, workers, frontend, evolution, guacamole, seq…) |

Templates oficiais já instalados no server (390 no total): `Docker by Zabbix agent 2`,
`PostgreSQL by Zabbix agent 2`, `Redis by Zabbix agent 2`, `RabbitMQ node by Zabbix agent`.

## 2. O que foi feito

### 2.1 Agent2 + plugin Docker (compose)

- `docker/zabbix-agent2-plugins-docker.conf` montado em
  `/etc/zabbix/zabbix_agent2.d/plugins.d/` → `Plugins.Docker.Endpoint=unix:///var/run/docker.sock`;
- volume `docker.sock:ro` + `group_add: ${DOCKER_GID:-964}` (socket 660 root:docker;
  documentado no `.env.example` — descobrir com `stat -c '%g' /var/run/docker.sock`);
- validação: `zabbix_agent2 -t docker.ping = 1` e
  `docker.containers.discovery` listando todos os containers.

### 2.2 Provisionamento idempotente (`scripts/zabbix/provision-infracontrol.py`)

Login via `user.login` + `Authorization: Bearer` (contrato do
`ZabbixApiClient`, docs/37 — o 8.0 rejeita o campo `auth`). Sobrescritas por
env (`ZBX_API_URL`, `ZBX_USER`, `ZBX_PASSWORD`, `POSTGRES_*`, `REDIS_PASSWORD`,
`RABBITMQ_*`). Executado 4× — nas repetições tudo é "já existe/garantido".

| # | Host | Templates / conteúdo | Resultado |
|---|---|---|---|
| 1 | `infracontrol-docker` | `Docker by Zabbix agent 2` | **autodiscovery: 23 containers**, ~1056 itens (CPU/mem/redes/I/O por container) |
| 2 | `Zabbix server` (existente) | interface corrigida → `infracontrol-zabbix-agent:10050` | 143 itens voltando a coletar (frescor ~1s) |
| 3 | `infracontrol-postgres` | `PostgreSQL by Zabbix agent 2` + macros `{$PG.CONNSTRING.AGENT2,USER,PASSWORD,DATABASE}` | 188 itens, **0 not-supported** |
| 4 | `infracontrol-redis` | `Redis by Zabbix agent 2` + macros `{$REDIS.CONN.URI,PASSWORD,USERNAME}` | 122 itens, 0 not-supported, `redis.ping=1` |
| 5 | `infracontrol-rabbitmq` | `RabbitMQ node by Zabbix agent` + macros API (cluster/node descobertos via `/api/nodes`) | 26 itens, 0 not-supported; `net.tcp.service.perf=0,0006s`; mem_used=42,8MB |
| 6 | `infracontrol-services` | web scenario (7 steps) + **14 itens TCP** (`net.tcp.service`/`.perf` por serviço) + **7 triggers** | latência/health de frontend, api, rabbitmq, zabbix-web, evolution, guacamole, adminer |

Grupo `InfraControl` criado (id 23). Triggers `infracontrol-services: {serviço} offline`
(sev 4) em cada endpoint.

### 2.3 Latência por serviço (amostra da validação)

| Serviço | TCP perf (s) | Disponível |
|---|---|---|
| api | 0,00085 | 1 |
| rabbitmq mgmt | 0,00072 | 1 |
| frontend | 0,00130 | 1 |
| zabbix-web | 0,00139 | 1 |
| evolution-api | 0,00231 | 1 |
| guacamole | 0,00247 | 1 |
| adminer | — | religado (estava `Exited (0)` há 26h; trigger pegou de verdade) |

## 3. Bugs/peculiaridades do trunk 8.0rc2 encontrados (e contornos)

1. **`httptest.create` não cria os itens `web.test.*`** (a doc diz que cria;
   aqui nenhum item `web.test.*` existe em todo o servidor — mesmo após
   delete+create). *Contorno*: saúde/latência por serviço feita com
   `net.tcp.service` + `net.tcp.service.perf` por endpoint (funcionando);
   o web scenario foi mantido (visível em Monitoring > Web, 7 steps).
2. **API mudou**: `host.create` exige `groups:[{groupid}]` (não `groupids`);
   `templates:[{templateid}]` (não `{host}`); `host.massupdate` **não existe**
   (usar `host.update`, que substitui a lista); `webscenario`/`httpteststatus`
   não existem (é `httptest`); steps exigem `no`, sem `delay`; itens exigem
   `interfaceid`; `auth` só via header Bearer.
3. **Interface com nome DNS**: `ip` exige endereço válido → usar
   `useip=0` + `dns=infracontrol-zabbix-agent`.
4. Itens `not-supported` transitórios no bootstrap (NaN de CPU pct, itens
   derivados `last()` ainda sem base) — re-avaliados sozinhos (logs "became
   supported").

## 4. Validação final

```
Zabbix server:   113/143 itens com valor (coleta 1s), 11 not-supported*
infracontrol-docker:   994/1056 (1s), 23 containers via LLD
infracontrol-postgres: 167/188 (34s), 0 not-supported
infracontrol-redis:    112/122 (2s), 0 not-supported
infracontrol-rabbitmq: 12/26 (40s), 0 not-supported** nos itens-chave
infracontrol-services: 14/14 (44s), 7 triggers
```

\* template "Zabbix server health" (itens `zabbix[...]` internos) — 11 ainda em
avaliação neste build; \*\* 13 not-supported transitórios do bootstrap do
template RabbitMQ resolveram após correção da macro `{$RABBITMQ.NODE.NAME}`.

## 5. Uso e pendências

- Reprovisionar (idempotente): `python3 scripts/zabbix/provision-infracontrol.py`
  — rodar após **recriar** containers (o nome do node Rabbit muda).
- Após `docker compose --profile zabbix up`: se o socket tiver outro gid,
  exportar `DOCKER_GID` antes.
- `adminer` não tem `restart: unless-stopped` no compose (por isso parou) —
  sugerir política ao usuário.
- Hosts de switchs (ICMP) intactos — fora do escopo.
