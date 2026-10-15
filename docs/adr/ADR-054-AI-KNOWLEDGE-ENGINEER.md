# ADR-054: Knowledge Engineer — rascunho de artigo com IA

- **Status**: aceito
- **Data**: 2026-10-14
- **Tasks**: Fase D do plano de execução da IA (`docs/36-PLANO-AI-EXECUCAO.md`);
  roadmap Fase 3 (blueprint `docs/34` §31 — Knowledge Engineer)

## Contexto

A geração determinística de rascunho (ADR-035 · `Generate*KnowledgeCommand`)
monta o artigo só dos registros existentes, sem prosa. A blueprint pede a
Fase 3: a IA redige rascunho a partir de **task/incident**, com o humano
no loop — o workflow de 8 estados do ADR-034 não pode mudar.

## Decisão

1. **Dois comandos novos** (`Operate`): `GenerateIncidentKnowledgeDraftCommand`
   (`POST /api/v1/incidents/{id}/generate-knowledge` — espelho da rota de
   tasks) e `GenerateTaskKnowledgeDraftCommand`
   (`POST /api/v1/tasks/{taskId}/generate-knowledge/ai` — irmã da rota
   determinística existente). Task mantém o gate documental (docs/32 §2):
   só `Resolved`/`Closed` gera.
2. **Capacidade `KnowledgeGeneration`** (ADR-037 §5 · docs/34 §31) no
   gateway; grounding pelo Context Engine (ADR-038) sobre título+descrição
   — mesma casca de evidências do Copilot/Investigation (§15).
3. **O rascunho nasce `Draft` por domínio** (`KnowledgeArticle.Create` —
   nunca enviamos status): publicar continua sendo transição humana
   `Draft → InReview → …` (ADR-034 §3 intacto). Sem categoria/responsável
   (null, igual à geração determinística) — humano classifica na revisão.
4. **Vínculo K4**: origem `Incident`/`Task` via `KnowledgeArticleOrigin`
   + versão v1.0 com motivo declarando autoria IA e revisão obrigatória —
   auditoria do que veio da IA (§55).
5. **Contrato JSON estrito**: o system prompt exige
   `{"title","content"}`; parse tolerante (primeiro `{` ao último `}`) com
   fallback para o título-fonte + texto bruto, sempre clampado nos limites
   do domínio (título ≤200, corpo ≤20.000) — resposta malformada nunca
   derruba a geração nem estoura validação.
6. **LEVEL 0** (ADR-040 §1): o draft DOCUMENTA — contexto, ocorrência e
   verificações somente leitura; nunca procedimento de escrita executado
   pela IA. Quem publica é humano.
7. **UI**: botão "Gerar artigo (IA)" na IncidentesPage (Operate), com
   aviso apontando a revisão na página Knowledge; a variante de task fica
   exposta na API (a tela de tasks hoje não tem geração — nem a
   determinística; não inventar UI nova aqui).

## Consequências

- Primeira escrita de domínio induzida por IA — mitigada por Draft +
  versão+origem rastreáveis e revisão humana obrigatória.
- Sem provider configurado → 503 amigável (GlobalExceptionHandler §44).
- Evolução natural: propor categoria/tags, enriquecer com evidências do
  incidente (já recuperadas) e geração em lote — com ADR próprio.
