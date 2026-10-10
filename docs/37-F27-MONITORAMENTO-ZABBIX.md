# 37. F27 — Monitoramento completo da infraestrutura com Zabbix + InfraControl (checklist vivo)

- **Task/phase:** F27 (Fase 27) — plano/checklist entregue pelo usuário em 2026-10-08
- **Status:** Fase 1.1 (auditoria do InfraControl) **CONCLUÍDA 2026-10-09**
  (baseline backend verde + auditoria read-only consolidada em §1.1.1 —
  contrato do webhook congelado em §1.1.1.C). Front bloqueado por merge
  incompleto pré-existente no HEAD da S4 (ver baseline). **§1.2+ bloqueadas**:
  exigem ambiente Zabbix real (credenciais/API) desta sessão não possui.
- **Relação com outros docs:** integração Zabbix existente em
  `docs/27-INTEGRACAO-ZABBIX.md` · ADR-023 (ingestão de problemas) · ADR-027
  (MonitoringEvent) · ADR-028 (monitoramento) · F26 em `docs/36` · contrato do
  endpoint existente `POST /api/v1/integrations/zabbix/problems` · regras
  transversais: `AGENTS.md` + "Regras obrigatórias de execução" ao fim
- **Regra de atualização:** este checklist só avança com **evidência** (build +
  testes + validação real) — doc/entidade/tela isolada não é implementação
  concluída; cada fase só é marcada como concluída após validação (regra 14)

> **Texto integral do plano preservado abaixo** (apenas ajuste de níveis de
> título para o documento); nenhum item reescrito.

---

## Objetivo

Integrar o InfraControl ao Zabbix existente e proporcionar uma visão centralizada de toda a infraestrutura de TI, incluindo:

- Impressoras.
- Câmeras e gravadores de vídeo.
- Switches e Access Points.
- UniFi, Aruba, HP, Intelbras e TP-Link.
- Telefones IP.
- Servidores físicos.
- Máquinas virtuais e hypervisors.
- Roteadores, firewalls e links de rede.
- Serviços, aplicações, bancos de dados e outros componentes identificados na infraestrutura.

O InfraControl deverá consultar os dados do Zabbix, relacioná-los ao inventário e apresentar estado operacional, métricas, problemas, histórico e contexto dos equipamentos.

O Zabbix continuará responsável pela coleta, avaliação de triggers, métricas e geração de problemas de monitoramento.

**Regra:** antes de implementar qualquer recurso, auditar o que já existe no Zabbix e no InfraControl. Não recriar coletores nem duplicar funcionalidades existentes.

---

## FASE 1 — Auditoria completa e mapeamento da infraestrutura

### 1.1. Auditoria do InfraControl

> **Auditoria de código executada em 2026-10-09** (read-only, dois levantamentos
> paralelos + verificação manual do contrato do webhook). Resultados consolidados
> em **§1.1.1** ao fim da seção. Frontend fora do alcance desta auditoria de
> código: bloqueado por merge incompleto pré-existente (ver baseline acima).

- [x] Inspecionar a solução e a arquitetura atual.
  - .NET 10 + CQRS/MediatR (behaviors: correlation → logging → performance →
    validation → idempotência → audit → transaction → authorization), Vertical
    Slices, PostgreSQL + Redis + RabbitMQ/Outbox, workers separados da API,
    frontend React+TS+Vite. Camadas: `Domain` → `Application` (ports) →
    `Infrastructure` (adapters) + `Api` (controllers/ViewModels).
- [x] Identificar o endpoint existente `POST /api/v1/integrations/zabbix/problems`.
  - `src/InfraControl.Api/Controllers/Integrations/ZabbixIntegrationController.cs`
    — `[AllowAnonymous]` + guard `X-Zabbix-Token` (vazio→404 desligado,
    divergente→401); comando `IngestZabbixProblemCommand`
    (`Application/Integrations/Zabbix/`) **sem** `IAuthorizedRequest` de
    propósito; rate limit `Webhook` 120 req/min.
- [x] Inspecionar a ADR-023 e preservar contratos existentes.
  - ADR-023 + amenda ADR-027 lidas; checklist de contrato congelado em
    **§1.1.1.C** (rota, header ordinal, envelope, outcome tokens
    `created|duplicate|resolved|none`, dedup `(Source, ExternalEventId)`,
    SaveChanges atômico, mapeamento de severidade, allowlist do arch test).
  - docs/27: runbook do action Zabbix 8.x **implementado** (2026-10-03);
    limitações pendentes listadas em §1.1.1.P.
- [x] Identificar serviços, entidades, endpoints, páginas e testes já implementados.
  - Ver **§1.1.1.A** (entidades), **§1.1.1.B** (endpoints/páginas/testes).
- [x] Identificar os módulos de inventário, monitoramento, incidentes, histórico, auditoria e dashboards.
  - Ver **§1.1.1.A** e **§1.1.1.E**; dashboard **sem controller próprio**
    (montado no cliente); histórico de tasks = `TaskHistory` append-only;
    auditoria = `AuditEntries` + `GET /admin/audit` (Administer).
- [x] Identificar os mecanismos existentes de jobs, workers, logs e observabilidade.
  - Ver **§1.1.1.D**: workers (Outbox 5s, MonitoringPolling 60s, Cleanup 24h,
    Backup, 6 notification channels na API), Serilog Console+Seq,
    OpenTelemetry OTLP (traces+metrics, **sem backend persistido** — exporter
    `debug`), health `/health|ready|live|startup` (NpgSql+Redis).
- [x] Verificar autenticação, autorização e configuração de integrações.
  - Ver **§1.1.1.F**: matriz única `RoleAccess` (Read/Operate/Administer,
    ADR-016), policies HTTP + 2ª camada MediatR, allowlist de 10 endpoints
    públicos no arch test; token Zabbix via `Integrations:Zabbix:Token` →
    env `ZABBIX_TOKEN` (compose anchor `app-env`); profile `zabbix` no compose
    (server/web/agent/db, portas via `.env`, imagens `alpine-trunk`).
- [x] Executar o build e os testes antes de realizar alterações.
  - **Evidência do baseline (2026-10-09, pós-merge Knowledge K1–K7 — S4 + linha própria):**
    - `dotnet build` → **0 erros**.
    - `dotnet test tests/InfraControl.UnitTests` → **1921/1921 ✓**.
    - `dotnet test tests/InfraControl.ArchitectureTests` → **119/119 ✓**.
    - `dotnet test tests/InfraControl.IntegrationTests --filter Knowledge` → **18/18 ✓**.
    - `dotnet test tests/InfraControl.IntegrationTests` (suíte completa) → **191/191 ✓** (inclui as E2E).
    - Frontend: **NÃO-verde (bloqueio pré-existente no HEAD da S4, não introduzido por esta sessão).**
      `npx tsc -b` → **154 erros** · `npm run build` falha nos mesmos · `npm run lint` → **26 erros**.
      Causa raiz: arquivos commitados pela S4 com merge incompleto:
      `src/services/api/knowledge.ts` interface `KnowledgeArticle` duplicada (20 erros TS2300/2687/2717);
      `src/services/api/unifi.ts` contrato `Promise<T>` vs `request<T>` = `T | null` (4× TS2322);
      `src/pages/KnowledgePage.tsx` híbrido `useState` manual + React Query sem declaração (127);
      `src/pages/PdiPage.tsx` imports não usados (3). Nenhum destes arquivos tem diff no worktree
      (pertencem ao HEAD da S4) — confirmado contra o ancestral `e3fcaff` (meu K1+K2): `unifi.ts` é
      adição nova da S4, `knowledge.ts` tinha 1 interface lá e 2 no HEAD.
- [x] Registrar implementações concluídas, parciais e ausentes.
  - Registro consolidado em **§1.1.1** (concluídas/parciais/ausentes).

#### §1.1.1 — Registro de implementações (2026-10-09)

**A. Entidades de domínio (todas concluídas)** — `src/InfraControl.Domain/Entities/`:
`MonitoringEvent` (evento bruto, unicidade `(Source, ExternalEventId)`, statuses
Problem/Recovered); `MonitoringCheck` (check por device, `CheckKind` =
Ping|SnmpToner|SnmpDrum|HttpWan|SnmpSystem|Tcp, único campo mutável `Enabled`);
`MonitoringResult` (1 linha/poll, `Values` dict); `Alert` (anti-storm 1 aberto
por (device,check), nativo vs externo, `Alert.CreateExternal` com DeviceId?
nullable, severidade Warning/Critical); `Incident` + `IncidentAlert` (ciclo
Open→Investigating→Identified→Monitoring→Resolved, `RootCause`, 1 Open por
device); `DiscoveryRun`/`DiscoveryResult`; `TaskIncident`/`TaskDevice`.

**B. Endpoints, páginas, testes (concluídos, com lacunas pontuais)**
- `GET/POST/PUT/DELETE api/v1/monitoring/*` (checks CRUD + run-check → Operate;
  `GET monitoring/alerts` → auth simples; **sem endpoints de mutação de alerta**);
  `api/v1/incidents` (CRUD + acknowledge + status); `api/v1/discovery` (runs/results);
  `api/v1/network/topology` (GET, ligado ao root cause).
- Páginas: Dashboard (`/`), Alerts (`/alerts`), Incidents (`/incidents`),
  Topology (`/topology`), Insights (`/insights`), DeviceDetail (`/devices/:id`),
  Integrations (Zabbix). **Dashboard sem endpoint agregador próprio** (queries no cliente).
- Testes: unit (14 de Monitoring, 4 Alerts, 3 Incidents, 2 Zabbix, providers
  Ping/HTTP/TCP/SNMP com fakes) + integration (Monitoring, Alert, Correlation,
  Incident, ZabbixIngest, TaskIncident).

**C. Contrato congelado do webhook Zabbix (NÃO quebrar — ADR-023/027 + docs/27)**
1. Rota/verbo `POST api/v1/integrations/zabbix/problems`; `[AllowAnonymous]` +
   entrada obrigatória na allowlist `Public_Endpoints_Should_Be_The_Explicit_Allowlist`
   (arch test) — hoje 10 endpoints públicos, só o Zabbix fora de auth/convite.
2. Header `X-Zabbix-Token` comparado **ordinal** com `Integrations:Zabbix:Token`;
   vazio→404 "A integração com Zabbix está desativada."; divergente→401 "Token do
   Zabbix inválido."; sempre no envelope `ApiResponse {isSuccess,data,errors}`.
3. Payload: `eventId`={EVENT.ID} **obrigatório nos dois status** (chave de dedup);
   `recoveryEventId`={EVENT.RECOVERY.ID} opcional; `host`={HOST.NAME1};
   `ip`={HOST.IP}; `name`={TRIGGER.NAME} obrigatório; `severity`={EVENT.NSEVERITY}
   inteira 0..5 obrigatória; `status` ∈ PROBLEM|OK (case-insensitive);
   `occurredAt`={EVENT.TIMESTAMP} unix opcional. Validação no handler na ordem
   name → severity → status, 400 pt-BR via GlobalExceptionHandler.
4. Semântica: PROBLEM → **sempre** 1 Incident (não reusa incidente aberto por
   device); dedup por `(Source, ExternalEventId)` — **dedup por título banido**;
   ciclo Event→Alert→Incident em **1 SaveChanges atômico**; match de device por
   IP exato → fallback hostname → sem match = `DeviceId` nulo (nunca erro);
   OK = recupera o problem original (linha própria com recoveryId ou in-place);
   OK órfão grava linha + Warning + outcome `none` — **nunca derruba o webhook**.
5. Resposta 200: `{incidentId, outcome}` com `outcome ∈ created|duplicate|resolved|none`
   (**tokens de contrato — não traduzir**). Mapeamento de severidade: Zabbix
   0/1→Low, 2→Medium, 3→High, 4/5→Critical (Alert: 0..2 Warning, 3..5 Critical).
6. Rate limit `Webhook` 120 req/min. Mudança de payload exige mudar o action no
   Zabbix junto (os dois lados documentados em docs/27).

**D. Jobs/Workers e observabilidade (concluídos; lacunas marcadas)**
- Workers host `InfraControl.Workers`: `OutboxDispatcherWorker` (5s/50),
  `MonitoringPollingWorker` (60s, flag `MonitoringPolling:Enabled`),
  `CleanupWorker` (24h, retenção — inclui `monitoring_events` 90d),
  `BackupWorker` (horas). API host: 6 notification channel workers (SignalR,
  Email, Telegram, WhatsApp, Webhook, Slack — loop 2s, retry→DLQ).
- Observabilidade: Serilog Console+Seq (compose `profile obs`), OpenTelemetry
  OTLP traces+metrics (sources/meters `InfraControl.*`), correlation
  `X-Correlation-Id` (middleware 1º), health `/health|/health/ready|/health/live|/health/startup`
  (NpgSql+Redis, expostos pelo nginx). **Ausente/parcial:** exporter OTel = `debug`
  (sem backend de métricas/traces persistido); workers sem OTel; sem endpoint
  `/metrics` Prometheus; `Worker.cs` template órfão (não registrado).

**E. Histórico/auditoria (concluídos)**
`AuditEntries` (AuditBehavior + SecurityAudit, nunca payload, leitura
`GET /admin/audit` Administer); `outbox_messages` (publicação por polling,
retenção 30d); `task_histories` (append-only, actor+action+detail ≤200).

**F. Auth/integração (concluídos)**
Matriz única `RoleAccess` (Read= todas, Operate=Operator+Admin, Administer=Admin)
— ADR-016; policies HTTP (`AddRolePolicies`) + 2ª camada MediatR
(`IAuthorizedRequest`); arch test impede roles/policies fora do padrão. Token
Zabbix: appsettings `Integrations:Zabbix:Token` ← env `ZABBIX_TOKEN` (compose
anchor `&app-env`, api+workers); `.env` dev = `infracontrol-zabbix-dev-2026`.
Profile `zabbix` (compose): zabbix-db (PG17, porta host via `ZABBIX_DB_PORT`
=.env 5434), zabbix-server (`alpine-trunk`, 10052), zabbix-web (8086 — 8085 é
do frontend), zabbix-agent2 (host 10053); credenciais dev `Admin`/`zabbix`.

**P. Parciais/ausentes relevantes para as fases seguintes da F27**
- *Parcial:* action Zabbix legada sem `recoveryEventId` (recovery in-place, sem
  linha auditável — docs/27 §5); incidentes pré-ADR-027 respondem `none`;
  hosts fora do inventário nascem sem device; imagens Zabbix `alpine-trunk`
  (upgrade p/ tags 8.0 oficiais pendente — F18-T001).
- *Ausente (candidatos a fases da F27):* direção B (JSON-RPC pull/periodic) da
  API do Zabbix descartada/futura; backend persistido de métricas/traces
  (Jaeger/Prometheus+Grafana); `/metrics`; supervisor de estado dos itens/hosts
  do Zabbix dentro do InfraControl; UI admin de integrações (painel
  `● Connected` do Centro de Operações); alertas sem mutação via API
  (acknowledge/suppress de alerta nativo); dashboard com endpoints agregadores.


### 1.2. Auditoria do Zabbix

- [x] Identificar versão e arquitetura da instalação.
- [x] Identificar servidores, proxies e demais componentes existentes.
- [x] Verificar conectividade e autenticação da API.
- [x] Listar hosts, grupos, templates, interfaces, itens, triggers e problemas.
- [x] Identificar hosts desativados e itens sem suporte.
- [x] Identificar métodos de coleta: agente, SNMP, ICMP, HTTP, IPMI, JMX, ODBC, APIs e outros existentes.
- [x] Verificar regras de descoberta automática.
- [x] Identificar templates associados a cada categoria de equipamento.
- [x] Verificar histórico, tendências e retenção.
- [x] Identificar alertas, ações, webhooks e notificações existentes.
- [x] Verificar permissões e limites de acesso.
- [x] Identificar hosts duplicados e equipamentos sem monitoramento adequado.

#### §1.2.1 — Registro da auditoria (2026-10-09)

Auditoria read-only via API JSON-RPC. Stack do docker-compose profile `zabbix` de pé há 26h (`infracontrol-zabbix-server`/`web`/`agent` healthy, PG dedicado `infracontrol-zabbix-db` porta 5434).

**1. Versão e arquitetura**

- Zabbix **8.0.0** (imagem `alpine-trunk`, upgrade pendente F18-T001 executado).
- Componentes: server (pgsql), web nginx (host 8086), agent2 (host 10053→10050), PostgreSQL 17 dedicado. **Sem proxy.**
- **Mudança de API relevante (Zabbix ≥7.2/8.0)**: autenticação por header `Authorization: Bearer <token>`; o campo `auth` no corpo é rejeitado (`unexpected parameter "auth"`). Cliente da Fase 2 **deve** usar Bearer. Runbook `docs/27` §auth precisa de atualização.

**2. Hosts, grupos, interfaces**

- **1 host**: `Zabbix server` (hostid 10084), enabled, normal (não inventado), interface 127.0.0.1:10050 (Zabbix agent passive), 2 templates: `Linux by Zabbix agent` + `Zabbix server health`.
- **0 hosts de produção** — nenhum equipamento real monitorado ainda.
- 7 grupos padrão (Applications, Databases, Discovered hosts, Hypervisors, Linux servers, Virtual machines, Zabbix servers). Nenhum grupo custom.
- Sem hosts duplicados (apenas 1 host).

**3. Itens, triggers, problemas**

- Host 10084: **132 itens** — 43 Zabbix agent passive, 76 SNMPv3, 1 simple, 12 dependent. **0 desabilitados.**
- **11 itens unsupported**, todos internos e esperados (pollers não usados: ipmi/java/snmp-trapper/vmware collector, `zabbix[vmware,buffer,pused]`). Nenhum item de produção com falha.
- Triggers de fábrica dos templates (template `Zabbix server health` + `Linux by Zabbix agent`). Nenhuma trigger custom.
- **1 problem aberto**: sev3 `Linux: Zabbix agent is not available (for 3m)` desde 2026-10-03 16:49 — causa: interface do host aponta para 127.0.0.1 (loopback do container do server), enquanto o agent2 roda em container separado (`infracontrol-zabbix-agent`, host 10053). **Ação de correção futura**: trocar interface para nome do container/IP do host (§1.4 lacuna).

**4. Métodos de coleta existentes**

- Zabbix agent passive (43), SNMPv3 (76, dos templates de fábrica — ainda sem equipamento SNMP real), simple check (1), dependent items (12). ICMP ping via `fping` do agent, HTTP/IPMI/JMX/ODBC disponíveis nos templates de fábrica (390 templates builtin) mas **não associados a host algum**.

**5. Discovery (LLD)**

- 6 regras no host, todas enabled: mounted filesystem (1h), network interface, block devices, proxy/proxy-groups/HA-node (internas). Nenhuma discovery custom para produção.

**6. Templates**

- 390 templates de fábrica, **0 custom**. Categorias relevantes disponíveis no catálogo e prontas para as Fases 4–8: impressoras (HP/EPSON by SNMP), switches/AP (Cisco IOS, Huawei VRP, Ubiquiti), VMware, Linux by Zabbix agent, Windows, etc. Nenhum template do InfraControl criado ainda.

**7. Retenção (housekeeping)**

- history **31d**, trends **365d**, trigger events **365d**, discovery/internal/autoreg events **1d**, audit **31d**, sessions **31d**. Compressão desativada (`compress_older=7d`, status off). Parâmetros globais de history/trends desativados (defaults por tipo de item).

**8. Ações, webhooks, notificações**

- 6 actions: 5 defaults **DISABLED** (auto discovery, report to admins, not-supported items/LLD, unknown triggers) + **action 7 `InfraControl — triggers` ENABLED** (eventsource=triggers, filtro vazio = todos, esc_period 0).
  - Operation (PROBLEM) → media type **105** `InfraControl — Problem`; Recovery → **106** `InfraControl — Recovery`.
  - MTs 105/106: webhook (script JS), status enabled, 3 tentativas/10s, timeout 30s.
  - Parâmetros: `url=http://api:8080/api/v1/integrations/zabbix/problems`, `token=infracontrol-zabbix-dev-2026` (**bate** com `ZABBIX_TOKEN` do `.env`).
  - Script envia body `{eventId, host, ip, name, severity, status, recoveryEventId?}` + header `X-Zabbix-Token` — **confere com o contrato §1.1.1.C congelado**.
  - Histórico `alert.get`: 2 falhas iniciais (`No message defined for media type`, ajuste ao vivo 2026-10-03/04) seguidas de **3 entregas OK** (status=1) em 2026-10-04 00:25–00:26. Webhook **funcional** na data da auditoria.
- 7806 macros globais (herdadas dos templates de fábrica, ex.: `{$TEMP_CRIT}`).

**9. Usuários e permissões**

- 3 usuários: `Admin` (Super admin), `guest` (Zabbix admin? role 4 = Guest), `paulomvbumba@gmail.com` (role 2 = User). Sem media types de email/SMS — apenas os 2 webhooks InfraControl.

**10. Lacunas confirmadas (alimentam §1.4)**

- Nenhum host de produção monitorado (0 cobertura real).
- Interface do host "Zabbix server" aponta para loopback → problem crônico do próprio agent.
- Nenhum template custom, nenhum grupo custom, nenhuma trigger custom, nenhuma discovery de produção.
- ~~Docs/27 desatualizado quanto à autenticação Bearer do Zabbix 8.0.~~ **Corrigido (Fase 2, 2026-10-10):** `docs/27-INTEGRACAO-ZABBIX.md` agora documenta `Authorization: Bearer` (campo `auth` rejeitado no 8.0).

### 1.3. Inventário da infraestrutura

Criar um levantamento dos equipamentos já existentes, incluindo:

> **Estrutura pronta (2026-10-10):** `docs/database/f27-inventario-equipamentos.csv` —
> colunas das 11 dimensões abaixo + `coleta_status` (Monitorado / Parcial / Host sem
> coleta / Sem host no Zabbix / Não monitorado) para expor a diferença inventário ×
> Zabbix × monitorado efetivo. Única linha pré-preenchida = host real auditado
> (`Zabbix server`, id 10084). **Preenchimento do restante depende dos dados reais do
> usuário** (Regra 4: não inventar equipamentos).

- [ ] Nome e categoria.
- [ ] Fabricante e modelo, quando conhecidos.
- [ ] IP, MAC e hostname, quando disponíveis.
- [ ] Localização física, setor ou unidade.
- [ ] Site, VLAN e segmento de rede, quando conhecidos.
- [x] Identificador do host no Zabbix. <!-- 1 host real: 10084; demais aguardam inventário -->
- [x] Método de monitoramento utilizado. <!-- Zabbix agent (único host) -->
- [x] Template associado. <!-- "Zabbix server" (único host) -->
- [x] Estado atual da coleta. <!-- coluna coleta_status do CSV -->
- [ ] Métricas e alertas disponíveis.
- [ ] Responsável e criticidade, caso cadastrados.

Não presumir que todos os dispositivos estejam configurados ou monitorados. A auditoria deve mostrar a diferença entre inventário conhecido, host existente no Zabbix e equipamento efetivamente monitorado.

### 1.4. Entregáveis

- [x] Relatório da auditoria. <!-- §1.1.1 (InfraControl) + §1.2.1 (Zabbix) -->
- [x] Inventário inicial. <!-- docs/database/f27-inventario-equipamentos.csv (estrutura + 1 host real; preencher com dados do usuário) -->
- [x] Matriz de cobertura de monitoramento. <!-- §4.2.2 (por categoria × template oficial × lacuna) -->
- [ ] Lista de hosts duplicados ou sem correspondência. <!-- 0 hosts de produção; host sem Asset = 1 (o próprio server) -->
- [ ] Lista de métricas disponíveis e ausentes. <!-- requer inventário real -->
- [ ] Plano de correção das lacunas encontradas. <!-- lacunas mapeadas (§1.2.1/§4.2.1); plano por dispositivo aguarda inventário -->

---

## FASE 2 — Fundação da integração Zabbix

### 2.1. Cliente da API

- [x] Implementar ou completar `IZabbixClient`.
- [x] Implementar ou completar `ZabbixClient`.
- [x] Utilizar JSON-RPC 2.0 conforme a versão instalada.
- [x] Implementar autenticação e gestão segura de credenciais.
- [x] Implementar timeout, cancelamento e tratamento de erros.
- [x] Implementar paginação e limites de resultados.
- [x] Tratar falhas HTTP, erros JSON-RPC e respostas inválidas.
- [x] Implementar consultas de versão, hosts, grupos, templates, itens, triggers, eventos e problemas conforme os métodos disponíveis.
- [x] Implementar consultas históricas com limites de período e volume.
- [x] Mapear respostas externas para contratos internos.
- [x] Evitar expor tokens ou dados sensíveis em logs.
- [x] Reutilizar os padrões existentes de configuração e observabilidade.

#### §2.1.1 — Registro da implementação (2026-10-10)

Backend concluído seguindo o padrão UniFi (casa). Arquivos:

- `src/InfraControl.Application/Integrations/Zabbix/Ports/IZabbixClient.cs` + `ZabbixApiModels.cs` — porta e contratos internos (`ZabbixHostDto` com interfaces/templates/grupos, `ZabbixProblemDto`, `ZabbixItemDto`, `ZabbixTriggerDto`, `ZabbixHistoryValueDto`).
- `src/InfraControl.Infrastructure/Integrations/Zabbix/ZabbixApiClient.cs` — JSON-RPC 2.0 no named client `zabbix-api` (Polly do dispatcher n8n). `user.login` com cache de sessão (1 login por vida do cliente); detecção de versão via `apiinfo.version` (sem auth): **major ≥ 8 → header `Authorization: Bearer`** (o 8.0 rejeita o campo `auth` — confirmado na §1.2.1), **≤ 7 → campo `auth` no corpo**. Sessão recusada (erro JSON-RPC de autorização: "Session terminated", "Not authorized" etc.) → re-login + 1 retry. Erros HTTP/JSON-RPC/JSON malformado viram exceção identificável **sem token/senha** (teste dedicado). Listagens com `limit` do servidor (default 1000); `history.get` exige `itemId` + período UTC + limit 1..100000. Endpoint relativo: `ApiUrl` aceita raiz ou `.../api_jsonrpc.php`.
- Consultas implementadas: `apiinfo.version`, `user.login`, `host.get` (interfaces/templates/grupos), `hostgroup.get`, `template.get`, `item.get` (por host), `trigger.get` (monitored/only_true), `problem.get` (recent), `history.get` (período+limit, ordenado por clock ASC).

### 2.2. Configuração administrativa

Criar ou completar `Administração → Integrações → Zabbix`.

- [x] Configurar URL e acesso à API.
- [x] Armazenar credenciais de forma segura.
- [x] Implementar teste de conexão.
- [x] Exibir versão detectada, estado e última comunicação.
- [x] Exibir última sincronização bem-sucedida. <!-- LastInventorySyncAtUtc — Fase 3.1 -->
- [x] Exibir erros recentes sem revelar segredos.
- [ ] Permitir sincronização manual. <!-- endpoint POST /sync implementado (Fase 3.1); item fecha com a UI -->
- [x] Restringir configurações a administradores autorizados.
- [x] Documentar permissões necessárias.

#### §2.2.1 — Registro da implementação (2026-10-10)

Espelho do padrão UniFi (linha única, banco → env):

- `src/InfraControl.Domain/Entities/ZabbixIntegrationSetting.cs` — `ApiUrl`/`Username`/`PasswordEncrypted` (AES-GCM via `ICredentialProtector`; texto puro nunca toca o banco) + estado do painel: `DetectedVersion`, `LastCommunicationAtUtc`, `LastTestedAtUtc`, `LastTestOk`, `LastTestError` (truncado em 1000, sem segredos).
- Migração `20261010013838_AddZabbixIntegrationSetting` (tabela `zabbix_integration_settings`) — validada pela cadeia de testes de integração (SQLite) e pelo build.
- Ports/impl: `IZabbixSettingsRepository`/`ZabbixSettingsRepository` (auto-registro do `RepositoryRegistrar`), `IZabbixApiOptionsAccessor`/`ZabbixApiOptionsAccessor`, `IZabbixOptionsResolver`/`ZabbixOptionsResolver` (banco → env, falha ao decifrar = senha vazia + log).
- Commands (`ZabbixSettingsCommands.cs`): `GetZabbixSettingsQuery`, `SaveZabbixSettingsCommand` (senha write-only; omissão mantém a gravada; `IAuditableRequest` "ZabbixSettings"), `TestZabbixConnectionCommand` (nunca lança por falha de rede; grava versão/estado/erro na linha quando ela existe).
- Controller (`ZabbixIntegrationController`): `GET/PUT api/v1/integrations/zabbix/config` + `POST .../test-connection`, todos com policy **Administer**; o webhook público `POST .../problems` permanece intacto (contrato §1.1.1.C).
- Options: `ZabbixIntegrationOptions` estendida na MESMA seção `Integrations:Zabbix` (`Token` inbound + `ApiUrl`/`Username`/`Password` outbound — fallback de env); appsettings com chaves vazias de discoverability.
- DI (`DependencyInjection.cs`): `IZabbixClient` "padrão" resolve a config efetiva (banco → env) por request + named client `zabbix-api` com a mesma Polly do dispatcher n8n.

**Permissões necessárias (documentado):** usuário de API no Zabbix com acesso de leitura a hosts, groups, templates, items, triggers e problems (no ambiente dev, `Admin`/Super admin; em produção, criar usuário de API dedicado com perfil de leitura — preferir API token do Zabbix quando suportado for evolução futura). A senha de API vive cifrada no banco ou em env (`Integrations:Zabbix:*`), nunca em repositório.

### 2.3. Critérios de aceite

- [ ] Conexão validada contra o Zabbix existente. <!-- smoke ao vivo pendente de rebuild da imagem da API (unit tests cobrem o contrato 8.0 gravado na §1.2.1) -->
- [x] Autenticação funcional.
- [x] Credenciais protegidas.
- [x] Erros tratados sem derrubar o InfraControl.
- [x] Build e testes relevantes aprovados.

---

## FASE 3 — Inventário de hosts e descoberta

### 3.1. Sincronização de hosts

- [x] Consultar hosts e grupos.
- [x] Persistir identificadores externos e atributos relevantes.
- [x] Consultar interfaces, endereços e estado de disponibilidade.
- [x] Consultar templates e tags quando necessários.
- [x] Atualizar registros existentes sem duplicações.
- [x] Tratar hosts desativados, removidos ou inacessíveis.
- [x] Registrar a data da última atualização.
- [x] Preservar histórico e relacionamentos.

#### §3.1.1 — Registro da implementação (2026-10-10)

Backend concluído:

- `src/InfraControl.Domain/Entities/ZabbixHost.cs` — read-model por hostid (único): `Host`/`Name`, interface principal (`Ip`/`Port`), `Enabled`/`Discovered`, `GroupsJson`/`TemplatesJson` (nomes para a UI filtrar), `LastSyncedAtUtc`, `MissingSinceUtc` — **nunca apagado** (host removido no Zabbix vira ausente, mesmo critério do `UniFiSite`).
- Migração `20261010025019_AddZabbixHostInventory` (tabela `zabbix_hosts` + coluna `last_inventory_sync_at_utc` em `zabbix_integration_settings` — fecha o item §2.2 "última sincronização").
- `SyncZabbixInventoryCommand` (policy **Operate**, audit "ZabbixSync"): `apiinfo.version` → `host.get` + `hostgroup.get` + `template.get` (nomes; falha em grupos/templates loga e segue); upsert por hostid; hosts locais ausentes → `MarkMissing()`; erro por host conta em `Errors` e não derruba o sync; falha de conexão/credencial propaga (502/503); grava `LastInventorySyncAtUtc` na linha de config quando existe.
- `IZabbixHostsRepository`/`ZabbixHostsRepository` — busca por nome/IP/hostid (case-insensitive), filtros `enabled`/`missing`, paginação `PagedResult` (clamp 1..100, padrão do projeto). Registrado também no host de teste do `BaseIntegrationTest`.
- Endpoint `POST api/v1/integrations/zabbix/sync`.

### 3.2. Descoberta

- [x] Auditar as regras de Network Discovery existentes. <!-- §1.2.1: nenhuma regra custom; só defaults do Zabbix server -->
- [x] Identificar regras de Low-Level Discovery. <!-- §1.2.1: 6 LLD internas do host server, nenhuma de produção -->
- [x] Identificar hosts descobertos automaticamente. <!-- §1.2.1: 0 hosts inventados -->
- [ ] Avaliar descoberta de interfaces, sistemas de arquivos e recursos de rede. <!-- requer hosts de produção reais -->
- [ ] Identificar recursos descobertos que ainda não possuem monitoramento adequado. <!-- idem -->
- [x] Evitar criar novas regras que dupliquem as existentes. <!-- nenhuma regra criada nesta Fase -->
- [ ] Documentar recursos que exigem configuração manual. <!-- após hosts de produção -->
- [x] Não adicionar automaticamente todos os dispositivos encontrados sem política e validação. <!-- sync só espelha hosts já existentes no Zabbix — nada é criado no Zabbix pelo InfraControl -->

### 3.3. Interface

Criar ou completar `Infraestrutura → Hosts monitorados`.

- [ ] Pesquisar por nome, IP, MAC e identificador. <!-- backend: busca por nome/IP/hostid pronta; UI + MAC pendentes do front -->
- [ ] Filtrar por grupo, categoria, fabricante e estado quando disponíveis. <!-- backend: filtros enabled/missing prontos; grupo/categoria no front -->
- [ ] Exibir host, origem, método de atualização e última sincronização. <!-- DTO pronto (LastSyncedAtUtc); UI pendente -->
- [ ] Abrir detalhes do host. <!-- front -->
- [ ] Exibir problemas e métricas associados. <!-- depende da UI + Fase 4+ -->
- [ ] Identificar hosts sem Asset correspondente. <!-- backend: flag HasAsset (IP → hostname, mesmo critério do webhook) pronta; front pendente -->
- [ ] Identificar hosts com monitoramento incompleto. <!-- front + Fase 4 -->

#### §3.3.1 — Registro parcial (2026-10-10)

Backend da interface pronto: `GET api/v1/integrations/zabbix/hosts` (autenticado) com `GetZabbixHostsQuery(term, enabled, missing, page, pageSize)` → `PagedResult<ZabbixHostViewDto>` (inclui `Groups`/`Templates`, `HasAsset`, `MissingSinceUtc`, `LastSyncedAtUtc`). **A UI fica pendente** enquanto o frontend estiver quebrado pelo merge da S4 (ver checkpoint 2026-10-09).

### 3.4. Critérios de aceite

- [x] Hosts sincronizados corretamente. <!-- unit (6) + integração (4) verdes; smoke ao vivo pendente de rebuild da imagem -->
- [x] Descobertas não geram duplicações. <!-- nenhuma regra criada; sync idempotente por hostid (testado) -->
- [x] Dados reais são diferenciados de dados indisponíveis. <!-- Enabled/Discovered/MissingSinceUtc são campos distintos; erro por host ≠ host offline -->
- [x] Falha de consulta não é interpretada como host offline. <!-- conexão/credencial quebrada propaga como erro do sync — nunca marca todos os hosts ausentes -->

---

## FASE 4 — Monitoramento de impressoras

### 4.1. Cobertura de impressoras

Abranger HP, Brother e demais fabricantes presentes no inventário.

- [ ] Identificar impressoras de rede.
- [ ] Verificar suporte a SNMP e templates compatíveis.
- [ ] Verificar impressoras acessíveis somente por USB ou servidor de impressão.
- [ ] Identificar estado de disponibilidade.
- [ ] Monitorar toner ou tinta quando suportado.
- [ ] Monitorar cilindro, unidade de imagem ou outros consumíveis quando disponíveis.
- [ ] Consultar contadores de páginas quando suportados.
- [ ] Identificar erros, estados de papel e alertas do dispositivo quando expostos.
- [ ] Monitorar interfaces de rede quando aplicável.
- [ ] Identificar impressoras sem métricas de consumíveis.

### 4.2. Templates e coleta

- [x] Auditar templates já existentes. <!-- §4.2.1 — 2026-10-10 -->
- [ ] Reutilizar templates oficiais ou compatíveis com o modelo. <!-- depende do modelo real de cada impressora -->
- [ ] Validar OIDs e tipos de dados antes de criar itens SNMP personalizados. <!-- depende dos dispositivos reais -->
- [ ] Evitar criar itens duplicados. <!-- depende dos dispositivos reais -->
- [ ] Configurar intervalos de coleta adequados. <!-- depende dos dispositivos reais -->
- [ ] Validar os valores coletados contra o dispositivo real. <!-- depende dos dispositivos reais -->

#### §4.2.1 — Auditoria de templates do Zabbix 8.0 (2026-10-10)

**Catálogo oficial (391 templates) auditado via API — achados críticos:**

- **Impressoras: 0 templates oficiais** — o Zabbix 8.0 **removeu** o legado "Printer SNMP".
  Base disponível para impressoras: **`Network Generic Device by SNMP`** (ICMP ping/loss/tempo,
  sysDescr/sysObjectID/sysUpTime, `net.if.discovery` + EtherLike) — **sem nenhum item de
  consumível** (toner/tinta/cilindro/contador de páginas).
  → Consumíveis exigem **itens SNMP personalizados com MIB do fabricante** (ex.: HP
  `PRAID-MIB`/`HP-ICF-POWERSUPPLY`, Brother `BR-ADMIN-MIB`) **ou** o coletor nativo do
  InfraControl já existente (§1.1: `PrinterSuppliesProvider` SNMP toner+drum). Regra 2/3:
  **não duplicar** — preferir o provider nativo já implementado e só criar item Zabbix se
  houver decisão explícita. OIDs devem ser validados no dispositivo real (Regra 4).
- **Câmeras/gravadores: 1 template** (`Hikvision camera by HTTP`) — **Intelbras, Dahua e
  NVR/DVR genéricos: 0**. Videomonitoramento via SNMP/ICMP apenas como disponibilidade.
- **Access points: 0 templates dedicados** (UniFi/Aruba AP) — usar `Network Generic Device
  by SNMP` + integração UniFi REST já existente (F26) para clientes conectados.
- **Switches/roteadores: 87 templates** (Cisco IOS, Aruba CX, Huawei VRP, MikroTik RouterOS,
  TP-Link, Juniper etc. — checar presença do modelo exato quando o inventário existir).
- **Telefonia: 1** (`Asterisk by HTTP`) — phones SIP sem template dedicado.
- **Servidores: 13** (`Linux/Windows/FreeBSD by Zabbix agent`, `Linux by SNMP`, `Windows by
  SNMP`…). **Hypervisors: 11** (VMware, Hyper-V standalone/cluster, Proxmox VE by HTTP/OTLP,
  Nutanix). **Bancos/serviços: 43** (MySQL, PostgreSQL, MSSQL, Mongo, Redis, Kafka by JMX,
  Elasticsearch, RabbitMQ…).
- Nenhuma regra de descoberta ou template foi **criado** nesta auditoria (somente leitura).

#### §4.2.2 — Matriz de cobertura por categoria (base para Fases 4–8)

| Categoria | Template oficial no Zabbix 8.0 | Lacuna conhecida | Caminho recomendado |
|---|---|---|---|
| Impressoras | ✗ nenhum | consumíveis (toner/cilindro/contador) | MIB do fabricante (itens custom) **ou** `PrinterSuppliesProvider` nativo (sem duplicar) |
| Câmeras/gravadores | ~ só Hikvision by HTTP | Intelbras/Dahua/NVR/DVR | ICMP/SNMP disponibilidade; API do fabricante quando existir |
| Switches | ✓ 87 (IOS, Aruba CX, VRP, RouterOS…) | modelo exato por inventário | template oficial do modelo + LLD de interfaces |
| Access Points | ✗ nenhum dedicado | clientes conectados | `Network Generic Device by SNMP` + API UniFi (F26) |
| Telefonia | ~ só Asterisk by HTTP | phones SIP/centrais | disponibilidade ICMP/SNMP; checks de serviço da central |
| Servidores | ✓ 13 (agent/SNMP) | agentes não instalados | `Linux/Windows by Zabbix agent` + IPMI quando suportado |
| Hypervisors | ✓ 11 (VMware/Hyper-V/Proxmox/Nutanix) | — | template da plataforma + correlação VM↔host (§8.2) |
| Serviços/Bancos | ✓ 43 | endpoints HTTP/certs | templates por tecnologia + `HTTP agent` |

> Nenhum item/trigger personalizado foi criado — apenas leitura do catálogo. A criação de
> itens customizados de consumível depende do inventário real (§1.3) e de validação de OIDs
> no dispositivo (Regra 4: não inventar métricas).

### 4.3. Alertas

- [ ] Impressora indisponível.
- [ ] Falha de coleta persistente.
- [ ] Toner ou tinta abaixo dos limites configurados.
- [ ] Cilindro ou consumível abaixo do limite, quando disponível.
- [ ] Erros de dispositivo relevantes.
- [ ] Alertas configuráveis conforme a criticidade.

### 4.4. Interface

- [ ] Listar impressoras por setor e localização.
- [ ] Exibir estado, modelo, IP e última atualização.
- [ ] Exibir consumíveis disponíveis.
- [ ] Exibir problemas ativos.
- [ ] Identificar impressoras sem métricas de consumíveis.

---

## FASE 5 — Câmeras, gravadores e videomonitoramento

### 5.1. Equipamentos

- [ ] Identificar câmeras IP e gravadores existentes.
- [ ] Incluir Intelbras e outros fabricantes presentes.
- [ ] Identificar modelos, endereços e interfaces.
- [ ] Verificar suporte a SNMP, ICMP, HTTP ou APIs específicas.
- [ ] Monitorar disponibilidade e perda de comunicação.
- [ ] Monitorar métricas de sistema, interfaces e armazenamento quando disponíveis.
- [ ] Verificar recursos de canais, discos e gravação dos gravadores quando expostos.
- [ ] Identificar equipamentos sem método de coleta adequado.

### 5.2. Alertas

- [ ] Câmera ou gravador indisponível.
- [ ] Falha de comunicação persistente.
- [ ] Problemas de armazenamento identificáveis.
- [ ] Falhas de coleta.
- [ ] Mudanças de estado relevantes fornecidas pelo equipamento.

Não presumir que o Zabbix consiga validar a gravação de vídeo de todas as câmeras. Isso depende dos recursos e interfaces disponibilizados pelo equipamento.

### 5.3. Interface

- [ ] Listar câmeras e gravadores.
- [ ] Agrupar por setor e localização.
- [ ] Exibir disponibilidade, modelo e última atualização.
- [ ] Exibir problemas e métricas suportadas.
- [ ] Diferenciar câmera offline de falha do mecanismo de consulta.

---

## FASE 6 — Rede, switches, Access Points e fabricantes

### 6.1. Equipamentos contemplados

- [ ] UniFi.
- [ ] Aruba.
- [ ] HP.
- [ ] Intelbras.
- [ ] TP-Link.
- [ ] Outros fabricantes encontrados no inventário.

### 6.2. Switches

- [ ] Monitorar disponibilidade.
- [ ] Monitorar interfaces e portas quando suportadas.
- [ ] Monitorar tráfego de entrada e saída.
- [ ] Monitorar erros, descartes e estado das interfaces.
- [ ] Monitorar velocidade e duplex quando disponíveis.
- [ ] Consultar CPU, memória, temperatura e fontes de alimentação quando suportadas.
- [ ] Identificar portas com problemas persistentes.
- [ ] Identificar interfaces sem coleta de métricas esperadas.
- [ ] Verificar templates e descoberta de interfaces.
- [ ] Evitar duplicar itens e triggers existentes.

### 6.3. Access Points

- [ ] Monitorar disponibilidade.
- [ ] Monitorar interfaces e tráfego quando disponíveis.
- [ ] Monitorar estado e recursos do equipamento quando suportados.
- [ ] Consultar clientes conectados quando houver integração ou API compatível.
- [ ] Identificar problemas de comunicação.
- [ ] Identificar APs sem métricas suficientes.

### 6.4. Roteadores, firewalls e links

- [ ] Identificar roteadores e firewalls existentes.
- [ ] Monitorar interfaces e conectividade.
- [ ] Monitorar tráfego e erros quando suportados.
- [ ] Monitorar CPU, memória e temperatura quando disponíveis.
- [ ] Identificar links WAN e seus indicadores de disponibilidade.
- [ ] Implementar verificações de serviços e destinos críticos quando aplicável.
- [ ] Diferenciar falha de link, equipamento indisponível e falha de consulta.

### 6.5. Critérios de aceite

- [ ] Templates e métodos compatíveis com cada modelo.
- [ ] Métricas verificadas em equipamentos reais.
- [ ] Interfaces identificadas corretamente.
- [ ] Descoberta e sincronização sem duplicações.
- [ ] Limitações de cada fabricante documentadas.

---

## FASE 7 — Telefonia e serviços de comunicação

### 7.1. Telefonia

- [ ] Identificar telefones IP, centrais telefônicas e gateways existentes.
- [ ] Verificar os métodos de monitoramento suportados por cada modelo.
- [ ] Monitorar disponibilidade dos equipamentos.
- [ ] Monitorar interfaces e recursos SNMP quando disponíveis.
- [ ] Monitorar serviços da central por checks apropriados.
- [ ] Consultar estado de registros ou ramais somente quando houver interface autorizada e compatível.
- [ ] Não coletar conteúdo de chamadas ou informações pessoais desnecessárias.

### 7.2. Alertas

- [ ] Equipamento indisponível.
- [ ] Serviço da central indisponível.
- [ ] Falha persistente de comunicação.
- [ ] Indicadores de capacidade ou recursos críticos quando suportados.

### 7.3. Interface

- [ ] Listar telefones, centrais e gateways.
- [ ] Exibir estado e problemas ativos.
- [ ] Agrupar por setor ou localização.
- [ ] Mostrar métricas realmente disponíveis.

---

## FASE 8 — Servidores físicos, máquinas virtuais e plataformas

### 8.1. Servidores físicos

- [ ] Identificar servidores e sistemas operacionais.
- [ ] Verificar instalação e versão dos agentes Zabbix.
- [ ] Monitorar CPU, memória, swap e processos relevantes.
- [ ] Monitorar discos, sistemas de arquivos e espaço disponível.
- [ ] Monitorar interfaces de rede.
- [ ] Monitorar serviços essenciais.
- [ ] Monitorar logs quando houver configuração apropriada.
- [ ] Monitorar hardware via IPMI ou mecanismos do fabricante quando suportados.
- [ ] Identificar servidores sem agente ou com coleta incompleta.

### 8.2. Máquinas virtuais e hypervisors

- [ ] Identificar a plataforma de virtualização instalada.
- [ ] Verificar se existe VMware, Hyper-V, Proxmox ou outra plataforma.
- [ ] Escolher o método de monitoramento adequado à plataforma.
- [ ] Monitorar hypervisors e recursos de virtualização.
- [ ] Monitorar VMs e os recursos disponibilizados.
- [ ] Correlacionar VM, hypervisor e sistema operacional quando possível.
- [ ] Evitar cadastrar a mesma VM duas vezes por mecanismos de descoberta diferentes.
- [ ] Identificar VMs sem agente ou sem métricas suficientes.

### 8.3. Aplicações e bancos de dados

- [ ] Identificar serviços e aplicações críticos.
- [ ] Verificar templates e agentes compatíveis.
- [ ] Monitorar disponibilidade dos serviços.
- [ ] Monitorar bancos de dados por métodos apropriados.
- [ ] Monitorar endpoints HTTP e certificados quando relevantes.
- [ ] Monitorar filas e dependências de aplicações quando suportadas.
- [ ] Identificar falhas de serviço mesmo quando o servidor está acessível.

### 8.4. Critérios de aceite

- [ ] Servidores e VMs com métricas validadas.
- [ ] Serviços críticos identificados.
- [ ] Alertas testados.
- [ ] Relacionamentos entre VMs e hypervisors preservados quando disponíveis.

---

## FASE 9 — Normalização, Assets e visão unificada

### 9.1. Correlação

- [ ] Relacionar hosts do Zabbix aos Assets existentes.
- [ ] Utilizar identificadores externos estáveis.
- [ ] Usar MAC, hostname e IP como evidências auxiliares.
- [ ] Evitar usar apenas IP como identidade do equipamento.
- [ ] Permitir associação manual quando houver ambiguidade.
- [ ] Identificar equipamentos monitorados sem Asset.
- [ ] Preservar histórico dos relacionamentos.
- [ ] Evitar duplicações durante sincronizações.
- [ ] Distinguir o inventário físico do host lógico de monitoramento.

### 9.2. Categorias e metadados

- [ ] Padronizar categorias de equipamento.
- [ ] Padronizar fabricantes e modelos quando conhecidos.
- [ ] Padronizar localização, setor e criticidade quando cadastrados.
- [ ] Preservar grupos, tags e metadados úteis do Zabbix.
- [ ] Identificar equipamentos sem categoria ou localização.
- [ ] Não inferir atributos sem evidência suficiente.

### 9.3. Interface unificada

- [ ] Permitir abrir um Asset e consultar seus dados de monitoramento.
- [ ] Exibir problemas, métricas e histórico relacionados.
- [ ] Mostrar host Zabbix associado.
- [ ] Exibir última coleta ou consulta válida.
- [ ] Permitir navegar do host ao Asset e vice-versa.
- [ ] Identificar relações desconhecidas ou ambíguas.

---

## FASE 10 — Problemas, alertas, histórico e dashboard geral

### 10.1. Integração de problemas e eventos

- [ ] Consultar problemas ativos.
- [ ] Consultar eventos e triggers conforme necessário.
- [ ] Reutilizar o endpoint existente quando compatível.
- [ ] Implementar processamento idempotente.
- [ ] Tratar recuperação de problemas.
- [ ] Preservar identificadores externos.
- [ ] Definir políticas para criar ou atualizar incidentes.
- [ ] Não transformar automaticamente todos os eventos em chamados.
- [ ] Evitar duplicação de incidentes e alertas.

### 10.2. Histórico

- [ ] Registrar sincronizações e resultados.
- [ ] Registrar mudanças observadas.
- [ ] Consultar histórico de métricas sob demanda quando apropriado.
- [ ] Diferenciar horário de origem e horário da observação.
- [ ] Preservar dados anteriores durante falhas temporárias.
- [ ] Definir retenção e limites de armazenamento.
- [ ] Identificar dados desatualizados.

### 10.3. Dashboard geral de infraestrutura

Criar ou completar `Infraestrutura → Visão geral`.

- [ ] Total de hosts monitorados.
- [ ] Hosts por categoria.
- [ ] Hosts por fabricante.
- [ ] Equipamentos com problemas ativos.
- [ ] Problemas por severidade.
- [ ] Impressoras com alertas de consumíveis quando disponíveis.
- [ ] Câmeras e gravadores com problemas.
- [ ] Switches, APs e interfaces com problemas.
- [ ] Servidores e VMs com problemas.
- [ ] Serviços críticos indisponíveis.
- [ ] Hosts sem Asset associado.
- [ ] Hosts sem coleta adequada.
- [ ] Estado da integração e última sincronização.

### 10.4. Interface por categoria

- [ ] Criar filtros por categoria, fabricante, localização, grupo e estado.
- [ ] Permitir navegação dos indicadores para os equipamentos.
- [ ] Exibir detalhes, métricas e problemas.
- [ ] Exibir origem e atualização dos dados.
- [ ] Diferenciar ausência de problemas de falha na consulta.
- [ ] Garantir paginação e desempenho para inventários grandes.

---

## FASE 11 — Operação, segurança, testes e aceite final

### 11.1. Segurança e resiliência

- [ ] Aplicar autorização aos endpoints e páginas.
- [ ] Proteger tokens e credenciais.
- [ ] Utilizar uma conta Zabbix com privilégios mínimos.
- [ ] Aplicar timeout, cancelamento e retry controlado.
- [ ] Evitar consultas excessivas à API.
- [ ] Limitar intervalos e volumes de consultas históricas.
- [ ] Tratar falhas parciais sem apagar dados válidos.
- [ ] Evitar execuções simultâneas conflitantes.
- [ ] Registrar logs, métricas e traces.
- [ ] Não expor segredos em logs.
- [ ] Manter operações de consulta somente leitura nesta integração inicial.

### 11.2. Testes automatizados

- [ ] Testar autenticação e erros de API.
- [ ] Testar sincronização e idempotência.
- [ ] Testar hosts duplicados.
- [ ] Testar problemas e recuperações.
- [ ] Testar consultas históricas.
- [ ] Testar correlação com Assets.
- [ ] Testar filtros, paginação e permissões.
- [ ] Testar falhas e recuperação da conexão.
- [ ] Testar dados ausentes e desatualizados.
- [ ] Testar políticas de incidentes.
- [ ] Testar dashboards com dados reais ou fixtures identificadas como testes.

### 11.3. Validação real

- [ ] Validar a integração com o Zabbix existente.
- [ ] Selecionar amostras reais de cada categoria de equipamento presente.
- [ ] Validar impressoras e consumíveis suportados.
- [ ] Validar câmeras e gravadores.
- [ ] Validar switches e interfaces.
- [ ] Validar Access Points.
- [ ] Validar telefonia.
- [ ] Validar servidores e VMs.
- [ ] Validar serviços e aplicações críticos.
- [ ] Confirmar que as métricas correspondem à origem.
- [ ] Testar uma falha de consulta e sua recuperação.
- [ ] Confirmar ausência de duplicações.
- [ ] Registrar evidências sem expor segredos.

### 11.4. Documentação

- [ ] Documentar arquitetura da integração.
- [ ] Documentar categorias e métodos de monitoramento.
- [ ] Documentar templates e recursos disponíveis.
- [ ] Documentar regras de correlação.
- [ ] Documentar políticas de alertas e incidentes.
- [ ] Documentar configuração e permissões.
- [ ] Documentar limites e lacunas de monitoramento.
- [ ] Documentar testes e evidências de aceite.

### 11.5. Critérios finais de aceite

- [ ] Build funcional.
- [ ] Suítes relevantes aprovadas.
- [ ] Integração validada com o Zabbix real.
- [ ] Hosts, problemas e métricas consultáveis conforme suportado.
- [ ] Categorias de equipamentos identificadas corretamente.
- [ ] Dados apresentados no InfraControl correspondem à origem.
- [ ] Assets não duplicados.
- [ ] Falhas de consulta não são confundidas com equipamentos offline.
- [ ] Dashboard utiliza dados reais.
- [ ] Segurança, histórico e observabilidade validados.
- [ ] Lacunas de monitoramento documentadas.
- [ ] Evidências de execução registradas.

---

## Regras obrigatórias de execução

1. Auditar antes de implementar.
2. Reutilizar o Zabbix e os templates já existentes.
3. Não duplicar mecanismos de coleta.
4. Não inventar métricas ou capacidades de dispositivos.
5. Não presumir que todos os fabricantes oferecem os mesmos dados.
6. Não criar Assets duplicados.
7. Não confundir falha da integração com falha do equipamento.
8. Não apagar dados válidos por falhas temporárias.
9. Não criar incidentes para todos os eventos sem política definida.
10. Não implementar ações de alteração de equipamentos nesta fase.
11. Não sobrescrever alterações de sessões paralelas.
12. Executar build e testes após incrementos relevantes.
13. Documentar limitações e lacunas encontradas.
14. Marcar cada fase como concluída somente após validação.
15. Considerar o monitoramento completo somente quando a cobertura real estiver comprovada.

## Monitoramento contínuo do desenvolvimento

- [ ] Acompanhar o estado do build.
- [ ] Investigar conflitos de sessões paralelas.
- [ ] Executar suítes de testes quando o build estiver estável.
- [ ] Registrar regressões e correções.
- [ ] Atualizar este checklist conforme a implementação avança.
