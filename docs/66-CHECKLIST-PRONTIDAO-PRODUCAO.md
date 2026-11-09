# 66 — Checklist de prontidão para produção

> **Origem:** validação ao vivo da plataforma contra o banco real (2026-11-08),
> sob direção do usuário ("testar CRUD por endpoint, tempo de resposta, carga
> até cair, IA e investigar lentidão pós-criação — deixar checklist no repo").
> **Como usar:** cada item tem status `[x]` (verificado), `[!]` (defeito
> encontrado — corrigir antes do go-live) ou `[ ]` (pendência de produto/ops).
> Evidências reproduzíveis: `scripts/validation/smoke-crud-latency.mjs` e
> `scripts/validation/post-create-latency.mjs` (ver §8).

---

## 1. Resumo executivo da validação

| Área | Resultado |
| --- | --- |
| CRUD ao vivo (10 domínios, list/create/get/update/delete) | **54 PASS / 0 defeitos de domínio** — payloads inválidos retornam validação pt-BR correta |
| Latência regime estável (20 iterações/endpoint) | p50 **6–15 ms**, p95 **7–19 ms** em 14 endpoints |
| Latência pós-criação (GET logo após POST) | overhead p95 **+4 ms** — **não há lentidão pós-criação na API** |
| Boot da API | ~3 s do start do container até `Application started` |
| Carga dentro do rate limit (ab, c=5, n=100) | **427 req/s**, p95 17 ms, 0 falhas |
| Carga estourando o rate limit | 429 em **5–7 ms** (rejeição barata) — limite Default **300 req/60 s** |
| Carga com concorrência alta (c=100+) | **colapso corrigido (D1)** — pós-fix: p95 211–251 ms em c=100 (§2.1) |
| IA ao vivo | chat, automation, validate e test-connection **todos 200** (§5) |
| `/health` agregado | **503** — 1 check central com defeito real + checks de serviços não implantados (§4.2) |

---

## 2. Defeitos encontrados (corrigir antes do go-live)

### [x] D1 — Orçamento de conexões do Postgres (ALTO) — **CORRIGIDO 2026-11-08**

- **Evidência (original):** `max_connections = 100` (default do image) **iguais** ao
  `Max Pool Size` default do Npgsql (**100**). Sob carga c=100 a API tomou as
  100 conexões (todas `idle`, todas do container da API) e:
  - `psql`/migrações/health passam a falhar com `FATAL: sorry, too many clients`;
  - workers ficam sem conexão;
  - requisições que precisam de conexão **esperam 24–28 s** (p90) mesmo com
    CPU da API em 2 % — o handler em si é rápido (p50 4 ms), o tempo todo é
    fila de pool.
- **Repro (original):** subir API, `ab`/Node a c=100 por ~1 min e observar
  `pg_stat_activity` (0 queries ativas + 100 conexões idle da API).
- **Correção aplicada:**
  1. compose base: `postgres` com
     `command: -c max_connections=${POSTGRES_MAX_CONNECTIONS:-200}`;
  2. connection string do anchor `&app-env` (api **e** workers) com
     `Maximum Pool Size=${POSTGRES_MAX_POOL_SIZE:-20}` — o overlay de produção
     já exigia a variável; o dev ficou descoberto;
  3. `appsettings.json` da API e dos Workers com `Maximum Pool Size=20`
     (roda-fora-do-compose);
  4. `.env`/`.env.example`/`.env.production.example` com as duas variáveis;
     preflight valida `POSTGRES_MAX_CONNECTIONS` (warn se ausente);
  5. teste `tests/scripts/test-db-connection-budget.sh` (**13/13**) cobre
     wiring + matemática (`max_connections ≥ 2×pool + folga`).
- **Revalidação ao vivo (2026-11-08):** `SHOW max_connections = 200`
  (source=command line), pool=20 em api+workers; carga Node c=100 n=1000 em
  `/auth/mfa/status` (endpoint que colapsava): **100% 200, p50 63–67 ms,
  p95 211–251 ms, 1001–1176 rps** (antes: p95 24–28 s); `psql` conecta
  durante a carga (antes: `too many clients`); 22 conexões idle estáveis
  (≈ pool 20 + workers), zero estouro.

### [!] D2 — `/health` sempre 503: probe do frontend erra a porta (MÉDIO)

- **Evidência:** `svc-frontend Unhealthy: Connection refused (frontend:8445)`.
  O probe usa `http://frontend` (porta 80) → nginx responde
  `301 Location: https://$host:8445/` (o `https_port_suffix` é o mapeado do
  **host**) → o HttpClient do probe segue o redirect para
  `https://frontend:8445` **dentro da rede**, onde nada escuta (nginx interno
  é 443).
- **Correção sugerida (escolher uma):** (a) probe direto em `https://frontend`
  com `DangerousAcceptAnyServerCertificate`; (b) `AllowAutoRedirect=false` no
  client `health-probe` e tratar 3xx como Healthy; (c) `location /healthz { return 200; }`
  no :80 do nginx do front e apontar o probe para lá.

### [!] D3 — `svc-adminer` erra a porta (BAIXO)

- Probe `http://adminer` (porta 80), container do Adminer escuta **8080**
  (mapeado 8082 no host). Corrigir URL para `http://adminer:8080`.

### [!] D4 — `PUT /inventory/locations/{id}` com nome duplicado → 500 (MÉDIO)

- **Evidência (repro):** criar `DUP-TEST-1` e `DUP-TEST-2`; renomear o 2º para
  `DUP-TEST-1` → **500** `Ocorreu um erro inesperado`. Log:
  `DbUpdateException … 23505: duplicate key … ux_locations_name` (a mensagem
  de domínio `Já existe um local com este nome` não sobe para o envelope).
- **Esperado:** 409/400 com a mensagem de domínio (como os demais erros
  pt-BR). Corrigir no handler de Update/ou mapear `DbUpdateException` de
  unique violation para conflicto.

### [!] D5 — Defaults de canais duplicados no caminho legado (MÉDIO)

- **Evidência:** sem linha em `notification_routing_policies`,
  `GET /notifications/routing` devolve
  `["Email","SignalR","Email","SignalR","WhatsApp"]`.
- **Causa raiz (reproduzida em console isolado):** o inicializador da
  propriedade `NotificationChannelsOptions.Default = ["Email","SignalR"]` +
  comportamento do `ConfigurationBinder` (**acrescenta** os valores da seção
  em vez de substituir) → lista inicial (2) + config mesclada
  `[Email,SignalR,WhatsApp]` (3, vem do `.env` `NOTIFY_DEFAULT_2=WhatsApp`) = 5.
- **Impacto:** UI mostra canais repetidos; se o admin salvar esse estado, a
  string persistida os guarda. O fanout do consumer deduplica
  (`configured.Contains`) — **não há envio duplo hoje**.
- **Correção sugerida:** remover o inicializador (deixar `[]`) **ou** limpar a
  lista no `PostConfigure`; validar `DefaultChannelNames` com `Distinct()`.

### [!] D6 — Logs duplicados (cada linha 2× no console) (BAIXO)

- **Causa:** `Program.cs` monta o bootstrap com
  `.ReadFrom.Configuration()` (que **já** inclui o sink Console do appsettings)
  **e** `.WriteTo.Console()` → dois sinks Console. `docker logs` mostra tudo
  em dobro (e o dobro duplica I/O do sink).
- **Correção:** remover o `.WriteTo.Console()` extra do bootstrap.

### [ ] D7 — Checks de serviços não implantados derrubam o `/health` (DECISÃO)

- `svc-zabbix-web`, `svc-evolution`, `svc-guacamole` (quando fora do profile)
  e `ai-runtime` (Degradado sem IA configurada) marcam o agregado como 503.
- Os checks **centrais** (postgresql, redis, masstransit, outbox) estão
  Healthy; `/health/ready` = 200.
- **Decisão:** em produção com todos os profiles no ar, os checks passam a
  fazer sentido; considerar severidade "Degraded" ≠ 503 para serviços
  opcionais, ou ligar checks por profile.

---

## 3. Itens pedidos pelo usuário (status)

| Item | Status |
| --- | --- |
| Login social (Google) habilitado | `[x]` F43-T004 no repo (env no overlay de produção + smoke 302/`error=google`); Fase 50 T002 cuida do botão oficial na tela. **Falta:** smoke do fluxo completo no ambiente de produção real (§7) |
| Melhor layout das páginas públicas/estáticas | `[ ]` **Fase 50–F6** (outra sessão, WIP no working tree) |
| Melhor logo | `[ ]` **Fase 50–F5** (upload de logo já existe em `/settings/company` na Fase 50) |
| Auditoria com descrição que facilite a compreensão | `[ ]` **Fase 50–F4** (Alert + Incident.RootCause + TextArea compartilhado) |

> As frentes F1–F7 da Fase 50 estão descritas em `PROJECT_STATE.md`
> (working tree desta sessão — não commitadas por este checklist).

---

## 4. Infraestrutura e operação

### 4.1 Banco de dados

- [x] Migrações = código (`__EFMigrationsHistory` 97/97; baseline AI-ENV-003,
      `scripts/db/check-migration-baseline.sh`)
- [x] CRUD validado contra o banco dev real (54 PASS)
- [x] **D1** — orçamento de conexões (corrigido 2026-11-08: pool=20/processo +
      `max_connections=200`; `tests/scripts/test-db-connection-budget.sh` 13/13;
      carga c=100 p95 211–251 ms, `psql` OK durante a carga — §2.1)
- [ ] Definir política de backup/restore testada (BackupSettings + BackupWorker
      existem — falta **executar um restore de verdade** e cronometrar RTO)

### 4.2 Saúde e monitoramento

- [!] **D2/D3** — probes do `/health` (corrigir)
- [x] `/health/ready` = 200 (checks centrais)
- [ ] Decidir severidade de serviços opcionais (**D7**)
- [ ] Alertas de infra (fora do escopo do `/health`) — definir canais via
      política de roteamento (F39)

### 4.3 Segredos e configuração

- [x] `.env` e `appsettings.Local.json` fora do git (`.gitignore` verificado;
      senha SMTP **não** aparece em nenhum commit)
- [x] `.env.production.example` sem segredos reais (F43-T004)
- [ ] **Rodar o grep de segredos no repo antes do go-live**
      (`git grep -iE 'password|secret|api[_-]?key' -- ':!*.example'`)
- [ ] Credenciais de produção **não** usar os defaults do compose
      (`infracontrol_dev_2026`, `CREDENTIAL_ENCRYPTION_KEY` default,
      `guacadmin_dev_password_2026`) — gerar valores novos por ambiente
- [ ] SMTP: confirmar App Password dedicada e rotacionável (a atual está no
      `.env` do dev em plaintext — ok para dev, **não** para prod)

### 4.4 Rede/edge

- [x] Rate limit Default 300/60 s + Login 5/60 s ativos e baratos (429 em <10 ms)
- [ ] Confirmar TLS/redirects do edge de produção (host `https_port_suffix`
      ≠ porta interna — origem do **D2**)
- [ ] Guacamole/remoto: profile `remote` ainda não implantado (pendência F34-T002)

---

## 5. IA (validado ao vivo 2026-11-08)

- [x] `GET /admin/ai-providers` — gemini (ativo, key set) + ollama (ativo)
- [x] `POST /admin/ai-providers/{id}/test-connection` — ollama 200 (13 ms),
      gemini 200 (380 ms)
- [x] `POST /admin/ai-providers/validate` — dry-run com fases em pt-BR
      (conectividade/autenticação/modelos/modelo); URL morta → falha graciosa;
      Gemini sem key → 400 `A API key é obrigatória…`
- [x] `POST /ai/chat` — 200 em 4,9 s (`gemini-3.8-flash`, sem fallback)
- [x] `POST /automation/recommendations` — 200 com recomendações
- [x] `GET /admin/ai-providers/{id}/models` — presente
- [ ] **Observação:** em teste anterior o circuit breaker do provider gemini
      abriu (`BrokenCircuitException`, chamada recusada a `host:8443`).
      Hoje fechado e estável; monitorar `ai-runtime` no `/health` e repetir
      o chat no ambiente de produção (sem `ollama-lb` local, o fallback muda)
- [ ] Definir política de fallback produção (gemini primário? ollama local?)
      — hoje capability map vem do seed/config

---

## 6. Performance e carga (medido)

| Cenário | Resultado | Limite registrado |
| --- | --- | --- |
| GETs em regime (p50/p95) | 6–15 ms / 7–19 ms | — |
| POST create | p50 ~10 ms | — |
| GET pós-create | overhead p95 +4 ms | **sem lentidão pós-criação** |
| ab c=5 n=100 | 427 req/s, p95 17 ms, 0 falhas | — |
| Estouro do rate limit | 429 em 5–7 ms | **300 req/60 s** (Default) |
| Node c=100 (endpoint com conexão nova) **antes do fix D1** | p95 24–28 s | **D1** — orçamento de pool |
| Node c=100 (endpoint com conexão do pool quente) | rps 1079, p95 127 ms | OK |
| Node c=100 pós-fix D1 (`/auth/mfa/status`, pool quente) | 1000×200, p50 63–67 ms, **p95 211–251 ms**, 1001–1176 rps | **D1 corrigido** (§2.1) |
| Boot | ~3 s | — |

- [x] Repetir a bateria de carga **após corrigir D1** e registrar novos números
      aqui (2026-11-08 — ver linha acima; teste de regressão:
      `bash tests/scripts/test-db-connection-budget.sh`)
- [ ] Definir SLO de latência por endpoint (hoje: p95 < 50 ms em regime é a
      referência usada desde a F46)

---

## 7. Qualidade e pendências conhecidas

- [ ] 2 testes `SkiaIncidentImageRendererTests` pré-existentes (unit
      3181/3183 na última medição) — corrigir ou isolar
- [ ] OOM `OperationsCenterPage.test.tsx` (dívida F44)
- [ ] F34-T000 — pendências do hospital (inventário `docs/62-F34-T000-…`)
- [ ] Produção sem profile `remote`/guacamole (F34-T002)
- [ ] NU1902/NU1903 (MessagePack) — sem upgrade ainda
- [ ] Smoke do login Google **no ambiente de produção** (§3)
- [ ] Restore de backup cronometrado (§4.1)

---

## 8. Como reproduzir a validação

```bash
# 1) stack de dev no ar (profile app)
docker compose up -d --build

# 2) login admin (gera cookie + XSRF) e bateria CRUD+latência
#    (NODE_TLS_REJECT_UNAUTHORIZED=0 por causa do TLS self-signed do dev)
NODE_TLS_REJECT_UNAUTHORIZED=0 node scripts/validation/smoke-crud-latency.mjs

# 3) overhead pós-criação
NODE_TLS_REJECT_UNAUTHORIZED=0 node scripts/validation/post-create-latency.mjs

# 4) carga (ab é do sistema; respeta/estoura o rate limit por design)
ab -k -n 100 -c 5 -H "Cookie: $COOKIE" \
   'https://localhost:8445/api/v1/inventory/locations?take=20'

# 5) regressão do orçamento de conexões (D1)
bash tests/scripts/test-db-connection-budget.sh
```

Notas:

- Os scripts fazem login com `admin@example.com` / `InfraControl_Dev_2026`
  (dev). Sobrescreva com `IC_EMAIL`/`IC_PASSWORD`.
- A bateria respeita o rate limit (gate de 240 req/janela) e **espera o
  `Retry-After`** em 429 — não tente contornar o limite para “medir melhor”;
  para isolar capacidade pura, suba `RateLimit__Rules__Default__PermitLimit`
  em um override temporário **e volte atrás** (como foi feito na medição da §6).
- Orfas de teste: os scripts limpam o que criam; se abortar no meio,
  `DELETE FROM locations WHERE name LIKE 'SMOKE%' OR name LIKE 'LAT-%';`

---

## 9. Go/no-go — mínimo antes de produção

1. **[x] D1** (orçamento de conexões) — corrigido e revalidado 2026-11-08 (§2.1).
2. **[!] D2** (`/health` do frontend) — corrigir; sem isso o orquestrador
   considera o stack doente.
3. **[!] D4** (500 em nome duplicado) — corrigir; é erro de usuário comum.
4. Segredos de produção gerados (§4.3) + grep de segredos.
5. Smoke do Google login no ambiente real (§3/§7).
6. Backup + restore executados uma vez de ponta a ponta (§4.1).
7. Itens de produto do usuário (Fase 50 F4/F5/F6 + F2 smoke) concluídos ou
   explicitamente adiados pelo dono.
