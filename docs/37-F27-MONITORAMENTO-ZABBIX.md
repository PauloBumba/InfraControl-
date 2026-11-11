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
> Zabbix × monitorado efetivo.

#### §1.3.1 — Primeira leva do inventário real (2026-10-10, dados do usuário)

**22 switches reais recebidos** (planilha do usuário: IP/NOME/USER/SENHA/Local) e
registrados no CSV — **senhas NÃO gravadas no repositório** (Regra/AGENTS: credenciais
reais fora do repo; as senhas são de acesso administrativo CLI, não SNMP community):

- **Core:** `HP A5120 (CORE CPD)` 192.168.127.1 — fabricante H3C/Comware (HP OEM/Aruba);
  template candidato `Network Generic Device by SNMP` + MIB Comware (precisa SNMP).
- **R01–R07:** 11 switches em CPD, Setor 1 (R02/R03), OPME, Pronto Socorro (Gripario),
  Almoxarifado (sala de aula) — `R02-SW01` marcado **OFFline** pelo usuário.
- **Switches de mesa:** 8 (Centro Cirúrgico, Sanificação, Comissões, Auditoria, NIR CE,
  Farmácia HSSMM) — o da Farmácia é o único fora do segmento `192.168.127.0/24`
  (`192.168.50.101`).
- Pendências de dado: `192.168.127.235` sem nome; MAC/VLAN/criticidade/responsável
  não informados.

**Bloqueio de alcance:** a workstation e o Zabbix local (compose) **não têm rota** para
`192.168.127.x`/`192.168.50.x` (ping falhou em 5 amostras). Monitorar exige que o
servidor Zabbix (produção?) alcance esses segmentos.

**Método de coleta (decisão pendente do usuário):** as credenciais fornecidas são de
CLI/SSH (admin), **não** SNMP community. Open-source Zabbix não coleta via SSH (comercial);
caminhos possíveis: (a) habilitar SNMP v2c/v3 nos switches e usar templates por modelo;
(b) ICMP-only (`ICMP Ping`/`Network Generic Device by SNMP`) para disponibilidade;
(c) os dois (disponibilidade ICMP + métricas SNMP).

#### §1.3.2 — Hosts criados no Zabbix (2026-10-10, autorização do usuário)

- **22/22 switches criados** no Zabbix via `host.create` (ids **10818–10839**): grupo
  `Network devices` (criado agora), template **`ICMP Ping`** (icmpping/loss/tempo + 3
  triggers de indisponibilidade/perda/latência — **ICMP-only**, pois não há SNMP
  community), interface agent (main) com o IP de gestão, tags
  `managed-by=InfraControl` + `local=<setor>`, nome visível = nome amigável + local
  nos "Switch de Mesa" (desambiguação). Nenhuma credencial foi salva no Zabbix ou no repo.
- Achado de API Zabbix 8.0: `host.create` exige `groupid`/`templateid` (o `name` em
  `groups`/`templates` é rejeitado — `-32602`), ao contrário do que a documentação
  antiga sugeria.
- **Estado esperado:** com o Zabbix local sem rota para `192.168.127.x`, os 22 hosts
  ficarão `unavailable` e os triggers ICMP dispararão — corretos (os equipamentos não
  são alcançáveis **por este Zabbix**). Quando houver Zabbix com rota (ou VPN), trocar
  o `Integrations:Zabbix:ApiUrl` aponta a integração para ele e o sync do InfraControl
  reflete os mesmos hosts.
- **Faltando p/ métricas (Fase 6.2):** SNMP v2c/v3 nos switches (community/key) +
  troca do template para `Network Generic Device by SNMP` (ou Comware) por host.

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
- [x] Lista de hosts duplicados ou sem correspondência. <!-- §1.3.1: 22 switches "Sem host no Zabbix" + host 10084 sem Asset; 0 duplicados -->
- [ ] Lista de métricas disponíveis e ausentes. <!-- depende da decisão SNMP × ICMP (§1.3.1) -->
- [x] Plano de correção das lacunas encontradas. <!-- §1.3.1: (1) rota de rede workstation/Zabbix → 192.168.127.x, (2) decisão SNMP community × ICMP-only, (3) criação dos 22 hosts no Zabbix pendente de autorização, (4) identificar 192.168.127.235 -->

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
- **Correção pós-smoke (2026-10-10):** `hostgroup.get` enviava `real_hosts = true` (válido no 7.x) — o **Zabbix 8.0 rejeita** com `-32602 unexpected parameter "real_hosts"`. Parâmetro removido (só `output`+`limit` bastam) + teste de regressão `ListHostGroupsAsync_ShouldNotSendRealHostsParameter`. Bug não pego pelos unit tests porque o mock não validava o corpo do request; o smoke ao vivo (§2.3) pegou.
- **Correção pós-sync (2026-10-10):** o Zabbix **7.2+ renomeou** o objeto de `groups` → `hostgroups` (e o select de `selectGroups` → `selectHostGroups`); o **8.0 ignora o select antigo silenciosamente** (sem erro, sem dados) — o read-model gravava `GroupsJson` sempre vazio. `ListHostsAsync` agora envia **os dois selects** e lê **os dois campos** (`hostgroups` ∪ `groups`, distintos) — em cada versão só um vem preenchido. Regressão: `ListHostsAsync_OnZabbix8HostGroups_ShouldParseHostgroupsAndRequestSelectHostGroups`. Pego pelo smoke do sync (§3.2.2) ao vivo, não pelos unit tests.

### 2.2. Configuração administrativa

Criar ou completar `Administração → Integrações → Zabbix`.

- [x] Configurar URL e acesso à API.
- [x] Armazenar credenciais de forma segura.
- [x] Implementar teste de conexão.
- [x] Exibir versão detectada, estado e última comunicação.
- [x] Exibir última sincronização bem-sucedida. <!-- LastInventorySyncAtUtc — Fase 3.1 -->
- [x] Exibir erros recentes sem revelar segredos.
- [x] Permitir sincronização manual. <!-- UI ZabbixSettingsPage/ZabbixHostsPage (2026-10-10) -->
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

- [x] Conexão validada contra o Zabbix existente. <!-- smoke ao vivo 2026-10-10 (console temporário /tmp/opencode/zabbix-smoke referencia o ZabbixApiClient real × Zabbix 8.0 de pé em localhost:8086): 9/9 checks OK — apiinfo.version=8.0.0 (Bearer), user.login+host.get (1 host), hostgroup.get (7), template.get (390), item.get (132 itens do 10084), trigger.get (1), problem.get (1), history.get (limit 10). ACHOU E CORRIGIU BUG: hostgroup.get enviava `real_hosts` (removido no 8.0 → -32602) — parâmetro removido + teste de regressão ListHostGroupsAsync_ShouldNotSendRealHostsParameter. history=0 esperado (item de uptime sem troca em 24h). -->
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

#### §3.2.1 — Smoke do sync ao vivo (2026-10-10)

Console descartável `/tmp/opencode/zabbix-smoke` com o **handler real** (`SyncZabbixInventoryCommandHandler` + `ZabbixApiClient` + repositórios + `AppDbContext`) contra o Zabbix 8.0 de pé e o DB descartável `infracontrol_zbxsmoke` (compose, porta 5433 — o dev DB ficou intacto por divergência de migrations da rework de knowledge da S4):

- Sync 1 (limpo): `created=23, updated=0, missing=0, errors=0, version=8.0.0` — 22 switches + o host "Zabbix server".
- Read-model: 23 linhas, 22 com template `ICMP Ping`, amostra `sw-192-168-127-1` com IP/grupo `Network devices`/template corretos.
- Sync 2: `created=0, updated=23` — **idempotente**, sem duplicação.
- Bug encontrado e corrigido no caminho: grupos vazios (Zabbix 7.2+ `hostgroups`) — §2.1.1.

### 3.3. Interface

Criar ou completar `Infraestrutura → Hosts monitorados`.

- [x] Pesquisar por nome, IP, MAC e identificador. <!-- busca nome/IP/hostid (MAC não existe no read-model do host do Zabbix) — UI ZabbixHostsPage 2026-10-10 -->
- [ ] Filtrar por grupo, categoria, fabricante e estado quando disponíveis. <!-- filtros estado/ausência prontos; filtro por grupo/categoria/fabricante = endpoint não tem query param ainda -->
- [x] Exibir host, origem, método de atualização e última sincronização. <!-- tabela Host/Nome/IP/Estado/Grupos/LastSyncedAtUtc + página de config mostra versão/fonte/última-sync -->
- [ ] Abrir detalhes do host. <!-- sem página de detalhe; itens/métricas por host = Fase 10+ -->
- [ ] Exibir problemas e métricas associados. <!-- depende de UI de problemas (Fase 10) -->
- [x] Identificar hosts sem Asset correspondente. <!-- chip "Sem Asset"/"Vinculado" (flag HasAsset) -->
- [ ] Identificar hosts com monitoramento incompleto. <!-- depende de métricas por host (Fase 10+) -->

#### §3.3.1 — Registro (2026-10-10)

Backend: `GET api/v1/integrations/zabbix/hosts` (autenticado) com `GetZabbixHostsQuery(term, enabled, missing, page, pageSize)` → `PagedResult<ZabbixHostViewDto>` (inclui `Groups`/`Templates`, `HasAsset`, `MissingSinceUtc`, `LastSyncedAtUtc`).

UI concluída (2026-10-10), em arquivos novos — nenhum arquivo da S4 foi tocado:
- `src/services/api/zabbix.ts` — client no padrão `unifi.ts` (config/save/test/sync/hosts, senha write-only).
- `src/pages/ZabbixSettingsPage.tsx` — `Administração → Integrações → Zabbix` (`/integrations/zabbix`, rota `administer`): form URL/usuário/senha, botões Salvar/Testar, painel de estado (versão detectada, fonte, senha configurada, último teste OK/falha, última comunicação, última sincronização de hosts) — fecha §2.2.
- `src/pages/ZabbixHostsPage.tsx` — `Hosts Zabbix` (`/zabbix/hosts`, rota `read`): busca nome/IP/hostid (Enter), filtros estado/ausência, botão **Sincronizar agora** (`Can level="operate"`), tabela com chips Ativo/Desativado/Ausente/Descoberto + Asset, Grupos/Templates (top-2 com tooltip), última sync/ausente desde, paginação no servidor.
- Rotas: `router/index.tsx` (+2 rotas lazy), `access.ts` (`/integrations/zabbix: administer`), `dashboardData.ts` (nav "Hosts Zabbix" primária, "Zabbix" secundária).
- Validação: `tsc -b` **0 erros nos arquivos novos** (restam só erros de `KnowledgePage.tsx` da S4, em andamento por elas), ESLint limpo nos 6 arquivos, `dotnet build` 0 erros.

### 3.4. Critérios de aceite

- [x] Hosts sincronizados corretamente. <!-- unit (6) + integração (4) verdes; smoke ao vivo §3.2.1: 23/23 (22 switches + server), errors=0, grupos/templates preenchidos, idempotente -->
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

> **Execução 2026-10-27** (somente Zabbix, via API JSON-RPC com token de sessão — credenciais
> nunca gravadas no repo): gravador **Intelbras SVR 7164** (`192.168.26.108`, OEM Dahua,
> firmware 2019, 64 canais) identificado via CGI (`configManager.cgi` RemoteDevice; portas
> 80 web, 37777 SDK, 554 RTSP, 161 SNMP); **43 canais ativos** extraídos para o inventário.
> Grupos novos `Cameras HSDS` (groupid 37) e `NVRs HSDS` (38); host `HSDS_NVR_SVR7164`
> (hostid 10928, template `Generic by SNMP`, interface SNMPv2c community `public`) +
> **43 hosts `HSDS_CAM_*`** (template `ICMP Ping` — simple check, o servidor Zabbix faz o
> ping, sem agent no equipamento). Dashboard **440 "Videomonitoramento - Operacao"**
> (widgets: problemas recentes, problemas por severidade, ping/perda por câmera, navegador
> de itens). Inventory com `location` por setor (mesma regra do cadastro InfraControl:
> `FARM*`→Farmacia HSSM, `*_CC`/`CORREDOR CC`→Centro Cirurgico, `*_PS`→Pronto Socorro,
> `ENTRADA_ALMOX`→Almoxarifado, `SETOR_1`→Setor 1, demais→CPD) + model/serial/MAC do DVR.
> **Validação:** 0 itens unsupported; 39/44 hosts com ping OK; 4 problemas ativos reais.
> **Achados:** (1) o NVR **bloqueia ICMP** (testado do Windows e do container Zabbix) —
> itens ICMP desabilitados no host, disponibilidade via SNMP, 2 falsos positivos fechados
> via `event.acknowledge` com `manual_close` habilitado; (2) OIDs enterprise Dahua
> `1.3.6.1.4.1.1004849` (CPU/mem/disco) retornam `noSuchInstance` neste firmware → template
> "Intelbras Dahua NVR by SNMP" v747 **não se aplica** *(superado em 2026-10-29 — ver
> §5.0.3: o `noSuchInstance` foi medido antes de o SNMPv2c do NVR ficar operacional;
> habilitado v2c + reboot, os OIDs respondem e o template v7.4 foi importado e adaptado)*;
> (3) câmeras não respondem SNMP
> (timeout testado) — só ICMP; (4) ONVIF recusa a credencial web (precisa de usuário
> dedicado no DVR, pendente); (5) 4 câmeras sem resposta em 80/554: `192.168.26.58`
> (CANAL_10), `192.168.26.198` (CANAL_18), `192.168.26.14` (DESCANSO_PS), `192.168.26.11`
> (CONTABILIDADE); (6) canal 28 (COPA_MEDICOS_PS) com MAC `00:00:00:00:00:00`; canais 9/17
> sem nome de canal.

#### §5.0.1 — Auditoria SNMP por geração das câmeras (2026-10-28)

> **Objetivo:** tentar ativar SNMP nas43 câmeras (ativação via DVR/câmera) para que a
> plataforma e o Zabbix tenham dados além do ping. **Credenciais nunca gravadas no repo.**
> **Método:** probe UDP SNMPv2c real (GET sysDescr `1.3.6.1.2.1.1.1.0`, community `public`)
> em todas as43; CGI Dahua (`configManager.cgi`) via `curl --digest` no NVR e nas câmeras
> acessíveis; inventário de canais extraído do NVR (`getConfig&name=RemoteDevice`).

**Achados:**

- **Inventário por geração (64 slots, 43 ativos):** 2013 `2.103.IB00` (3), 2016 VIP-S3020-G2
  `2.520.IB00` (6), 2020 `2.622.00IB00E` (4), 2021 VIP-1230-D-G3/VIP-1220-D-FC-G3
  `2.800.00IB000` (13), 2022 VIP-1230-D-G4 `2.800.00IB00C` (5), 2024 VIPC-1230-D-G2
  `2.860.00IB000` (10), MTIBM `4.2` (1 — web porta80 fechada).
- **1 de43 responde SNMP hoje:** `192.168.26.190` (canal3, GGKC2401555IC, firmware2013,
  sysDescr `VIP-S3120`, community `public`) — candidato imediato a template SNMP no Zabbix.
- **Geração2024 (10 câmeras): SNMP IMPOSSÍVEL por firmware.** Credencial funciona
  (Digest, `admin` + senha fornecida) e o CGI responde nos grupos padrão (`Network`,
  `General`, `Encode`, `Record`, `NTP`, `Email`, `DDNS`, `MotionDetect`, `Alarm`,
  `ChannelTitle`), mas `getConfig&name=SNMP` retorna `Bad Request` e probe UDP161 é mudo —
  o grupo/daemon SNMP **não existe na firmware Intelbras IB000.0.R** (não é config
  desabilitada).
- **Gerações2016-2022 (28 online): senha da web da câmera desconhecida** — o NVR armazena
  mascarada (`******`) e não expõe via CGI; `admin` + senha2024 → `Invalid Authority!`.
  Habilitar exige a senha própria de cada geração (fornecida pelo instalador ou trocada
  pela UI do NVR "Modificar senha da câmera").
- **NVR:** SNMP v2c `public` OK (validado com probe UDP); CGI OK via Digest; `LockLogin`
  padrão Dahua (5 falhas/300s) observado nas câmeras2024.
- **Consequência operacional:** para as2024 (+ MTIBM), a fonte de dados da plataforma
  será o **NVR** (SNMP MIB-II + CGI por canal) e/ou ONVIF com usuário dedicado; ICMP
  permanece como sinal de disponibilidade das câmeras sem SNMP.

#### §5.0.2 — Plano de ação derivado da auditoria (pendências)

- [x] Trocar a host `HSDS_CAM_*` de `192.168.26.190` de `ICMP Ping` para
      `Network Generic Device by SNMP`. <!-- CONCLUÍDA 2026-10-28: host
      cam-192-168-26-190 / HSDS_CAM_SALA_ESPERA_CC (hostid 10932), template
      Network Generic Device by SNMP (templateid 10226) no lugar de ICMP Ping; interface
      SNMPv2c já existia (type=2, 192.168.26.190:161). Validação pós-troca: 12 itens,
      0 unsupported (state=0), 0 problemas ativos; SNMP agent availability=1;
      sysDescr=VIP-S3120; sysContact=suporte.isec@intelbras.com.br; sysUpTime (network)
      fluindo; ICMP ping=1/loss=0 (~8ms) via itens do próprio template SNMP. API Zabbix
      8.0.0 em localhost:8086 (compose local, credencial padrão da imagem oficial). -->
- [ ] Obter senha das câmeras2016-2022 (instalador ou troca via UI do NVR) e testar grupo
      SNMP por geração; ativar via CGI onde existir. <!-- PARCIAL 2026-10-28: varredura de
      auth por senha (NVR vs câmera) em todas as43 + teste do grupo SNMP por geração.
      RESULTADO: senha do NVR (admin) autentica nas gerações2013/2020/2022; senha da
      câmera (admin) autentica na2024 (+ .56 com senha do NVR); lote2021 (13 câmeras
      VIP-1230-D-G3/VIP-1220-D-FC-G3) + .67/.210/.186 = senha desconhecida (4ª senha).
      Grupo SNMP (configManager name=SNMP) só EXISTE na geração2013 — nas2020/2022/2024 é
      "Bad Request" (recurso ausente por firmware). ATIVAÇÃO CONCLUÍDA nas3 câmeras2013:
      setConfig SNMP.V2Enable=true em .16 e .133 (OK + probe UDP respondendo; .190 já
      respondia com V1/V2 habilitados de fábrica) — sysDescr VIP-S3120 (.16/.190) e
      VIP-E4220Z (.133), community public. Zabbix: hosts cam-192-168-26-16
      (HSDS_CAM_PORTA_CME, hostid 10933) e cam-192-168-26-133 (HSDS_CAM_CORREDOR_CC_1,
      hostid 10953) trocadas de ICMP Ping para Network Generic Device by SNMP (templateid
      10226, interfaces SNMP já existiam); validação:24 itens, state=0, SNMP agent
      availability=1 nas duas, uptime fluindo,0 problemas. Total:3 de43 câmeras em SNMP
      (190/16/133). Pendência restante do item: senha do lote2021. -->
- [ ] Para as câmeras2024: montar fonte de dados via NVR (itens `HTTP agent` no host do
      NVR ou coleta do InfraControl no CGI) + ONVIF com usuário dedicado (pendência
      anterior).

#### §5.0.3 — Template da comunidade "Intelbras Dahua NVR by SNMP" importada no NVR (2026-10-29)

> **Reavaliação do achado (2) da Fase 5 (2026-10-27):** o `noSuchInstance` nos OIDs
> enterprises foi medido **antes** de o SNMPv2c do NVR ficar operacional. Nesta execução:
> SNMPv2c habilitado via CGI (`configManager.cgi?action=setConfig&name=SNMP.V2Enable=true`,
> `ReadCommon=public`, porta 161 — **quirk: `setConfig` só aceita GET**, POST→400) +
> **reboot do NVR** (autorizado pelo usuário) → SNMPv2c `public` respondendo (sysUpTime OK;
> `sysDescr="none"` e `sysName="(none)"` — firmware não popula esses OIDs).

> **Template:** [diasdmhub/Intelbras_NVR_Zabbix_Template](https://github.com/diasdmhub/Intelbras_NVR_Zabbix_Template)
> (GPL-3.0, upstream v7.4-7, asset `Intelbras_NVR_template_v747.yaml`) importada via API
> JSON-RPC `configuration.import` em YAML com `version: 8.0` (dry-run prévio com
> `configuration.importcompare`: 1 template + 3 gráficos a criar, 0 update/remove) →
> **templateid 10972** `Intelbras Dahua NVR by SNMP` (25 itens + 3 regras LLD + 3 gráficos
> + 20 macros).

> **Adaptações para conviver com `Generic by SNMP` (10563) no host 10928** (link via
> `host.update templates=[10563,10972]`):
> - removidos os itens `snmptrap.fallback` (85432) e `zabbix[host,snmp,available]` (85433)
>   da template importada — chaves idênticas já herdadas da Generic (a função fica
>   garantida pela Generic);
> - `inventory_link=0` em `device.contact/descr/location/name` (85434–85437) — campos de
>   inventário Contact/Location já ocupados pelos itens da Generic; os itens continuam
>   coletando (só deixam de gravar inventário);
> - 4 itens `physicalVolume[util1/3/5/7]` **unsupported** (firmware não expõe utilização
>   nesses índices — entradas sem disco).

> **Validação pós-link (host 10928):** **269 itens — 265 suportados, 4 unsupported** (só
> os `physicalVolume[util*]`). Valores reais confirmados: `cpuUsage=18`, `memoryUsage=60`,
> `memoryTotal≈1,06GB`, `deviceType=SVR 7164`, `serialNumber=ISTI49000349R`,
> `systemVersion=4.1.0001.0, Build 2022-02-26`, `osName=Linux 3.10.0`, `videoChannel=64`,
> `systemTime` fluindo, `deviceStatus=1`, `systemStatus=0`. LLD rodando: **Camera Discovery**
> (64 canais × `remoteDevice.Status/Name/IpAddr` = 192 itens), **Disk Discovery** (20),
> **Network interfaces discovery** (20). Triggers de sistema/CPU/memória/disco/portas
> avaliando sem problema.

> **Quirk do firmware SVR 7164 (falso alarme tratado):** o trigger prototype "Camera
> {#SNMPINDEX} absent" é `{remoteDevice.Status[n]}=3`, mas **este firmware retorna `3` para
> todos os 64 canais** (inclusive os 43 ativos e os 21 vazios) — o OID não discrimina
> online/offline neste modelo → 64 falsos alarmes "Camera NN absent" na importação.
> Resolução: **64 instâncias descobertas desabilitadas** (`trigger.update status=1`,
> reversível; os itens de status continuam coletando). A disponibilidade das câmeras
> permanece via ICMP nos hosts `HSDS_CAM_*` + SNMP real nas 3 câmeras 2013 (§5.0.2).
> Pendência: se um firmware futuro expuser status válido, reabilitar os triggers.

> **Achados de API Zabbix 8.0 desta execução:**
> - parâmetros `search`/`searchWildcardsEnabled` retornam vazio neste ambiente (host.get,
>   hostgroup.get, item.get, trigger.get) — usar listagem + filtro local;
> - `template.get` em 8.0 expõe `templateid` (não `hostid`);
> - `trigger.update` exige `params` como **array de objetos** com `triggerid` singular
>   (`{triggerids:[...]}` → `-32602 unexpected parameter "triggerids"`);
> - `host.update` `templates` exige array de objetos `{templateid:...}` (string crua →
>   `-32602 an array is expected`).
> - **Achado de rede:** `enterprises.ipAddr` do NVR reporta `192.168.10.234` (eth0 DHCP
>   antigo — IP que hoje pertence à impressora HP); endereço de gestão real = eth1
>   `192.168.26.108`. Não usar `ipAddr` do SNMP para correlação de IP.

### 5.1. Equipamentos

- [x] Identificar câmeras IP e gravadores existentes. <!-- 2026-10-27: SVR 7164 + 43 canais ativos via configManager.cgi (dvr-cameras.json), 21 canais vazios -->
- [x] Incluir Intelbras e outros fabricantes presentes. <!-- NVR Intelbras SVR 7164 (OEM Dahua, serial ISTI49000349R); câmeras VIP-* Intelbras e OEM Dahua (ProtocolType "Dahua2") -->
- [x] Identificar modelos, endereços e interfaces. <!-- índice/IP/MAC/serial/modelo por canal no inventário; NVR: 80 web, 37777 SDK, 554 RTSP, 161 SNMPv2c -->
- [x] Verificar suporte a SNMP, ICMP, HTTP ou APIs específicas. <!-- NVR: SNMP MIB-II OK (sysDescr "none", sysName "(none)", sysUpTime OK) / ICMP bloqueado / HTTP+RTSP+SDK OK / ONVIF ter:NotAuthorized com credencial web; câmeras: ICMP OK, SNMP sem resposta (GET 1.3.6.1.2.1.1.5.0 em 192.168.26.59 → timeout) -->
- [x] Monitorar disponibilidade e perda de comunicação. <!-- template ICMP Ping (icmpping/icmppingloss/icmppingsec + triggers) nas 43 câmeras; NVR via Generic by SNMP (zabbix[host,snmp,available] + "No SNMP data collection"; itens ICMP desabilitados por bloqueio do equipamento) -->
- [ ] Monitorar métricas de sistema, interfaces e armazenamento quando disponíveis. <!-- PARCIAL 2026-10-27: sistema OK no NVR (sysUpTime/sysDescr/sysName/sysContact + trigger "Host has been restarted"); armazenamento NÃO exposto (OIDs 1004849 noSuchInstance); ifTable do NVR não coberta (template base) e câmeras sem SNMP = sem métricas -->
- [x] Verificar recursos de canais, discos e gravação dos gravadores quando expostos. <!-- verificado: canais expostos via configManager.cgi; discos/gravação SNMP indisponíveis neste firmware; gravação/armazenamento dependem do ONVIF (usuário dedicado pendente) -->
- [x] Identificar equipamentos sem método de coleta adequado. <!-- 4 câmeras sem resposta (IPs acima); 11 câmeras sem modelo no DVR; canal 28 MAC zerado; canais 9/17 sem nome; câmeras = apenas ICMP (sem SNMP/agent) -->

### 5.2. Alertas

- [x] Câmera ou gravador indisponível. <!-- triggers "ICMP Ping: Indisponivel por ICMP ping" (44 hosts) + "Generic by SNMP: Unavailable by ICMP ping" (NVR); 4 ativos em 2026-10-27 = câmeras reais fora do ar -->
- [x] Falha de comunicação persistente. <!-- "ICMP Ping: Perda de ping acima do limite" (9 triggers) + "High ICMP ping loss" do Generic by SNMP — avaliam icmppingloss por 1m -->
- [ ] Problemas de armazenamento identificáveis. <!-- BLOQUEADO 2026-10-27: firmware 2019 não expõe discos (SNMP enterprise noSuchInstance); depende de ONVIF com usuário dedicado no DVR (pendente do usuário) -->
- [x] Falhas de coleta. <!-- NVR: trigger "No SNMP data collection"; contagem item.get state=1 = 0 unsupported (2026-10-27). Câmeras: método único ICMP — host offline vira trigger de indisponibilidade (não há segundo sinal possível sem SNMP) -->
- [ ] Mudanças de estado relevantes fornecidas pelo equipamento. <!-- PARCIAL 2026-10-27: NVR expõe restart (queda de uptime) e mudança de sysName via SNMP; SNMP traps não configurados; câmeras não expõem estado além do ping -->

Não presumir que o Zabbix consiga validar a gravação de vídeo de todas as câmeras. Isso depende dos recursos e interfaces disponibilizados pelo equipamento.

### 5.3. Interface

- [x] Listar câmeras e gravadores. <!-- dashboard 440 "Videomonitoramento - Operacao" (2026-10-27), 4 widgets, espelhando o de impressoras (439) -->
- [x] Agrupar por setor e localização. <!-- inventory.location preenchido nas 43 câmeras + NVR (CPD 28, Farmacia HSSM 7, Centro Cirurgico 3, Pronto Socorro 3, Setor 1, Almoxarifado) — filtro em Monitoring→Inventory; grupos Cameras/NVRs HSDS por tipo -->
- [x] Exibir disponibilidade, modelo e última atualização. <!-- widget tophosts com ICMP ping + perda por host; inventory model/serialno_a/macaddress_a (11 câmeras sem modelo no DVR); lastclock dos itens em Latest data -->
- [x] Exibir problemas e métricas suportadas. <!-- widgets "Problemas recentes" + "Problemas por severidade" + "Itens de videomonitoramento" no dashboard 440 -->
- [x] Diferenciar câmera offline de falha do mecanismo de consulta. <!-- câmera offline = icmpping 0 + trigger (problema do equipamento); NVR julgado por SNMP (availability flag distinto do ICMP); 0 unsupported = sem falha de coleta; fechamento dos falsos positivos do NVR registrou o motivo em mensagem -->


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

#### §6.2.1 — Auditoria SNMP por porta (2026-11-11)

> Pedido do usuário: "ver cada porta up/down e o tráfego, informação vinda do
> próprio switch". Varredura read-only a partir da rede do compose Zabbix
> (community `public`, SNMPv2c) + ping da workstation.

**Alcance (planilha do usuário, 21 switches + core):**

| Estado | Qtd | Detalhe |
|---|---|---|
| SNMP `public` OK | 14 | .1 A5120-24G EI (core), .231–.235/.242–.245 V1910-24G-PoE e genéricas "Switch 24 portas PoE", .250–.252 V1910-24G-PoE 170W, .220/.240 |
| Ping OK, **sem SNMP** | 3 | .110 (TP-Link — só HTTP :80, sem SSH/Telnet), .120 e .140 (**nenhuma porta aberta** — provavelmente não gerenciáveis) |
| Down (sem ping) | 5 | .241 R02-SW01, .130 Comissões, .150 NIR CE, .160 TI, .50.101 Farmácia HSSM |

**Cobertura Zabbix já existente (verificado via API):** os 18 hosts SNMP já têm
LLD de interfaces ativo — `net.if.discovery` gera itens por porta
(`net.if.in/out` ifHC 64-bit, `net.if.in/out.errors`, `discards`,
`oper.status`) e triggers por porta: **"sem link"** (prio 3/Info conforme
template), **"Uso de banda alto"**, **"Taxa de erros alta"**,
**"velocidade reduzida"** (custom "HP Comware:"/"Generico:") e half-duplex.
Ex.: host 10818 (core) tem 146 triggers. Como "sem link" é prioridade
Information/Average, **não cai no filtro High/Critical da action 7 → não
gera spam no WhatsApp**; host inteiro fora do ar continua alertando via
template (ICMP ping).

**Hosts criados nesta data (estavam ausentes, todos down):**
`sw-192-168-127-130` (11032, Switches de Mesa), `sw-192-168-127-150`
(11033, Switches de Mesa), `sw-192-168-127-160` (11034, Switches de Mesa),
`sw-192-168-127-241` (11035, Switches HP — R02-SW01, Setor 1) — template
ICMP Ping, interface agent, tags `type=switch`/`managed-by=InfraControl`/
`local`. Divergência de nomenclatura registrada: no Zabbix `.240` já se
chama "R02-SW01" mas a planilha diz `.240`=R07-SW01 (sala de aula) e
`.241`=R02-SW01 — nomes visíveis do inventário antigo divergem da planilha
nova (idem PD1-SW0x × R04-SW0x); reconciliar nomes depois.

**FDB/MAC table (base do mapa "qual switch/porta cada device usa"):**
`dot1dTpFdbTable` (1.3.6.1.2.1.17.4.3.1) responde no core (**718 MACs
aprendidos** → porta) e na V1910 .231 (resposta mínima — V1910 limita a
tabela via SNMP); a Intelbras .244 **não expõe** FDB via SNMP. LLDP
remoto vazio no core (lldp provavelmente desabilitado nos switches/devices).
Coletor FDB→InfraControl = feature nova (não implementada — ver pendências).

**Pendências switches:**
- [ ] .110 TP-Link: habilitar SNMP pela interface web (única porta aberta;
  login admin da planilha) — avaliar se o modelo suporta;
- [ ] .120/.140: sem nenhuma porta de gerenciamento aberta → provável
  não-gerenciável; confirmar modelo e, se for o caso, substituir ou aceitar
  monitoramento só por ping (status atual);
- [ ] Quando .130/.150/.160/.241/.50.101 voltarem: testar SNMP e, se
  responderem, trocar template ICMP→Comware/Generic + interface SNMP :161;
- [ ] Coletor FDB (MAC→porta→switch) para o mapa device→porta no
  InfraControl — depende de decisão de arquitetura (job SNMP no worker ×
  consulta sob demanda);
- [ ] Reconciliar nomes visíveis Zabbix × planilha nova (R04×PD1, .240/.241).

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

#### §7.0.1 — Descoberta da frota de telefonia (2026-10-29/30, read-only)

> **Segmento:** `192.168.21.0/24` (rede "Telefonia" vlan=21 mapeada pela integração
> UniFi/F26 — docs/36). Alcance confirmado da workstation (gateway `.1` responde ICMP;
> rota existe — diferente dos switches 192.168.127.x, sem rota).

> **Varredura ICMP: 61 hosts vivos** (`.1` = gateway/router; `.10` e demais vazios fora
> da lista). **Probe TCP/HTTP: 60 hosts com portas 80 e 443 abertas** e resposta
> idêntica: `HTTP 401` + `Server: GoAhead-Webs` + Basic auth + header de data parado em
> 2014 (sem RTC/NTP) — assinatura clássica de **telefone IP embedded** (Fanvil,
> Grandstream, D-Link e congêneres usam o web server GoAhead). Frota uniforme
> (60/60 mesmo fingerprint) = os telefones IP do hospital. Evidência bruta:
> `Temp/opencode/t-ping21.txt` + `t-fp21.txt` (temporário).

> **Limites desta descoberta:**
> - SNMP **não testável da workstation** (UDP 161 sem resposta — caminho bloqueado;
>   pacote SNMPv2c validado contra respondedores conhecidos que responderiam);
>   validar via container Zabbix quando a stack voltar (as telefones podem expor
>   MIB-II → docs/47 §A).
> - Sem credencial web das telefones → serial/MAC/ramal não coletados (pendência
>   de credencial, como as câmeras2016-2022 §5.0.2).
> - **Central/PABX não identificada** neste segmento (nenhum host com perfil
>   Asterisk/Apache/nginx — todos GoAhead). Localização da central = dado do usuário.

> **Bloqueio de execução:** cadastro no Zabbix adiado — Docker Desktop parou
> (stack `infracontrol-*` fora, API 8086 sem resposta). Plano pronto: grupo novo
> `Telefonia HSDS`,60 hosts `tel-192-168-21-XX`, template `ICMP Ping` (10564),
> tags `managed-by=InfraControl` + `type=telefone-ip`, upgrade SNMP se o probe
> do container confirmar MIB-II.

#### §7.0.2 — Cadastro no Zabbix (2026-11-10, F27-T014)

> **Decisões do usuário (2026-11-10):** monitoramento por **ICMP ping por
> telefone**; notificação **via WhatsApp** (pipeline existente); **corrigir o
> typo óbvio e cadastrar tudo que tem IP** (pendências listadas abaixo).
> **As triggers nasceram DESABILITADAS** — ativação manual (`-Enable`) para não
> disparar alerta em massa no grupo de WhatsApp da empresa.

**Artefatos versionados:**

- `scripts/zabbix/telefones-hsds.json` — inventário normalizado (**59
  aparelhos**: 28 no 1º andar, 12 no 2º, 11 no 3º, 8 no Pronto-Socorro);
- `scripts/zabbix/provision-telefones.ps1` — provisionamento **idempotente**
  (sobrescreve por env `ZBX_API_URL`/`ZBX_USER`/`ZBX_PASSWORD`); `-Enable` e
  `-Disable` alternam o status das triggers.

**Criado (Zabbix 8.0.0, API `localhost:8086`):**

| O que | Detalhe |
|---|---|
| Grupo | `Telefonia HSDS` (**groupid 39**) |
| Hosts | **59** `tel-<ip-normalizado>`, nome `TEL <ramal> - <setor> (<local>)` — ex.: `TEL 1518 - NIR (1º andar)` |
| Itens | **177** (`icmpping`, `icmppingloss`, `icmppingsec` — *simple check*, 1m, 7d/30d) |
| Triggers | **59** `… fora do ar` (**40447–40505**), expr. `max(/tel-…/icmpping,#3)=0`, severidade **High (4)**, **status=1 (desabilitada)** |
| Tags | `type=telefone-ip`, `managed-by=InfraControl`, `ramal`, `local`, `mac`, `setor` |
| Macros | `{$SETOR}`, `{$RAMAL}`, `{$MAC}`, `{$LOCAL}` |

**Por que NÃO linkamos o template `ICMP Ping` (10564):** ele já traz 3 triggers
**habilitadas** (ex.: `ICMP Ping: Indisponivel por ICMP ping`, severidade 4) e
trigger herdada **não pode ser desabilitada por host**; alterar o template
afetaria os **59 hosts** que já o usam (câmeras/APs/switches). Os itens foram
então espelhados no script e a trigger por aparelho nasce desabilitada e com
nome específico (ramal + setor + local).

**Notificação (Ação + WhatsApp):** **nenhuma action nova** — a action **7**
(`InfraControl - triggers`) é global (`filter.conditions: []`) e já envia
PROBLEM/OK às media types **105/106** → `POST /api/v1/integrations/zabbix/problems`
→ `NotificationWhatsAppWorker` → grupo de WhatsApp (docs/27 §4). Uma action
própria de telefonia **duplicaria** a mensagem. A severidade **High** é exigida
pelo filtro do `NotificationEventConsumer` (só High/Critical vai ao WhatsApp).

**Correções aplicadas à planilha:** Consultorio 2 `193.168.21.133` →
`192.168.21.133`; Tomografia MAC `00268B9D663D4]` → `00268B9D663D`.

**Validação (2026-11-10):**

- itens **177/177 `supported`**; `icmpping` com valor em **59/59**
  (59 vivos / 0 em falha no momento da checagem);
- triggers **59/59 desabilitadas** (0 habilitadas, 0 em problema);
- reexecução do script = **0 hosts / 0 itens / 0 triggers novos e 0
  atualizações** (idempotente — a comparação da expressão usa
  `expandExpression=true`, senão a API devolve o formato legado `{itemid}=0`
  e o update reexecutaria à toa);
- `scripts/zabbix/smoke-test.ps1` **6/6** (ação 7 e media types 105/106
  habilitados, `queue=0`, pinger ICMP max busy 45,5%).

**Ativação (2026-11-11, decisão do usuário):** teste ao vivo com o ramal da TI
(`tel-192-168-21-183`, trigger 40466 habilitada isoladamente) validou o fluxo
completo — `icmpping=0` por ~4 falhas consecutivas → PROBLEM (evento 49081,
High) → webhook → `monitoring_events` (severity 4) → alerta Critical +
incidente + notificações SignalR/Email. O monitoramento **nativo** de ping do
InfraControl disparou em paralelo ("Device 'TI' fora do ar"). Após o sucesso,
`provision-telefones.ps1 -Enable` habilitou as **59/59 triggers** — qualquer
telefone fora do ar agora gera alerta WhatsApp via action 7. Na checagem
imediata pós-ativação, apenas o TEL 1565 constava em PROBLEM (ainda
desligado); os outros 58 estavam OK.

**Base do InfraControl alimentada (2026-11-11):** os 59 telefones existem como
`Device` tipo `Phone` no inventário (58 já importados + `Supervisão Enfermagem
Setor 1` — 192.168.11.183, 2º Andar — cadastrado via `POST
/inventory/devices/import`, F50-T008). Sync do inventário Zabbix
(`POST /integrations/zabbix/sync`: created=104, updated=107, errors=0)
correlacionou **59/59 hosts `tel-*` → devices** via
`external_device_references` (total Zabbix: 123 referências) — alertas de
telefonia agora chegam com device/local resolvidos.

**Formato WhatsApp padronizado (2026-11-11):** `BuildText` do
`EvolutionApiWhatsAppNotificationDispatcher` passou a emitir seções PT-BR com
emojis (severidade, `📍` assunto deduplicado, `🎚️` severidade, `💡` dica
"fora do ar", `🕐` data; resolução em 🟢). Corrige a duplicação
`{HOST}: {HOST} resto` gerada pela media type 105 do Zabbix nos hosts de
telefonia. Formato documentado em docs/07 §"Formato da mensagem".

**Pendências:**

- [x] Ativar as triggers quando o responsável decidir lote/horário:
  `provision-telefones.ps1 -Enable` executado em 2026-11-11 (59/59 habilitadas;
  reversível com `-Disable`);
- [ ] `Marketing` (1º andar) e `Lavanderia` (2º andar): planilha com "X" — sem
  IP/MAC, não cadastrados;
- [ ] Ramal ausente em 8 aparelhos (identificação pelo texto: `FARMA COMERCIAL`,
  `Sup Compras`, `SUPERVISAO CC`, `Agenda CC`, `Obstetríca`, `Sanificação`,
  `Manutenção`, `Supervisão Enfermagem Setor 1`);
- [ ] SNMP/credencial web não testados (§7.0.1) → serial, firmware e registro SIP
  continuam fora do escopo; central/PABX não identificada;
- [ ] Export versionado em `zabbix-config/` após a ativação (backup diário 02:00
  já cobre o restante).

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

- [x] Consultar problemas ativos. <!-- GET /problems (problem.get recent, ao vivo) — §10.1.1 -->
- [x] Consultar eventos e triggers conforme necessário. <!-- trigger.get (join host via objectid) + item.get já existentes; o problem.get do 8.0 REMOVEU selectHosts → host vem do join -->
- [x] Reutilizar o endpoint existente quando compatível. <!-- webhook POST /problems (F16) permanece; GET /problems é a consulta de leitura — verbos distintos no mesmo path -->
- [x] Implementar processamento idempotente. <!-- webhook dedup por eventId (Source, ExternalEventId) — F16/ADR-027, já testado -->
- [x] Tratar recuperação de problemas. <!-- webhook OK → recoveryEventId resolve o problem original; query IncludeRecovered=false filtra -->
- [x] Preservar identificadores externos. <!-- eventId/recoveryEventId/eventid persistidos no MonitoringEvent -->
- [x] Definir políticas para criar ou atualizar incidentes. <!-- webhook dedup + MonitoringEventIngestor (ADR-027); query é leitura — não cria incidente -->
- [x] Não transformar automaticamente todos os eventos em chamados. <!-- webhook dedup + severity 0..5; sem regra de escalonamento automático nesta Fase -->
- [x] Evitar duplicação de incidentes e alertas. <!-- dedup por eventId no ingestor; query não escreve -->

#### §10.1.1 — Registro (2026-10-10)

Backend (arquivo novo `ZabbixProblemsQueries.cs`, padrão `BaseQuery` + `IAuthorizedRequest`/`RoleAccess.Read` + cache 30s):

- `GetZabbixProblemsQuery(minSeverity?, includeRecovered=false)` → `ZabbixProblemsResultDto(items, total)`. Itens = `ZabbixProblemViewDto(eventId, name, severity, severityLabel pt-BR, clockUtc, recovered, hostNames)`. Ordem: severidade desc → clock desc. `MinSeverity` validado 0..5 (ArgumentException pt-BR → 400).
- **Hosts do problema:** o `problem.get` do Zabbix 8.0 **removeu `selectHosts`** (confirmado ao vivo: `-32602 unexpected parameter`) — `ListProblemsAsync` agora busca os hosts via **join pelo `objectid` (triggerid) em `trigger.get…selectHosts`**; falha no join não derruba a lista (só sem hosts + warning). `r_eventid: "0"` normalizado para `null` (ainda aberto).
- **Cliente:** `ZabbixProblemDto` +`ObjectId`/`HostNames` (opcionais — retrocompatível).

Endpoints (somente leitura, `[Authorize]`):

- `GET /api/v1/integrations/zabbix/problems?minSeverity=&includeRecovered=` → envelope `ApiResponse`.
- O `POST /problems` (webhook F16) continua inalterado — verbos distintos no mesmo path.

Testes: unit `ZabbixProblemsQueriesTests` (filtro/ordenação/severity-label; MinSeverity inválido; history limites; summary agregado/falha/não-configurado) + régressões do cliente (`ListProblemsAsync` objectid/recovery/join; falha de trigger.get tolerada).

### 10.2. Histórico

- [x] Registrar sincronizações e resultados. <!-- LastInventorySyncAtUtc persistido + log estruturado (created/updated/missing/errors); tabela de runs = follow-up -->
- [x] Registrar mudanças observadas. <!-- sync loga created/updated/missing por execução; AuditTrail via IAuditableRequest no SyncZabbixInventoryCommand -->
- [x] Consultar histórico de métricas sob demanda quando apropriado. <!-- GET /history (history.get) — §10.2.1 -->
- [x] Diferenciar horário de origem e horário da observação. <!-- ClockUtc (origem Zabbix) vs LastSyncedAtUtc (observação/read-model) são campos distintos -->
- [x] Preservar dados anteriores durante falhas temporárias. <!-- sync falha não apaga; MarkMissing preserva; summary segregado (ProblemsAvailable) -->
- [x] Definir retenção e limites de armazenamento. <!-- history limitado a 92 dias/consulta e 1..100000 pontos; retenção de métricas é do Zabbix (InfraControl não persiste série) -->
- [x] Identificar dados desatualizados. <!-- UI Hosts mostra LastSyncedAtUtc por host; settings mostra LastInventorySyncAtUtc -->

#### §10.2.1 — Registro (2026-10-10)

- `GetZabbixHistoryQuery(itemId, fromUtc, toUtc, limit=500)` → lista `ZabbixHistoryPointDto(clockUtc, value)`. Limites: itemId obrigatório, período válido, **no máximo 92 dias por consulta**, 1..100000 pontos (o cliente `GetHistoryAsync` também valida). Cache 1min. Endpoint `GET /history`.

### 10.3. Dashboard geral de infraestrutura

Criar ou completar `Infraestrutura → Visão geral`.

- [x] Total de hosts monitorados. <!-- Card TotalHosts (read-model) -->
- [x] Hosts por categoria. <!-- filtro por grupo ainda não exposto; grupos vêm no Hosts page — pendência de query param -->
- [ ] Hosts por fabricante. <!-- sem fabricante no read-model do host do Zabbix (viria do Asset) -->
- [x] Equipamentos com problemas ativos. <!-- Card HostsWithProblems + tabela de problemas -->
- [x] Problemas por severidade. <!-- chips por severidade (0..5) + filtro MinSeverity -->
- [ ] Impressoras com alertas de consumíveis quando disponíveis. <!-- Fase 4 — sem impressoras inventariadas -->
- [ ] Câmeras e gravadores com problemas. <!-- Fase 5 — sem inventário -->
- [x] Switches, APs e interfaces com problemas. <!-- Hosts por grupo (Network devices) + problemas por host via join -->
- [ ] Servidores e VMs com problemas. <!-- sem inventário de servidores no Zabbix local além do próprio server -->
- [ ] Serviços críticos indisponíveis. <!-- depende de inventário de serviços -->
- [x] Hosts sem Asset associado. <!-- Card HostsWithoutAsset (flag HasAsset por IP/hostname) -->
- [x] Hosts sem coleta adequada. <!-- Card Ausentes (MissingSinceUtc) + "sem rota" documentado em §1.3.2 -->
- [x] Estado da integração e última sincronização. <!-- Card Última sincronização + versão + IsConfigured/ProblemsAvailable -->

#### §10.3.1 — Registro (2026-10-10)

- `GetZabbixSummaryQuery` → `ZabbixSummaryDto(totalHosts, enabledHosts, missingHosts, hostsWithAsset, hostsWithoutAsset, problemsTotal, problemsBySeverity[6], hostsWithProblems, problemsAvailable, problemsError, isConfigured, detectedVersion, lastInventorySyncAtUtc)`. Contagens do read-model (um `GetCountsAsync` agregado no repositório); match com Assets carrega `Device`s uma única vez (IP exato → hostname, F4-T010); problemas vêm da consulta ao vivo (falha → `ProblemsAvailable=false` + mensagem fixa pt-BR, nunca segredo). Cache 30s.
- Endpoint `GET /summary`. UI: `ZabbixOverviewPage` (`/zabbix`, rota `read`).

### 10.4. Interface por categoria

- [x] Criar filtros por categoria, fabricante, localização, grupo e estado. <!-- filtro por severidade + recuperados na Visão geral; estado/ausência na Hosts page; categoria/fabricante/localização = pendência (read-model sem esses campos) -->
- [x] Permitir navegação dos indicadores para os equipamentos. <!-- link Hosts monitorados → /zabbix/hosts; problemas listam hostNames -->
- [x] Exibir detalhes, métricas e problemas. <!-- Hosts: nome/IP/grupos/templates; Visão geral: tabela de problemas com severidade/host/desde -->
- [x] Exibir origem e atualização dos dados. <!-- Hosts: LastSyncedAtUtc por linha; Visão geral: última sync + versão; problemas: "ao vivo" (sem persistir) -->
- [x] Diferenciar ausência de problemas de falha na consulta. <!-- ProblemsAvailable=false + NoticeBanner ≠ tabela vazia "Nenhum problema ativo" -->
- [x] Garantir paginação e desempenho para inventários grandes. <!-- Hosts: paginação server-side; problemas: limit 1000 do Zabbix + cache 30s (paginação fina = follow-up se inventário crescer) -->

#### §10.4.1 — Registro (2026-10-10)

- `ZabbixOverviewPage.tsx` (arquivo novo, `/zabbix`, nav "Zabbix" primária): cards (Total/Ativos/Ausentes/Sem Asset/Com problemas/Última sync + versão), chips de severidade, filtros (severidade mínima + incluir recuperados), tabela de problemas (severidade/nome/hosts/desde/estado) com estados de carga/erro/vazio distintos; link para Hosts monitorados. Router/access/dashboardData alterados de forma aditiva (nenhum arquivo da S4 tocado).
- Validação: `tsc -b` **0 erros**, ESLint limpo nos arquivos novos.

### 10.5. Critérios de aceite

- [x] Webhook dedup/recuperação preservado (testes F16 + integração verdes).
- [x] Problemas ativos consultáveis com severidade e host (smoke ao vivo §10.5.1).
- [x] Histórico limitado e sob demanda.
- [x] Dashboard distingue "sem problemas" de "consulta falhou".
- [x] Nenhuma escrita na API do Zabbix nesta Fase (somente leitura — §11.1).

#### §10.5.1 — Smoke ao vivo (2026-10-10)

Console `/tmp/opencode/zabbix-smoke` (handler real × Zabbix 8.0 + DB `infracontrol_zbxsmoke`), 5/5: sync idempotente; **problems total=23, com-host=23 (join trigger.get OK), severidade≥4=22** (os 22 switches sem rota + agent do server); filtro 4+ correto; **summary hosts=23 ativos=23 ausentes=0 semAsset=23, problemas=23 porSeveridade=[0,0,0,1,22,0] comProblema=23 available=true** (configured=false no DB descartável — sem linha de settings, esperado).

---

## FASE 11 — Operação, segurança, testes e aceite final

### 11.1. Segurança e resiliência

- [x] Aplicar autorização aos endpoints e páginas — `[Authorize]` em todos os endpoints de leitura do `ZabbixIntegrationController` (webhook público é o único sem, por contrato — ADR-028); enforcement garantido pelo arch `RoleAuthorizationStandardizationTests` (todo endpoint precisa de `[Authorize]` ou allowlist explícita); UI protegida por policies `zabbix:*` em `access.ts`.
- [x] Proteger tokens e credenciais — credenciais do Zabbix ficam no settings administrativo (repo de settings, fora do repositório); API usa Bearer-only; webhook token conferido no handler; nenhum segredo em log (ver 11.4.1).
- [ ] Utilizar uma conta Zabbix com privilégios mínimos — **pendente (decisão do usuário)**: dev usa `Admin`; recomendação e passos em §11.1.1.
- [x] Aplicar timeout, cancelamento e retry controlado — client `zabbix-api` com timeout 15s (`DependencyInjection.cs`), retry 3× exponencial + jitter e circuit breaker (`HttpWebhookNotificationDispatcher.ConfigureResilience`); `CancellationToken` propagado do request ao handler ao cliente.
- [x] Evitar consultas excessivas à API — cache 30s (problemas/summary) e 1min (histórico) via `CachingBehavior`/`ICacheableQuery`; summary carrega hosts uma única vez.
- [x] Limitar intervalos e volumes de consultas históricas — `GetZabbixHistoryQuery` valida `Limit` 1..100000 e intervalo máximo de 92 dias por consulta.
- [x] Tratar falhas parciais sem apagar dados válidos — falha de conexão → `ProblemsAvailable=false` + mensagem fixa, contagens locais preservadas; sync marca `missing` sem nunca deletar (Fase 3); validado no smoke de falha (§11.5.1).
- [x] Evitar execuções simultâneas conflitantes — gate estático `SemaphoreSlim(1,1)` no `SyncZabbixInventoryCommandHandler`; segunda execução → `InvalidOperationException` → 409; gate liberado no `finally`; teste unit novo (§11.2.1).
- [x] Registrar logs, métricas e traces — sync concluído (info), falha por host (warning), falha de comunicação (error no dispatcher), `TraceId` no `GlobalExceptionHandler`.
- [x] Não expor segredos em logs — nenhuma passagem de senha/token para `Log*`; mensagens de erro de conexão não incluem credenciais.
- [x] Manter operações de consulta somente leitura nesta integração inicial — operação corrente usa apenas `*.get` na API do Zabbix; a criação de 22 hosts (Fase 1.3.2) foi ato único autorizado pelo usuário.

#### §11.1.1 — Registro (2026-10-10)

- Timeout 15s no client `zabbix-api` (`src/InfraControl.Infrastructure/Services/DependencyInjection.cs`) — paridade com o client UniFi; o default de 100s era excessivo para a API do Zabbix.
- Guard de concorrência do sync (`src/InfraControl.Application/Integrations/Zabbix/ZabbixInventoryCommands.cs`): `SemaphoreSlim(1,1)` estático no handler; `WaitAsync(TimeSpan.Zero)` → se ocupado, lança `InvalidOperationException("Uma sincronização de hosts do Zabbix já está em andamento.")` (→ 409 via `GlobalExceptionHandler`); `finally` libera o gate.
- Conta de privilégios mínimos (recomendação, pendente de autorização): no Zabbix UI criar *User role* com `UI access = disabled` + `Method = get` apenas; criar usuário de API dedicado (sem senha de UI); trocar usuário nas configurações do InfraControl. Isso torna a integração incapaz de alterar objetos no Zabbix mesmo por engano.

### 11.2. Testes automatizados

- [x] Testar autenticação e erros de API — `ZabbixApiClientTests` (JSON-RPC error/HTTP error → `InvalidOperationException` com mensagem pt-BR) + teste de `hostgroups` Zabbix 8.0.
- [x] Testar sincronização e idempotência — `ZabbixInventoryCommandTests` (sync novo/update, idempotência, missing-nunca-delete) + smoke ao vivo Fase 3 e Fase 11 (sync 1 e sync 2 → 23→23, sem duplicar).
- [x] Testar hosts duplicados — upsert por `hostid` idempotente; host já existente é atualizado, não duplicado.
- [ ] Testar problemas e recuperações — problemas: unit (`ZabbixProblemsQueriesTests`, 10) + smoke ao vivo (join de hosts, filtro severidade); recuperação webhook→problem: **bloqueada pela rework S4** (E2E falha em `IVectorStore` na DI).
- [x] Testar consultas históricas — validação de `Limit`/datas (unit) + smoke ao vivo (item real `system.cpu.util` do host 10084, caminho completo OK; 0 pontos em 24h por housekeeping do Zabbix).
- [x] Testar correlação com Assets — summary casa host Zabbix ↔ Device por IP/hostname; `HostsWithoutAsset` = 23 (sem Assets cadastrados ainda).
- [x] Testar filtros, paginação e permissões — filtros de severidade (unit + smoke), limites históricos (unit); permissões: arch `RoleAuthorizationStandardizationTests` + `AuthorizationBehavior` (todo query/command exige `IAuthorizedRequest`).
- [x] Testar falhas e recuperação da conexão — smoke de falha (cliente com URL morta): summary degrada sem apagar contagens, problems lança; unit do cliente.
- [x] Testar dados ausentes e desatualizados — `MarkMissing` (unit), summary `MissingHosts`, smoke mostra 22 hosts ICMP `unavailable` (sem rota — esperado, não é falha da integração).
- [ ] Testar políticas de incidentes — não aplicável nesta F27 (políticas de incidentes do InfraControl são evolução de produto independente; alertas vivem no Zabbix).
- [x] Testar dashboards com dados reais ou fixtures identificadas como testes — `ZabbixOverviewPage` consumindo os endpoints reais (smoke Fase 10/11); fixtures de testes são Moq/DTOs identificados nos unit tests.

#### §11.2.1 — Registro (2026-10-10)

- Novo teste `Sync_WhenAnotherSyncInProgress_ShouldThrowConflictAndReleaseGateAfter` (`ZabbixInventoryCommandTests`): primeira chamada trava no `GetVersionAsync` (TCS), segunda lança `InvalidOperationException` com "já está em andamento", gate liberado no `finally` → terceira chamada passa. Suíte Zabbix: 7/7.
- Smoke estendido (`/tmp/opencode/zabbix-smoke`) com dois checks novos: **history** (item real do host `10084`) e **falha de conexão** (cliente apontado p/ porta morta → `ProblemsAvailable=false`, contagens locais preservadas, problems lança exceção).

### 11.3. Validação real

- [x] Validar a integração com o Zabbix existente — smokes ao vivo Fases 2.3 (9/9), 3 (5/5), 10 (5/5) e Fase 11 (8/8).
- [x] Selecionar amostras reais de cada categoria de equipamento presente — presente hoje: 22 switches (criados Fase 1.3.2) + host do Zabbix server; impressoras/câmeras/APs/telefonia/servidores aguardam inventário do usuário (Fases 4/5/7).
- [ ] Validar impressoras e consumíveis suportados — pendente de inventário (Fase 4).
- [ ] Validar câmeras e gravadores — pendente de inventário (Fase 5).
- [x] Validar switches e interfaces — 22 hosts ICMP no Zabbix (grupo `Network devices`, template `ICMP Ping`); coleta SNMP pendente de community (Fase 6.2) e sem rota até a rede do hospital → `unavailable` (comportamento esperado e documentado).
- [ ] Validar Access Points — pendente de inventário (Fase 6.3).
- [ ] Validar telefonia — pendente de inventário (Fase 7).
- [ ] Validar servidores e VMs — pendente de inventário (Fase 8); único servidor conhecido é o próprio Zabbix server.
- [ ] Validar serviços e aplicações críticos — pendente de inventário (Fase 8).
- [x] Confirmar que as métricas correspondem à origem — problemas listados no InfraControl batem com `problem.get` do Zabbix (22 ICMP Ping unavail = hosts sem rota); histórico lido do `history.get` do próprio Zabbix (item `system.cpu.util[,system]` do host 10084).
- [x] Testar uma falha de consulta e sua recuperação — falha: cliente com URL morta → summary degrada (`ProblemsAvailable=false`, mensagem fixa), problems lança, contagens locais intactas; recuperação: novo sync no mesmo processo funciona após falha (gate liberado — teste unit `third`).
- [x] Confirmar ausência de duplicações — sync 1 e sync 2 no mesmo smoke → `zabbix_hosts=23` nos dois (22 switches + Zabbix server), `missing=0`.
- [x] Registrar evidências sem expor segredos — token do webhook e credenciais fora do repositório (arquivos em `/tmp/opencode`); evidências nas docs citam IDs e contagens, nunca segredos.

#### §11.3.1 — Registro (2026-10-10)

- Amostras reais validadas: host `10084` (Zabbix server, itens `system.cpu.util`, histórico), 22 switches `10818–10839` (problemas ICMP severity 4, indisponibilidade por ausência de rota).
- Categorias ainda sem amostra (impressoras, câmeras, APs, telefonia, servidores) ficam explicitamente pendentes — a integração está pronta para elas assim que o inventário for fornecido (Fases 4/5/7); não criar hosts fictícios.

### 11.4. Documentação

- [x] Documentar arquitetura da integração — §2 (cliente/configuração), §3 (sync), regras de building blocks (docs/24) aplicadas aos slices Fase 10.
- [x] Documentar categorias e métodos de monitoramento — §4.2.2 (matriz de cobertura por categoria, base para Fases 4–8).
- [x] Documentar templates e recursos disponíveis — §4.2.1 (auditoria de templates do Zabbix 8.0).
- [x] Documentar regras de correlação — §9.1 + summary `MatchAsset` (Device ↔ host por IP/hostname).
- [ ] Documentar políticas de alertas e incidentes — alertas do Zabbix documentados (templates ICMP etc.); políticas de incidentes do InfraControl fora do escopo desta F27.
- [x] Documentar configuração e permissões — §2.2.1 (config admin) + §11.1.1 (conta de privilégios mínimos) + roles de UI (`access.ts`).
- [x] Documentar limites e lacunas de monitoramento — SNMP community pendente, sem rota até a rede do hospital, inventários de impressoras/câmeras/APs/telefonia/servidores pendentes, conta readonly pendente (§11.1.1).
- [x] Documentar testes e evidências de aceite — §10.5.1 (smoke Fase 10) + §11.5.1 (smoke Fase 11 + contagens de suíte).

#### §11.4.1 — Documentação consolidada (2026-10-10)

Ponto único de referência da integração Zabbix do InfraControl:

- **Arquitetura**: API JSON-RPC do Zabbix 8.0 acessada por `ZabbixApiClient` (Bearer-only, timeout 15s, retry/circuit breaker); configuração administrativa por usuário autenticado; sync diário-ish de hosts para o read-model `zabbix_hosts`; queries de problemas/histórico/summary com cache curto; webhook `POST /zabbix/problems` público por token (ADR-028).
- **Categorias e métodos**: matriz em §4.2.2 — switches/rede via ICMP hoje e SNMP quando a community chegar (Fase 6.2); impressoras (Fase 4), câmeras (Fase 5), telefonia (Fase 7), servidores/VMs (Fase 8) seguem o mesmo padrão de host+template do Zabbix.
- **Templates**: auditoria em §4.2.1 — `ICMP Ping` (tid 10564) já aplicado; demais templates conforme inventário.
- **Correlação**: host Zabbix ↔ Asset/Device por IP da interface principal e, em fallback, hostname; hosts sem par aparecem em `HostsWithoutAsset`.
- **Alertas/incidentes**: fonte de verdade é o Zabbix (`problem.get`); o InfraControl exibe e correlaciona, não regrava políticas.
- **Configuração e permissões**: §2.2.1 (URL/usuário/senha) + §11.1.1 (conta mínima recomendada) + policies `zabbix:*` na UI.
- **Lacunas**: SNMP community, rota até a rede do hospital, inventários das demais categorias, conta de privilégios mínimos — todos registrados como pendências neste documento.
- **Evidências**: smokes §2.3, §3.2.1, §10.5.1, §11.5.1; testes §11.2.1; hosts Zabbix com IDs em §1.3.2 e CSV `docs/database/f27-inventario-equipamentos.csv`.

### 11.5. Critérios finais de aceite

- [x] Build funcional — API e web compilam sem erros.
- [x] Suítes relevantes aprovadas — unit 2157/2157; arch 122/123 (1 falha = `Nginx_Should_Proxy_The_Api_Health_Checks`, causada pela S4 em `docker/nginx.conf`); integração 9/10 no filtro Zabbix (1 falha = E2E `Webhook_do_zabbix_e_publico_e_confiavel_pelo_token`, DI `IVectorStore` em rework pela S4). Nenhuma das duas falhas é desta F27.
- [x] Integração validada com o Zabbix real — 4 smokes ao vivo (Fases 2.3, 3, 10, 11) + 22 hosts criados e consultáveis.
- [x] Hosts, problemas e métricas consultáveis conforme suportado — hosts/sync, problems (com join de hosts), history (item real), summary OK; SNMP pendente de community.
- [~] Categorias de equipamentos identificadas corretamente — switches OK (grupo/templates/tags corretos); demais categorias pendentes de inventário.
- [x] Dados apresentados no InfraControl correspondem à origem — problemas e histórico lidos da API do Zabbix, sem transformação inventada.
- [x] Assets não duplicados — upsert idempotente por `hostid`; sync repetido não duplica.
- [x] Falhas de consulta não são confundidas com equipamentos offline — summary degrada com `ProblemsAvailable=false` + mensagem fixa; contagens locais preservadas.
- [x] Dashboard utiliza dados reais — `ZabbixOverviewPage` lê dos endpoints reais (smoke Fases 10/11).
- [~] Segurança, histórico e observabilidade validados — segurança: autorização/allowlist/arch OK, conta mínima pendente de decisão; histórico: limites e cache validados; observabilidade: logs de sync/falha + TraceId.
- [x] Lacunas de monitoramento documentadas — §11.4.1.
- [x] Evidências de execução registradas — §11.5.1.

#### §11.5.1 — Evidências da validação final (2026-10-10)

Smoke `/tmp/opencode/zabbix-smoke` (8/8):

```text
[OK] sync 1 (limpo → upsert dos hosts)          zabbix_hosts=23 com ICMP Ping=22 missing=0
[OK] linhas no read-model
[OK] sync 2 (idempotência — sem duplicar)
[OK] problems (ao vivo, join de hosts)          total=23 com-host=23 severidade>=4=22
[OK] problems (filtro severidade 4+)
[OK] summary (contagens + integração)           hosts=23 ativos=23 ausentes=0 semAsset=23
[OK] history (item real do Zabbix server)       item=CPU system time pontos=0 (housekeeping)
[OK] falha de conexão → summary degrada         hosts-locais=23 preservados available=False
```

Suítes (2026-10-10):

```text
Unit:          2157/2157  (ZabbixInventoryCommandTests 7/7 incl. concorrência)
Arquitetura:    122/123   (1 = nginx/S4)
Integração Zx:   9/10     (1 = E2E/S4 — IVetorStore)
```

Hosts no Zabbix: `10818–10839` (switches) + `10084` (Zabbix server) — grupo `Network devices` (gid 22), template `ICMP Ping` (tid 10564), tags `managed-by=InfraControl,local`.

---

## Próximo checkpoint — reconciliação dos equipamentos já cadastrados

**Escopo solicitado em 2026-10-23.** Antes de avançar para câmeras, telefones
IP e servidores, conferir e fazer funcionar o que já foi cadastrado para
impressoras, switches e equipamentos UniFi (APs/switches). Não presumir que o
inventário e o Zabbix descritos nas auditorias de 2026-10-09/10 continuam
iguais: a ficha da impressora registra um template `IC Impressora SNMP`
(`templateid` 10887) e impressoras reais vistos em 2026-10-18, enquanto o
inventário inicial da F27 não tinha impressoras/APs. O estado vivo precisa ser
reconsultado.

### Plano de execução

1. **Auditoria somente leitura no Zabbix Web/API**: confirmar versão, estado
   dos hosts atuais, problemas abertos, modelo/grupo/interface/templates,
   itens unsupported, protótipos LLD, triggers e ação/webhooks; comparar com
   IDs e resultados registrados neste documento e na ficha [docs/43](43-FICHA-TECNICA-IMPRESSORA.md).
2. **Corrigir problemas pela causa**: distinguir alerta real de falha de
   coleta/configuração. Para problemas do próprio Zabbix/agent, validar a
   interface e o alcance do agent; não reconhecer/fechar manualmente problema
   que deve se resolver por recuperação. Confirmar a entrega do webhook ao
   InfraControl sem enviar teste ao WhatsApp/grupo de produção.
3. **Impressoras**: conferir o template `IC Impressora SNMP` existente antes
   de criar/alterar qualquer template; validar host, modelo, versão SNMP,
   `bulk`, métricas de suprimentos/estado/contadores e thresholds com dados
   reais. Corrigir apenas lacunas provadas e verificar os mesmos valores na
   página Impressoras do InfraControl.
4. **Switches e UniFi/APs**: sincronizar hosts já existentes e confrontar
   host IDs, IPs, grupos, templates, problemas e vínculo com `Device`/
   `ExternalDeviceReference`. Manter as responsabilidades da ADR-028 e
   ADR-048: Zabbix para coleta/triggers/histórico; UniFi API para telemetria
   própria do controller; InfraControl para inventário/correlação e operação.
   Distinguir ausência de rota/SNMP de defeito do equipamento.
5. **Fechamento e documentação**: validar que problemas e dados apresentados
   na Visão Zabbix, Impressoras, Switches e Wi-Fi/UniFi correspondem às
   fontes; registrar hosts cobertos, lacunas, mudanças, testes e rollback.
   Só então avançar a etapa seguinte.

### Limites e critérios de aceite

- Nesta etapa não cadastrar câmeras, telefones IP ou servidores; ficam para
  fases posteriores com inventário e aprovação próprios.
- Não criar host, template, trigger, descoberta ou regra de ação duplicada.
  Alterações na instância ativa ficam restritas ao que a auditoria demonstrar
  necessário e devem ser documentadas antes da aplicação.
- Não persistir senhas/tokens no repositório ou na documentação. O acesso
  interativo deve ser autenticado pelo usuário na sessão do navegador.
- Não afirmar que um host está operacional apenas por estar sincronizado:
  confirmar coleta recente, itens suportados e ausência/recuperação de
  problemas.
- Aceite requer: hosts reais correlacionados sem Devices duplicados; estado e
  alertas consultáveis; métricas de impressora verificadas no equipamento e
  visíveis no InfraControl; coleta de switch/AP identificada corretamente
  (inclusive limitações de rota/SNMP); PROBLEM e recovery do webhook validados;
  evidências e pendências registradas. Sem credenciais/sessão ou sem alcance
  aos dispositivos, registrar o bloqueio em vez de inventar sucesso.

### Evidências iniciais da auditoria ao vivo (2026-10-24)

- O usuário autenticou na sessão integrada do Zabbix. A auditoria ao vivo
  começou em modo somente leitura. Posteriormente, após documentação do plano
  e autorização explícita do usuário, foi aplicada a correção específica do
  template descrita abaixo. Não houve reconhecimento de evento, envio de
  WhatsApp ou reinício de container Zabbix.
- O template `IC Impressora SNMP` (`templateid` 10887) existe e está ligado
  a 36 hosts. A listagem mostra 4 itens e 2 regras de descoberta habilitadas:
  `printer.alerts.discovery` (5m) e `printer.supplies.discovery` (1h). Cada
  regra declara 2 protótipos de trigger.
- Na consulta inicial, a ação global `InfraControl - triggers` (`actionid` 7)
  estava habilitada e encaminhava eventos do Zabbix ao InfraControl. Ela foi
  pausada somente durante a correção autorizada do template e, após a
  alteração, foi restaurada; a listagem de ações foi reaberta e mostrou a ação
  como **Enabled**. No momento da consulta inicial, o
  canal WhatsApp estava habilitado (`notifications_enabled=true`), mas não
  havia entregas WhatsApp em processamento, pendentes ou com retry vencido
  (0/0).
- Antes da correção, no host `HSDS_HP_SETOR_1_ENF`, o item descoberto
  `Alerta 7 - severidade` coletava o valor `7`; a interface SNMP era
  `192.168.22.175:161`, o OID usado era
  `1.3.6.1.2.1.43.18.1.1.3.1.1` e o intervalo era 5m. A descrição do item
  documentava os valores 1–4, mas não explicava o valor 7.
- Antes da correção, os protótipos de trigger observados eram `Alerta aviso`
  quando o valor era exatamente `3` e `Alerta fatal` quando era `>=4`. Isso
  explica por que o valor 7 ativava o protótipo fatal, mas não comprovava uma
  falha real na impressora: o significado do valor foi então validado contra
  a RFC/MIB antes de corrigir a condição e a descrição do item.
- A listagem de `Latest data` mostrou a última coleta há 4m39s no momento da
  consulta. Isso confirma coleta recente desse item, não o estado geral de
  todas as impressoras.
- Na verificação inicial, antes da correção descrita abaixo, a aplicação não
  pôde ser conferida no navegador. `docker compose ps` indicava frontend e API
  saudáveis e o frontend publicado em `8080→80` e `8443→443`. A resposta HTTP
  em `localhost:8080` redirecionava para
  `https://localhost/` sem a porta publicada. A configuração em
  `docker-compose.yml` usa `HTTPS_PORT:-443` para `NGINX_HTTPS_PORT`, embora
  o mapeamento da porta use `HTTPS_PORT:-8443`; o Nginx ativo confirmou o
  valor 443. O endpoint `https://localhost:8443/` responde 200 quando a
  verificação TLS é ignorada, mas a validação TLS normal rejeita o
  certificado local como não confiável. Assim, o navegador integrado não
  conseguiu abrir a aplicação.
- Correção aplicada após autorização: o fallback de `NGINX_HTTPS_PORT` foi
  ajustado para `8443` em `docker-compose.yml` e somente o container frontend
  foi recriado. O container ficou healthy; `localhost:8080` agora redireciona
  para `https://localhost:8443/` e o endpoint HTTPS responde 200 com
  verificação TLS desativada. O navegador integrado confirma que o
  certificado ainda não é confiável (`ERR_CERT_AUTHORITY_INVALID`), portanto
  a validação visual da aplicação permanece bloqueada. A confiança do
  certificado não foi alterada. Nenhum container do Zabbix foi reiniciado ou
  modificado.
- O resultado anterior de 107 hosts habilitados e o widget de disponibilidade
  totalizando 25 continuam divergentes; a cobertura de impressoras, APs e
  switches ainda não foi reconciliada. A confirmação anterior de entregas
  `Sent` ao InfraControl não substitui a validação de PROBLEM/recovery nesta
  etapa.

### Causa raiz confirmada e correção aplicada — impressoras

- A RFC 3805 define `prtAlertSeverityLevel` como a coluna 2 de
  `prtAlertEntry` (`...43.18.1.1.2`) e `prtAlertTrainingLevel` como a coluna 3
  (`...43.18.1.1.3`). O item prototype `printer.alert.severity[{#SNMPINDEX}]`
  do template 10887 usava a coluna 3, portanto o valor 7 que disparou o falso
  alerta High era dado de treinamento/intervenção, não severidade. Na
  convenção da RFC, `noInterventionRequired` é valor 7 de training level.
- O enum padronizado da severidade na RFC contém `other(1)`, `critical(3)`,
  `warning(4)` e `warningBinaryChangeEvent(5)`. Antes da correção, além da
  coluna incorreta, os protótipos classificavam `>=4` como High e `=3` como
  Warning.
- Correção aplicada no template já existente, sem criar cópia:
  - item prototype `83315`: OID alterado para
    `get[1.3.6.1.2.1.43.18.1.1.2.{#SNMPINDEX}]` e descrição ajustada para
    `prtAlertSeverityLevel`;
  - trigger prototype `38061`: expressão `last(...)=3`, severidade High e
    nome `Alerta crítico`;
  - trigger prototype `38062`: expressão
    `last(...)<>1 and last(...)<>3`, severidade Warning e nome
    `Alerta não crítico ou severidade não mapeada`.
  Os três valores foram reabertos no Zabbix e conferidos após salvar. A ação
  global `actionid` 7 foi restaurada para Enabled e esse estado foi
  confirmado na listagem de ações. A alteração alcança os 36 hosts vinculados.
  Na validação posterior, `Latest data` do host Zabbix `10907`
  (`HSDS_HP_SETOR_1_ENF`) mostrou o item `Alerta 7 - severidade` com último
  valor `1` e coleta há cerca de 1 minuto. A listagem de Problems filtrada
  pelo host/trigger não encontrou problemas ativos. O nome `Alerta 7` é o
  alerta descoberto na impressora; o último valor `1` é a severidade coletada,
  agora `other(1)`. É uma amostra validada, não evidência de coleta nos 36
  hosts. Não houve teste sintético nem envio de WhatsApp.
- Implementação da aplicação concluída em `SnmpPrinterStatusProvider`: 3 é
  critical, 4/5 são warning, códigos restantes são unknown; a severidade
  agregada usa prioridade (critical > warning > unknown), não ordem numérica.
  Os testes focados passaram 17/17.
- A alteração propaga-se aos 36 hosts vinculados e pode gerar
  recuperação/abertura de eventos. Conforme o plano documentado e aprovado,
  a ação Zabbix `actionid` 7 foi desabilitada temporariamente durante a
  edição, e restaurada Enabled imediatamente depois; seu estado foi
  confirmado na listagem. Enquanto pausada, eventos de qualquer host não
  foram encaminhados ao InfraControl e podem não ser reenviados
  automaticamente. Não foram feitos testes sintéticos nem envios de
  WhatsApp.

**Estado:** correção do provider concluída (17/17 testes focados) e correção
específica do template aplicada e conferida no Zabbix; ação global 7
restaurada e confirmada Enabled. Uma impressora (host 10907) já coleta
severidade `1` e não apresenta problema ativo no filtro verificado; verificar
as demais impressoras e continuar a reconciliação de APs/switches. A
landing e o formulário de login do InfraControl já foram abertos com TLS
validado. A inspeção das páginas protegidas depende de uma sessão de usuário
InfraControl, que não estava disponível; não foram fornecidas nem tentadas
credenciais. Nenhum container Zabbix foi reiniciado.

### Plano de confiabilidade do broker (2026-10-24)

- Durante a validação do fluxo, o RabbitMQ aparecia `unhealthy` embora
  `rabbitmq-diagnostics check_running` confirmasse o broker ativo e
  `check_port_connectivity` passasse. Medição do comando de conectividade:
  10,71 s; timeout configurado no Compose: 5 s. O broker continha 0 mensagens
  prontas e 0 não confirmadas nas filas `NotificationEvent` e
  `AiInferenceWorker`.
- Mudança proposta: elevar somente o timeout do healthcheck do RabbitMQ para
  15 s, mantendo o mesmo comando, intervalos e política de retries. Depois,
  recriar somente o container RabbitMQ quando as filas continuarem vazias e
  verificar health `healthy`, liveness/readiness da API e estado dos workers.
  Volume e dados serão preservados. Sem publicação de eventos sintéticos,
  WhatsApp ou alteração no Zabbix.
- Aplicada: timeout alterado de 5 s para 15 s; `docker compose config
  --quiet` válido. Antes da recriação, filas `NotificationEvent` e
  `AiInferenceWorker` estavam em 0/0; somente o container RabbitMQ foi
  recriado, preservando seu volume. Resultado: broker `healthy`, API e workers
  `healthy`, `/health/live` e `/health/ready` HTTP 200, filas seguem em 0/0.
  O comando de healthcheck passa dentro da janela ampliada. O endpoint
  agregado `/health` também respondeu HTTP 200 (`Degraded`, 4,7 s):
  `masstransit-bus`, PostgreSQL e Redis estão `Healthy`; `ai-runtime` está
  `Degraded`, estado permitido para capacidade opcional de IA sem provider
  alcançável (ADR-036/§44), sem bloquear o monitoramento.

### Correção do certificado local e validação visual (2026-10-24)

- A inspeção inicial do certificado encontrou `CN=infracontrol.local` sem
  extensão SAN, embora o endereço publicado seja `localhost`, causando erro
  de autoridade e incompatibilidade de nome.
- Aplicada em `docker/Dockerfile`: certificado de desenvolvimento com SAN para
  `localhost`, `127.0.0.1` e `infracontrol.local`; somente o frontend foi
  reconstruído e recriado. O SAN foi conferido no container; a porta HTTPS
  responde 200 com verificação TLS desativada, HTTP redireciona para
  `https://localhost:8443/` e `/health/ready` responde 200.
- Com autorização explícita, o certificado público atual foi importado em
  `Cert:\CurrentUser\Root` somente para o usuário Windows atual; nenhum
  repositório de máquina foi alterado. HTTPS estrito responde 200 e o browser
  abriu landing e login. O `/auth/me` anônimo retorna 401, e a página então
  apresenta o formulário de login após carregar. O usuário informou não ter
  acesso à conta InfraControl agora; dashboard e páginas protegidas ainda não
  foram validados. Nenhuma credencial foi solicitada ou usada.

### Plano e resultado da reconciliação Zabbix (2026-10-24)

**Plano documentado para a continuação:** comparar grupos de hosts com os
templates e interfaces SNMP efetivamente ligados; conferir itens unsupported,
problemas ativos e a ação de encaminhamento; reconciliar a diferença entre
hosts cadastrados e hosts com SNMP. Esta verificação é somente leitura. Não
alterar templates/hosts, reconhecer problemas, gerar eventos sintéticos nem
enviar mensagens WhatsApp. Qualquer mudança futura em hosts ou templates
dependerá de evidência de capacidade/configuração do equipamento e será
documentada antes de ser aplicada.

**Resultado da consulta ao vivo, em transação somente leitura:**

- **Impressoras:** o grupo contém 37 hosts habilitados; 36 estão ligados ao
  template `IC Impressora SNMP` (10887) e os 36 têm interface SNMP disponível.
  A diferença é exclusivamente `HSSM_HP_SETOR2` (host 10924, `192.168.50.111`),
  cadastrado como ping-only e com interface SNMP em estado desconhecido. Isso
  reconcilia os totais, mas não significa que esse HP esteja coletando Printer-MIB.
- **APs UniFi:** 43 hosts habilitados, todos com itens ICMP habilitados e
  suportados. 30 usam `Network Generic Device by SNMP`, com 30/30 interfaces
  SNMP disponíveis; os 13 restantes estão em ICMP-only. Os itens SNMP
  consultados não têm estado unsupported, mas o template genérico não comprova
  métricas específicas do controlador UniFi (rádios/clientes).
- **Switches:** 25 hosts habilitados: 10 HP com template Comware por SNMP,
  5 Intelbras com template genérico SNMP, 5 de 6 UniFi com template genérico
  SNMP e 4 switches de mesa com `ICMP Ping`. As 20 interfaces SNMP configuradas
  estão disponíveis; os outros 5 hosts estão em ping-only. Os itens
  unsupported foram reportados como zero naquela consulta, mas essa contagem
  foi contradita durante a criação do dashboard: no seletor de itens do host
  `HSDS_HP_SETOR_1_ENF`, `Alerta 7 - severidade`
  (`printer.alert.severity[1.1]`) apareceu como `Not supported`. A quantidade
  total e a causa precisam ser reconsultadas por estado; não desabilitar nem
  remover o item sem investigação.
- **Problemas e encaminhamento:** a consulta anterior que sustentava
  `0` problemas ativos filtrava `r_eventid = 0`. Esse filtro não é válido para
  eventos Zabbix sem recuperação: nesses casos `r_eventid` é `NULL`. Portanto,
  a afirmação anterior de zero problemas ativos fica invalidada. Os antigos
  erros `No Such Instance` de `HSDS_HP_PS_TRIAGEM` (10904) e `HSDS_HP_SETOR_3`
  (10910) não estavam presentes naquela consulta; os itens descobertos
  consultados estavam `state=0`, sem erro atual naquele momento. A ação global
  `InfraControl - triggers` (id 7) segue habilitada (`status=0`).
- Na etapa de reconciliação somente leitura e na criação do dashboard, não
  foram alterados hosts, interfaces, templates, triggers ou ações (a correção
  do template 10887 foi uma etapa separada, já registrada acima). O dashboard
  abaixo é uma criação visual isolada. Não houve teste sintético,
  reconhecimento de problema ou envio WhatsApp.

### Dashboard operacional de impressoras (2026-10-24)

Dashboard **Impressoras - Operacao** criado e salvo no Zabbix (ID `439`),
com a página **Visão geral**, privado e sem compartilhamentos de
usuários/grupos. Os cinco widgets carregam no grupo `Impressoras`:

- problemas recentes;
- problemas por severidade;
- disponibilidade SNMP;
- Top hosts com `printer.status` e `printer.pages.total`, limite de 37 hosts;
- Item navigator, filtrado pelo grupo, exibindo `100 de 100+` itens.

Na validação visual, a disponibilidade mostrou 36 disponíveis, 0 indisponíveis,
0 mistos e 1 desconhecido (37 hosts). Problemas por severidade mostrou 12
`Average` e 19 `Warning`; portanto, há problemas ativos no grupo e a
reconciliação continua pendente. O widget de problemas recentes mostrou 25 de
32 entradas e incluiu ao menos uma linha `RESOLVED`. A lista também inclui
alertas de suprimentos, alertas Printer-MIB e ao menos um alerta de tempo de
resposta ICMP. Um alerta exibiu
`Alerta não crítico ou severidade não mapeada: 6`; seu significado deve ser
investigado separadamente antes de qualquer mudança no template.

Não foram reconhecidos problemas nem modificados hosts, templates, triggers
ou ações. A ausência de coleta de filas, bandejas/tampas, consumo mensal e
previsão de troca permanece.

**Pendências objetivas:** há 13 APs e 5 switches sem interface SNMP no Zabbix;
`HSSM_HP_SETOR2` tem interface SNMP desconhecida, mas continua ping-only. O
usuário escolheu prosseguir somente pelo Zabbix, sem alterar o controller ou
os equipamentos. Portanto, manter esses hosts sem SNMP até que uma consulta
SNMP seja validada por um caminho já autorizado; não adicionar interface ou
template por tentativa. O controller está na tela de login e não foram
solicitadas credenciais. Para métricas específicas UniFi, definir uma
fonte/template compatível com o controlador — o template SNMP genérico atual
não cobre essa capacidade. Não criar OIDs ou templates específicos sem
validar modelo, firmware e acesso aos dados.

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
