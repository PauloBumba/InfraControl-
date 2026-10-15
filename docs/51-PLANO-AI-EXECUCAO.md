# 51 — Plano de execução da IA (Fases A–F) — registro e evidências

- **Status**: CONCLUÍDO (2026-10-14)
- **Escopo**: execução do roadmap de IA aprovado (blueprint
  `docs/34`, fundação `docs/35`) — funcionalidades de IA presentes na
  plataforma, uma fase por vez, com checkpoint por fase.
- **Decisões do solicitante**: (1) execução fase a fase com checkpoint e
  commit por fase; (2) validação **mock agora** (`HttpMessageHandler` /
  mocks — live depois no ambiente do usuário, sem Docker neste ambiente —
  `docs/35` §46: documentação ≠ evidência); (3) Fase F =
  **somente LEVEL 1 Recomendação** (sem escrita autônoma); (4) push ao
  terminar tudo.

## 1. Fases executadas

| Fase | ADR | Commit | Escopo | Validação |
| --- | --- | --- | --- | --- |
| A — Provedores externos | ADR-050 | `05ca20a`* | Registry por nome, router com fallback (`AiRoutingModelOptions`), `OpenAiCompatibleProvider` + `GeminiProvider`, DI/HttpClient, appsettings/compose/`.env.example`, 42 testes de providers | AI 128/128 |
| B — Copilot | ADR-052 | `19cd493` | `SendAiChatCommand` (LEVEL 0, evidências `[E1]…`), `POST /ai/chat`, 503/502 no `GlobalExceptionHandler`, CopilotPage + rota `/ai/copilot`, 14 testes | AI 128/128 · unit 2344→ · vitest 3/3 · tsc 0 |
| C — Investigation | ADR-053 | `3b2d40f` | `InvestigateIncidentCommand` (Operate) grounded no incidente + Context Engine, tool `get_incident`, `POST /incidents/{id}/investigate`, painel na IncidentesPage, 15 testes | AI 139/139 · unit 2372 · arch 139/140 · vitest 6/6 |
| D — Knowledge Engineer | ADR-054 | `7245886` | `GenerateIncident/TaskKnowledgeDraftCommand` (Operate), capacidade `KnowledgeGeneration`, JSON `{title,content}` com fallback, **Draft por domínio** + origem K4 + versão com autoria IA, 2 rotas + botão na IncidentesPage, 12 testes | AI 146/146 · unit 2402 · arch 146/147 · vitest 7/7 |
| E — Reporting | (docs/34 §37) | `1d95d34` | `SummarizeTransversalReportCommand` (Operate) sobre o read-model ADR-049, capacidade `Reporting`, `POST /reports/transversal/ai-summary`, botão+painel no ReportsPage, 9 testes | AI 151/151 · unit 2407 · arch 148/149 · vitest 5/5 |
| F — Automation LEVEL 1 | ADR-055 | `ec70664` | `GenerateAutomationRecommendationsCommand` (Operate): sinais = top 8 de incidentes ativos + knowledge, **sem sinais = sem LLM**, capacidade `Automation` com prompt LEVEL 1, JSON `{items}` com prioridade normalizada, bloco no CopilotPage, 13 testes | AI 159/159 · unit 2415 · arch 152/153 · vitest 6/6 |

\* Fase A foi contaminada por um commit paralelo ("update" `05ca20a`) que
varreu o worktree — o conteúdo está no repo; histórico não foi reescrito
(política: não mexer no commit alheio).

## 2. Validação global (fim das 6 fases)

```text
dotnet build InfraControl.slnx                     → 0 erros
dotnet test tests/InfraControl.UnitTests           → 2415/2415 (filtro AI: 159/159)
dotnet test tests/InfraControl.ArchitectureTests   → 152/153 (1 falha pré-existente S4: Nginx_Should_Proxy_The_Api_Health_Checks — não é da IA)
npx tsc --noEmit -p tsconfig.json                  → 0 erros (frontend)
vitest (telas tocadas: Copilot/Incidents/Reports)  → 18/18
```

Sem provider configurado, todos os endpoints de IA devolvem **503
amigável** (casca degradada §44/§57) — nunca alucinação.

## 3. Mapa de superfícies novas

- **API**: `POST /ai/chat` · `POST /incidents/{id}/investigate` ·
  `POST /incidents/{id}/generate-knowledge` ·
  `POST /tasks/{taskId}/generate-knowledge/ai` ·
  `POST /reports/transversal/ai-summary` ·
  `POST /automation/recommendations` (todas `Operate`, exceto chat Read).
- **UI**: `/ai/copilot` (chat + assistente de automação), IncidentesPage
  (Investigar + Artigo (IA)), ReportsPage (Resumir com IA).
- **Tools MCP**: `get_incident` (nova) alongside `get_active_incidents` /
  `search_knowledge`.
- **ADRs**: 050, 052, 053, 054, 055 (o 051 é de outra sessão — UniFi).

## 4. Pendências

1. **Smoke live** no ambiente do usuário (Docker profile `ai`, chaves em
   `.env` — nunca no repo) com Ollama ou provedor externo real.
2. **LEVEL 2+** (aprovação + execução via tool explícita com
   Policy/Permission/Approval/Audit) — exige ADR próprio (ADR-040/§40).
3. Falha arch pré-existente do nginx (S4) — fora do escopo da IA.
4. `TASKS.md` / `PROJECT_STATE.md` pertencem à sessão paralela
   (UniFi/SNMP) — este registro vive aqui justamente por isso.
5. `docs/ai/` segue como referência da Sprint 0; as decisões novas estão
   nos ADRs 050–055.

## 5. Próximo passo

`git push` (autorizado ao fim de tudo) → smoke live com provider real →
eventual ele LEVEL 2 com ADR.
