# 01 — Arquitetura da camada de IA

> Espelho executivo do diagnóstico `docs/35` (Sprint 0) e dos ADRs
> 036–041. Atualizado a cada entrega da Fase 0.

## Estado atual (S1–S8 entregues; zero chamada a LLM)

- 7 projetos .NET 10 (Clean Architecture + DDD + CQRS/MediatR +
  Vertical Slice + Modular Monolith, ADR-001/006/007/031) com módulos
  como pastas em `Application/` (16 módulos) e front React+TS+Vite.
- **Sprint 0 (docs/35 §1): zero código AI/LLM/MCP/RAG** — nada a
  desfazer. Desde então: ADRs 036–041 (2026-10-08) e o **S3**
  criou os primeiros contratos + `AiGateway` em `Application/AI/`.
- **S4 (2026-10-08; DI completado 2026-10-09)**: ConfigurationProviderRegistry
  (v1 por config), OllamaProvider (HTTP puro sem SDK), AiProviderRouter
  (mapeamento por capacidade), model discovery GET /api/tags, teste de
  conexão, DI, appsettings, testes unit. Em 09/10 o DI foi fechado:
  `IAiGateway` + `IAiAuditSink` (LoggingAiAuditSink → log estruturado)
  + limites por chamada em `Ai:Gateway` (§55). Nenhuma chamada a LLM
  existe ainda; a integração gateway↔Ollama é pendência de ambiente
  (§12).
- **S5 (2026-10-09)**: runtime health (§44) — capacidades sem
  mapeamento = Degraded "não configurado", teste de conexão §13 por
  provider, `AiRuntimeHealthCheck` no `/health` (tag `ai`; saudável
  sem Ollama) — e observabilidade (§43) — `AiDiagnostics`
  "InfraControl.AI": activity `ai.complete` com tags
  capability/provider/model/fallback/trace + métricas
  `ai.requests`/`ai.request.duration`/`ai.tokens.*` capturadas pelo
  OTel existente.
- **S6 (2026-10-09)**: segurança LEVEL 0 (ADR-040) — `AuditAiAuditSink`
  persiste UMA entrada `AI.{capacidade}` por request na auditoria de
  domínio existente (ator §41 via `Actor` do request, motivo de
  fallback §54, trace §43) e o gateway aplica os limites §55
  (`MaxPromptCharacters` recusado auditado; `MaxCompletionTokens` →
  `num_predict`).
- **S7 (2026-10-09)**: MCP foundation — `IAiTool`/registry/executor
  in-process (autorização §41 pela hierarquia RoleAccess do pipeline,
  timeout §52, auditoria `AI.Tool.*`) + as duas primeiras tools de
  leitura (`get_active_incidents`, `search_knowledge`) sobre queries
  MediatR existentes. Servidor MCP externo = ADR-039 (futuro).
- **S8 (2026-10-09)**: Context Engine + RAG foundation (ADR-038) —
  contratos (`IEmbeddingProvider`/`IVectorStore`/`IRetrievalService`/
  `IReranker`/`IContextBuilder`), busca híbrida v1 (vetor+keyword →
  merge → rerank heurístico; degrada p/ keyword §44 sem pgvector),
  `EvidenceSet` §19 (só `Probable`, nunca `Confirmed`), chunker de
  Knowledge (limite 1500 chars documentado), ingesta de artigo
  publicado (command já chamável) e `OllamaEmbeddingProvider`
  (`POST /api/embed`, `Ai:Ollama:EmbeddingModel`). **Pendências**:
  avaliação da imagem pgvector (`postgres:17-alpine` não a embute) +
  `IVectorStore` no DI + trigger de ingesta na publicação — todas
  condicionadas a ambiente com Docker.
- Infra pronta para a IA: health checks (`/health/*`), OpenTelemetry +
  collector, Serilog/Seq, SignalR, auditoria persistida (F0-T014),
  autorização por roles (ADR-016), PostgreSQL 17, Redis, RabbitMQ.
- Compose único com profiles (ADR-022): `infra`, `app`, `obs`,
  `zabbix` — e agora `ai` (ADR-041).

## Arquitetura-alvo (§3)

```text
  ITSM · INVENTORY · MONITORING · KNOWLEDGE   (módulos existentes)
                    │
              AI GATEWAY  (ADR-036)  ← único caminho p/ LLM
                    │
            AI CONTEXT ENGINE (ADR-038)
        ┌───────────┼───────────┐
       RAG        TOOLS        LLM (ADR-037: Ollama → futuros)
   pgvector    registry in-     provider router,
   +embeddings process (ADR-039) capability, fallback
        └───────────┼───────────┘
                    │
     Copilot · Investigation · Knowledge · Reporting · Automation
   (Fases 1–5 — todas LEVEL 0/1 inicialmente, ADR-040)
```

## Decisões registradas

| ADR | Decisão |
|---|---|
| [ADR-036](../adr/ADR-036-AI-LAYER-AND-GATEWAY.md) | Camada de IA em pastas por convenção (`Application/AI`, `Infrastructure/AI`, `Api/Controllers/AI`) — sem csproj novo; gateway é o único caminho para LLM |
| [ADR-037](../adr/ADR-037-AI-PROVIDER-AND-MODEL-MANAGER.md) | `IAiProvider` + `OllamaProvider` (HTTP sem SDK); config v1 por env, evolução para banco+UI atrás de `IProviderRegistry`; modelos por capacidade + fallback |
| [ADR-038](../adr/ADR-038-AI-CONTEXT-ENGINE-HYBRID-RAG.md) | pgvector no Postgres atual; contratos `IEmbeddingProvider`/`IVectorStore`/`IRetrievalService` híbrido + `EvidenceSet` (§19); fonte inicial = Knowledge publicado |
| [ADR-039](../adr/ADR-039-MCP-AND-TOOL-CONTRACTS.md) | Tools in-process (`IAiTool` + registry) executando application services; servidor MCP = evolução; checklist de aceite por tool |
| [ADR-040](../adr/ADR-040-AI-SECURITY-AND-AUTONOMY.md) | LEVEL 0 (Read Only); `LLM → Tool → Policy → Permission`; permissões = RoleAccess existente; conteúdo recuperado = dado; audit `AI.*` |
| [ADR-041](../adr/ADR-041-AI-DOCKER-RUNTIME.md) | Serviço `ollama` no compose único sob `profiles: [ai]`; `http://ollama:11434` por DNS de serviço; evidência pendente de ambiente |

## Pipeline de uma chamada (ADR-036 §2)

```text
capability (§15) → política/limites (ADR-040/§55) → contexto
(ADR-038, se houver) → provider (ADR-037) → normalização
→ audit + telemetry (provider, modelo, latência, tokens, traceId)
→ fallback (§54) se erro/timeout
```

## Onde o código vai nascer (ADR-036)

| Pasta | Conteúdo |
|---|---|
| `src/InfraControl.Application/AI/` | `IAiGateway`, `IAiProvider`, policies, capabilities, context engine, tool contracts |
| `src/InfraControl.Infrastructure/AI/` | `OllamaProvider`, pgvector store, embedding provider — único `HttpClient` de IA |
| `src/InfraControl.Api/Controllers/AI/` | endpoints do Copilot/health de IA |
| `tests/…` | unit por contrato; integração com Ollama quando houver ambiente |

## Dependências de infraestrutura (ADR-041)

- `docker compose --profile ai up -d ollama` → porta 11434, volume
  `ollama_data`, healthcheck `GET /api/tags`.
- Env: `OLLAMA_BASE_URL`, `OLLAMA_PORT`, `OLLAMA_MODEL`
  (`qwen2.5-coder:1.5b`, §11), `OLLAMA_EMBED_MODEL` (vazio — escolha do
  ADR-038/S8).

## Pendências de ambiente (bloqueiam o §63)

Sem Docker neste workstation: `docker compose config`/`up`, health
check do Ollama, pull do modelo, evidência de geração real, testes de
integração e as 12 migrações pendentes (`docs/35` §12/§13). Enquanto
isso, tudo que é código/teste/docs segue avançando — mas **nada é
dado como concluído sem execução** (§46).
