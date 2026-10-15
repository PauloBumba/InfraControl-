# ADR-052: AI Copilot — chat grounded LEVEL 0

- **Status**: aceito
- **Data**: 2026-10-14
- **Tasks**: Fase B do plano de execução da IA (`docs/36-PLANO-AI-EXECUCAO.md`);
  autorização "nova" exigida por docs/35 §11 para a Fase 1 do roadmap
  (blueprint `docs/34` §23/§24/§51)

## Contexto

A Fase 0 entregou o gateway, providers, Context Engine (RAG híbrido),
MCP tools e segurança LEVEL 0, mas **nenhuma feature de usuário chama
a IA** — o pipeline está pronto e dormiente. A blueprint descreve o
Copilot (§23): chat que investiga com o Context Engine + tools e responde
grounded, citando evidências. O produto NÃO deve virar "Dashboard +
ChatGPT" (§68): o chat é ferramenta de investigação com evidência, não
bate-papo genérico.

## Decisão

1. **LEVEL 0 somente leitura** (ADR-040 §1): o system prompt obriga
   resposta grounded com citação de evidências (`[E1]`, `[E2]`…),
   proíbe invenção ("não encontrou contexto suficiente" é resposta
   válida) e proíbe recomendação de escrita — elevação exige ADR novo.
2. **Grounding pelo Context Engine existente** (ADR-038): a pergunta do
   usuário alimenta a busca híbrida (`IRetrievalService` →
   `IContextBuilder` → `EvidenceSet`, §19); sem evidências o prompt
   declara "sem contexto" — nunca invenção (§44).
3. **Capacidade `Copilot`** via `IAiGateway` (ADR-036): o chamador não
   escolhe modelo; roteamento/audit/fallback continuam no gateway
   (ADR-037 §15/§54).
4. **Autorização Read** (`IAuthorizedRequest.RequiredRoles =
   RoleAccess.Read` + `[Authorize]` sem policy no controller): "a IA
   enxerga exatamente o que o usuário enxerga" (ADR-040 §3/§41) —
   qualquer role autenticada conversa; o retrieval usa as mesmas
   visibilidades do backend.
5. **Contrato**: `POST /api/v1/ai/chat` recebe a conversa (últimas 20
   turns; a última é do usuário) e devolve resposta + evidências
   (fonte, citação, confiança) + provider/modelo/usedFallback para
   transparência (§15/§43).
6. **Estado degradado amigável** (§44/§57): `AiProviderNotConfigured`
   → HTTP 503 com mensagem clara; falha do provider → 502. Nunca 500
   genérico nem resposta inventada.
7. **Escopo v1**: sem streaming (evolução), sem persistência de
   histórico (o front mantém a conversa da sessão), tools do MCP fora
   do v1 do chat (entram na Fase 2 — Investigation).

## Consequências

- Primeira feature de usuário da IA; valida o pipeline Fase 0 de ponta
  a ponta quando houver runtime (Ollama profile `ai` ou provider
  externo do ADR-050).
- Sem provider configurado a tela mostra o estado "IA não configurada"
  — degradado explícito, nunca silencioso.
- Histórico por sessão no front; persistência de conversa = evolução.
- Streaming/SSE e tools no chat = evoluções com ADR próprio.
