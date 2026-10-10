# Plano — AI Foundation (Fase 0) · Investigação Sprint 0

> Entrega da etapa de investigação da AI Blueprint (`docs/34`, §66:
> *"NÃO implemente nada ainda … Execute somente investigação e
> planejamento nesta etapa"*). Este documento responde aos itens A–J da
> §66 e traz diagnóstico, arquitetura atual, gaps, dependências, plano
> técnico, plano Docker, ADRs necessários, ordem de execução e
> critérios de aceite. **Nenhum código de IA foi escrito nesta etapa.**
> Data: 2026-10-07 · Próxima fase autorizada: **Fase 0 — AI Foundation**
> · Numeração de ADRs segue a do repo (próximo: **ADR-036**).

## 1. Arquitetura encontrada (§66 A)

### Solução (7 projetos .NET 10, `InfraControl.slnx`)

- `Domain` · `Application` · `Infrastructure` · `Api` · `Workers` ·
  `BuildingBlocks` · `Contracts` — Clean Architecture + DDD + CQRS
  (MediatR) + Vertical Slice + Modular Monolith (ADR-001, 006, 007,
  031).
- Módulos (pastas de `Application`): Alerts, Devices, Discovery,
  Identity, Incidents, Integrations, Inventory, Knowledge, Monitoring,
  Network, Notifications, Operations, Pdi, RemoteAccess, Sla, Tasks.
- Front: React + TypeScript + Vite em `frontend/InfraControl.Web`
  (catálogos pt-BR em arquivos únicos — `taskCatalogs`,
  `knowledgeCatalogs`; MVC **sem** `JsonStringEnumConverter` → enums
  numéricos no JSON, nomes só via DTO que já faz `.ToString()`).

### Infraestrutura e configuração (§66 itens 8–24)

| Item | Estado |
|---|---|
| PostgreSQL 17 | ADR-002; `postgres` no compose; **candidato a pgvector** (§35) |
| Redis 7 | ADR-003 (`hybrid cache`, ADR-010) |
| RabbitMQ/MassTransit + Outbox | ADR-004/005; consumers em `Workers` |
| Identity/sessions/MFA/CSRF | ADR-008, 017, 018, 019, 020 |
| Roles/Permissions | ADR-016 (`RoleAccess`, policies `Read`/`Operate`/`Administer`) — **base dos guardrails da IA (§41)** |
| Serilog → Seq | ADR-021; rota Seq atrás do nginx (basic auth) |
| OpenTelemetry | ADR-009; `AddOpenTelemetry` + serviço `otel-collector` no compose (porta 4317) |
| SignalR | `AddSignalR` no `Program.cs` — canal de notificação pronto p/ Copilot |
| Health checks | **já existem**: `AddHealthChecks` + `/health`, `/health/ready`, `/health/live`, `/health/startup` (`Program.cs` 196/273–304) — ponto de encaixe do *runtime health* da IA (§44) |
| Zabbix | compose `profiles: [zabbix]` + webhook inbound (ADR-023) + ingestion (ADR-027); token via `Integrations__Zabbix__Token` |
| UniFi | client REST via env (ADR-028/F20) |
| n8n | **só outbound** (F19-T007/ADR-028 §6 — `HttpWebhookNotificationDispatcher`); n8n não roda no compose (é externo) — mesmo desenho p/ Fase 5 |
| Docker | **compose único com profiles** `infra`/`app`/`obs`/`zabbix` (ADR-022), `docker/Dockerfile` multi-target, healthcheck por serviço |

### Código AI/LLM/MCP/RAG existente (§66 itens 29–31)

**Nenhum.** Grep por `OpenAI|Ollama|SemanticKernel|ModelContextProtocol|
pgvector|Pinecone|Weaviate|Chroma|Embedding|IChatClient` em `src/` →
0 ocorrências. `docs/ai/` (§60) ainda não existe. Zero dívida
arquitetural de IA para desfazer — e também zero reuso de código de IA.

## 2. Componentes reutilizáveis (§66 B)

- **Autorização/policies** (ADR-016) + `ICurrentUserContext` → o tool
  calling herda o mesmo corte de permissão (§41: *Viewer não acessa
  via IA o que não acessa direto*).
- **Health checks** existentes → acrescentar checks de IA degradados
  (`not configured` sem ambiente), sem novo framework.
- **OpenTelemetry + otel-collector** → spans/attributes de IA (§43)
  entram no pipeline atual.
- **Audit** (`AuditReader`/entries) → as execuções de IA viram entradas
  de auditoria no mesmo formato.
- **SignalR** → avisos do Copilot/investigação futuros.
- **PostgreSQL 17** → avaliar `pgvector` antes de qualquer banco
  vetorial novo (regra da própria §35 e §6 "reutilizar antes de
  criar").
- **Módulo Knowledge** (artigos, categorias, versões ADR-034, origens
  K4, workflow de status) → primeira fonte do RAG.
- **Tasks/Incidents/Inventory/Pdi/Sla/Monitoring** → leitura por trás
  das tools (§21).
- **Pipeline MediatR/IQuery** → porta de entrada natural das tools
  (*tool → application service*, §20).
- **Compose com profiles** (ADR-022) → a IA entra como `profiles: [ai]`.
- **`.env.example`** → configuração de providers/modelos por lá na v1.

## 3. Componentes inexistentes (§66 C)

AI Gateway · Provider abstraction (`IAiProvider`) · Provider & Model
Manager · serviço Ollama · Model discovery · AI Context Engine ·
embeddings · vector store · reranker · context builder · evidence
model · MCP (server/tools/registry) · Investigation Engine ·
Knowledge Engineer (geração) · Reporting com IA · políticas/limites de
custo de IA · Evaluation harness · prompt registry.

## 4. Integrações existentes (§66 D)

Zabbix (in + compose), UniFi (REST), n8n (POST de notificação),
canais de notificação (Email/SignalR/Telegram/WhatsApp/Webhook/Slack),
Google login. Nenhuma toca em IA; todas continuam intactas.

## 5. Docker existente (§66 E)

- Arquivo único `docker-compose.yml` (raiz) com **profiles**
  (`infra` sem profile; `app` = api/workers/frontend; `obs` = seq +
  otel-collector; `zabbix` = 4 serviços).
- Rede única `infracontrol` (bridge) — DNS interno por **nome de
  serviço** (`api`, `seq`, `otel-collector`), exatamente o que a §11
  exige para `http://ollama:11434`.
- Volumes nomeados (`postgres_data`, `redis_data`, `rabbitmq_data`,
  `seq_data`, `zabbix_*`); healthcheck declarado em cada serviço.
- Portas em uso hoje: 5432, 6379, 5672/15672, 8082, 8080/8443/5341,
  4317, 5433/10051/8086/10050 — **11434 (Ollama) está livre**.
- `docker/Dockerfile` (targets api/workers/frontend),
  `docker/otel-collector.yaml`, `docker/nginx.conf`.

## 6. Dependências a adicionar (§66 F)

1. Serviço **Ollama** no compose (profile `ai`) + volume `ollama_data`
   + modelos (LLM + embedding) baixados na primeira subida.
2. Cliente HTTP p/ Ollama/OpenAI-compatible (HttpClient + JSON; ou
   abstração `Microsoft.Extensions.AI` — **decisão do ADR-037**; nada
   de SDK espalhado, §9/§10).
3. **pgvector** no Postgres existente, se a avaliação do ADR-038
   aprovarmos (§35 manda avaliar o banco atual antes de novo banco).
4. **MCP SDK** (quando o ADR-039 definir a forma de exposição).
5. Nenhuma dependência de domínio nova sem ADR.

## 7. Gaps e conflitos com a blueprint (§66 G)

1. **Docker indisponível neste posto de trabalho** (decisão do usuário
   — "não roda o docker"). As regras §47/§48/§63 (*compose config*,
   `up`, health check, evidência de execução) **não são executáveis
   aqui** — mesmo estado das 12 migrações não aplicadas. Mitigação:
   preparar config + validar por revisão/lint, marcar DoD como
   "pendente de ambiente" e executar quando houver Docker. A §46 é
   clara: documentação ≠ evidência — **nada disto pode ser dado como
   concluído sem execução**.
2. **Numeração de ADRs**: a §59 sugere `ADR-001-ai-layer.md` … mas o
   repo já está em **ADR-035** (a própria §59 manda verificar antes de
   criar) → ADRs de IA continuam de **ADR-036**. Observação de
   housekeeping: existem dois arquivos `ADR-033-*` (numeração
   duplicada pré-existente) — não bloqueia (próximo número = 036).
3. **ADR-035 × §33**: coerentes — o rascunho de Knowledge nasce de
   montagem determinística e toda publicação passa por revisão humana;
   a Fase 3 (*Knowledge Engineer*) entra como **evolução** que passa a
   gerar rascunhos com IA mantendo o mesmo workflow (§33: AI Generated
   → Human Review → Published). Registrar em ADR-036/038.
4. **§49 pede arquivos de compose múltiplos; o ADR-022 decide compose
   único com profiles** → a blueprint também manda "antes de criar
   outro Compose, verificar os arquivos existentes" — nosso caminho é
   **`profiles: [ai]`**, não novo arquivo.
5. **§63/§67 DoD com Docker**: itens de execução ficam explicitamente
   pendentes de ambiente (ver §12/§13 deste documento), sem virar
   falso "concluído" (§46).

## 8. Riscos (§66 G)

- **Execução sem ambiente** (alto) → escopo da Fase 0 dividido em
  sprints com checkpoint; tudo que exige container fica documentado e
  bloqueado, não improvisado.
- **Capacidade do modelo local 1.5B** (§11 usa `qwen2.5-coder:1.5b`
  como exemplo) → ADR-037 define modelos por capacidade + fallback;
  começar com tarefas de baixo risco (Copilot de leitura, LEVEL 0).
- **Prompt injection** (§42) → todo conteúdo recuperado tratado como
  dado; política no ADR-040 antes de qualquer tool de escrita.
- **Escopo da Fase 0 é enorme** (§8: gateway + providers + context +
  RAG + MCP + segurança + observabilidade + avaliação) → sprints
  pequenos, uma task por fatia, nenhum sem teste.
- **Baseline sujo**: 12 migrações pendentes atrapalam os testes de
  integração da IA (que serão o DoD da Fase 0) → resolver quando o
  ambiente de teste existir.
- **Custo** (§55): Ollama local = custo direto zero; limites de
  tokens/ferramentas continuam obrigatórios desde o primeiro request.

## 9. ADRs necessários (§66 H — de ADR-036 em diante)

| # | Tema | Decisões que carrega |
|---|---|---|
| ADR-036 | **AI Layer & Gateway** | onde a IA vive no monólito (pastas/namespaces `AI` em Application/Infrastructure, sem módulo novo isolado), contratos (`IAiProvider`, gateway), pipeline (policy → context → llm → audit), relação com ADR-035/§33 |
| ADR-037 | **Provider & Model Manager** | abstração + Ollama primeiro, config (v1: `.env`/appsettings vs tabelas), descoberta de modelos, modelos por capacidade (Copilot/Analyst/…), fallback (§54) |
| ADR-038 | **Context Engine & Hybrid RAG** | pgvector no Postgres atual × banco dedicado (§35), embeddings, busca híbrida + reranking, evidence model (§19), fontes (Knowledge primeiro) |
| ADR-039 | **MCP & Tool Contracts** | como MCP é exposto (em processo × sidecar), contrato/autorização/timeout/limite das tools (§52), ferramentas iniciais (§21) |
| ADR-040 | **AI Security & Autonomy** | LEVEL 0 de partida (§39), guardrails `LLM→Tool→Policy→Permission→Approval` (§40), prompt injection (§42), auditoria (§41) |
| ADR-041 | **AI Docker Runtime** | profile `ai`, serviço `ollama` (imagem, volume, porta, healthcheck), modelos via pull, `.env` (`OLLAMA_BASE_URL=http://ollama:11434`), evidência pendente de ambiente |

Observabilidade de IA (§43/§44/§55) nasce dentro de ADR-036+041
(ActivitySource + health check + tags de custo); se crescer, vira
ADR-042.

## 10. Plano Docker (§47–§48)

Preparação (sem executar — ambiente sem Docker):

1. Novo **`profiles: ["ai"]`** no `docker-compose.yml` existente:
   serviço `ollama` (`ollama/ollama`, volume `ollama_data`, porta
   `${OLLAMA_PORT:-11434}:11434`, healthcheck `GET /api/tags`, rede
   `infracontrol`, `restart: unless-stopped`) — seguir o padrão já
   usado nos demais serviços.
2. `.env.example`: `OLLAMA_BASE_URL` (interno
   `http://ollama:11434`), `OLLAMA_PORT`, `OLLAMA_MODEL`,
   `OLLAMA_EMBED_MODEL` (defaults do ADR-037).
3. Ordem de validação **quando houver ambiente** (literal §48):
   `docker compose config` → `docker compose --profile ai up -d ollama`
   → `ps`/`logs --tail=100` → `docker exec … ollama pull <modelo>` →
   `GET /api/tags` → `GET /api/generate` → healthcheck verde → só
   então subir a aplicação apontando para `http://ollama:11434`.
4. Validação possível **hoje**: revisão do arquivo + conferência de
   portas/networks/volumes (§5 checklist) — `docker compose config`
   exige a CLI; **evidência marcada como pendente** (§46).

## 11. Ordem exata de implementação (§66 J / §62)

```text
Sprint 0 — investigação e planejamento  ……… ESTE DOCUMENTO (docs/35)
   ↓
S1 — ADRs 036–041 (aprovados pelo humano — arquitetura exige ADR)
      ………………………………………………………………… CONCLUÍDO 2026-10-08 (FAI-T001)
   ↓
S2 — Compose profile `ai` + .env.example + docs/ai/00..01 (config
     preparada; execução/health check pendentes de ambiente)
      ………………………………………………………………… CONCLUÍDO 2026-10-08 (FAI-T002/T003)
   ↓
S3 — AI Gateway mínimo (IAiProvider, router, timeout/retry/fallback,
     hook de audit) + testes unit
      ………………………………………………………………… CONCLUÍDO 2026-10-08 (FAI-T004)
   ↓
S4 — Provider & Model Manager v1 (config por env/appsettings conforme
     ADR-037; admin UI só se o ADR exigir já)
      ………………………………………………………………… CONCLUÍDO 2026-10-09 (FAI-T005:
      registry/router/OllamaProvider + DI completo com IAiGateway,
      LoggingAiAuditSink e Ai:Gateway — unit 1930 · arch 119)
   ↓
S5 — Runtime health (checks de IA com estado "não configurado") +
     observabilidade (ActivitySource AI, tags §43)
      ………………………………………………………………… CONCLUÍDO 2026-10-09 (FAI-T006:
      AiRuntimeHealthService + AiRuntimeHealthCheck no /health com tag
      "ai" — sempre Healthy/Degraded; AiDiagnostics "InfraControl.AI"
      com activity ai.complete + métricas requests/duration/tokens)
   ↓
S6 — Segurança LEVEL 0 (policy, auditoria de requests, limites §55)
      ………………………………………………………………… CONCLUÍDO 2026-10-09 (FAI-T007:
      AuditAiAuditSink persiste AI.* na auditoria existente (§41 ator,
      §54 motivo de fallback) + limites §55 no gateway — prompt recusado
      auditado e MaxCompletionTokens → num_predict)
   ↓
S7 — MCP foundation (contratos internos + registro de tools §21;
     exposição conforme ADR-039)
      ………………………………………………………………… CONCLUÍDO 2026-10-09 (FAI-T008:
      IAiTool/registry/executor (autorização §41 + timeout §52 +
      auditoria AI.Tool.*) + 2 tools de leitura sobre queries MediatR
      existentes — get_active_incidents, search_knowledge)
   ↓
S8 — Context Engine + RAG foundation (avaliação pgvector, embedding
     provider, busca híbrida esqueleto, evidence model)
      ………………………………………………………………… CONCLUÍDO 2026-10-09 (FAI-T009: contratos
      ADR-038 + híbrida v1 vetor+keyword com degradação §44, rerank
      heurístico, EvidenceSet §19 (nunca Confirmed), chunker 1500,
      ingesta de Knowledge publicado; OllamaEmbeddingProvider /api/embed;
      pgvector = pendência de Docker — IVectorStore fora do DI)
   ↓
S9 — Evaluation foundation (harness de avaliação §45)
      ………………………………………………………………… CONCLUÍDO 2026-10-09 (FAI-T010: harness +
      checks tool-selection/authorization/grounding sem LLM; cenário
      real = Fase 1 — docs/ai/02)
   ↓
DoD §63 + critério §67 (Fase 0 → Fase 1)  →  checkpoint
   ↓
Fase 1 — AI COPILOT (autorização nova)
```

Cada Sx vira task(s) no `TASKS.md` na autorização da Fase 0 — nada de
implementar várias fases sem autorização (regra do repo).

## 12. Critérios de aceite (§63 + §67)

**Executáveis sem Docker** (agora): código + testes unit/arch ·
políticas/permissions testadas · prompts documentados (`docs/ai/13`) ·
tools documentadas (`docs/ai/05`) · `docs/ai/` 00–15 criados ao longo
das fases · ADRs 036–041 · config `compose`/`.env` preparada e
revisada · evaluation test esqueleto · UI quando aplicável.

**Pendentes de ambiente (Docker)** — bloqueiam o "Fase 0 concluída"
segundo §46/§67: `docker compose config` · `up` do Ollama · health
check verde · pull de modelo · evidência de geração real · integração
AI Gateway↔Ollama · testes de integração · migração do pgvector (se
aprovada) · 12 migrações pendentes.

## 13. Pendências de ambiente (globais)

- Docker indisponível → §12 acima + testes de integração + 12
  migrações não aplicadas.
- Push para `origin/main` pendente de autenticação (13+ commits
  locais após a Fase 24).

## 14. Gate Fase 0 → Fase 1 (DoD §63 + critério §67 — avaliação 2026-10-09)

Os sprints S1–S9 estão **todos implementados** (código + testes +
docs + commit local por task). O gate está **aberto com pendências de
ambiente**: segundo §46/§67, "documentação não é evidência" — os
itens marcados ⚠️ exigem Docker/Ollama/pgvector para virarem evidência
de execução.

### DoD §63 (funcionalidade de IA)

| Item | Estado | Evidência |
|---|---|---|
| Código | ✓ | S1–S9 (`src/*/AI/`) |
| Testes | ✓ | unit **1981/1981** (suite AI: 82) · arch **119/119** |
| Autorização | ✓ | tools: RoleAccess do pipeline (§41) + gate `IAuthorizedRequest` |
| Logs | ✓ | `AuditAiAuditSink` + `Logging` por chamada/tool |
| Telemetria | ✓ | `AiDiagnostics` `InfraControl.AI` (activity + métricas §43) |
| Tratamento de erros | ✓ | exceções pt-BR, degradação §44, fail-open de auditoria |
| Documentação | ✓ | docs/35 §11–§14, docs/ai/01–02, ADR-036–041, TASKS |
| UI | n/a | Fase 0 sem UI (Copilot = Fase 1) |
| Auditoria | ✓ | `AI.{capacidade}` (§41/§54) + `AI.Tool.{nome}` (§41) |
| Prompt documentado | n/a | nenhum prompt de produto existe ainda (sem LLM) |
| Tool documentada | ✓ | descriptor §52 por tool + docs/ai/01 |
| Evaluation test | ✓ (base) | harness + 3 checks sem LLM; cenário real = Fase 1 |
| Docker validado | ⚠️ | sem Docker no workstation (§13) |
| Health check | ✓ | `ai-runtime` no `/health` (tag `ai`; degradado sem Ollama) |
| Evidência de execução | ⚠️ | geração real Ollama/integração = pendente de ambiente |

### Critério §67 (Fase 0 → Fase 1)

| Exigência | Estado |
|---|---|
| AI Gateway funcionando | ✓ (testes) · ⚠️ integração real |
| Provider Manager funcionando | ✓ (registry/router/testes) |
| Ollama funcionando | ⚠️ pendente de ambiente |
| Modelo testado | ⚠️ pendente de ambiente |
| Runtime Health funcionando | ✓ (degradado sem Ollama, saudável com) |
| Audit funcionando | ✓ (auditoria de domínio F0-T014) |
| Observabilidade funcionando | ✓ (collector padrão captura `InfraControl.AI`) |
| Permissions funcionando | ✓ (RoleAccess/ADR-016 na camada de tools) |
| MCP foundation funcionando | ✓ (registry + executor + 2 tools; server = ADR-039) |
| Context Engine funcionando | ✓ (híbrida degradada) · ⚠️ vetorial |
| RAG foundation funcionando | ✓ contrato+v1 · ⚠️ pgvector (avaliação de imagem) |
| Tool contracts funcionando | ✓ (§52 completo) |
| Evaluation foundation funcionando | ✓ (harness; cenário real = Fase 1) |
| Docker validado | ⚠️ §13 |
| Testes passando | ✓ 1981 unit + 119 arch |

**Decisão**: Fase 0 implementada por completo; o avanço efetivo para a
Fase 1 fica condicionado às pendências de ambiente da §13 (Docker →
Ollama real → pgvector → migrações → integração gateway↔Ollama).
