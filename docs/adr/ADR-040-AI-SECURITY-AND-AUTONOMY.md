# ADR-040: AI Security & Autonomy — partida no LEVEL 0 (Read Only)

- **Status**: aceito
- **Data**: 2026-10-08
- **Tasks**: FAI-T001 (Fase AI — sprint S1 de `docs/35`; blueprint `docs/34` §39–§42, §65)

## Contexto

A blueprint cria níveis de autonomia (§39) e manda **"Começar
obrigatoriamente no LEVEL 0"**, com guardrails que proíbem LLM → shell
arbitrário, LLM → banco direto e qualquer ação sem
`Policy → Permission → Approval → Audit` (§40/§41/§65). O repositório
já tem autorização por roles (ADR-016 `RoleAccess` + policies
`Read`/`Operate`/`Administer`) e auditoria persistida (F0-T014), além
de regras de visibilidade testadas (ex.: nunca-publicados escondidos de
quem não escreve — K2/ADR-034 §4).

## Decisão

1. **Nível inicial: LEVEL 0 — Read Only** (§39): a IA consulta,
   analisa e explica; **nenhuma tool de escrita** nesta fase. Qualquer
   elevação (LEVEL 1 recomenda, LEVEL 2 aprova e executa) exige ADR
   novo com o workflow de aprovação.
2. **Pipeline obrigatório de ação**: `LLM → Tool → Policy →
   Permission → Data` (§40). Ferramenta sempre explícita no registry
   (ADR-039) — nunca ferramenta implícita derivada de texto.
3. **Permissões = as existentes** (§41): `Read` para interagir;
   `RequiredPermission` por tool reusa `RoleAccess`. **Nenhum role novo**
   até haver necessidade real. A IA enxerga exatamente o que o usuário
   enxerga: os filtros de visibilidade do backend (ex.:
   `CanSeeUnpublished`) valem para todo contexto entregue à IA.
4. **Prompt injection** (§42): todo conteúdo recuperado (Knowledge,
   logs, tarefas, documentos, APIs externas) é tratado como **dado**,
   delimitado e citado — nunca como instrução; system prompt,
   permissions e tool policy **não** vêm de conteúdo recuperado.
5. **Auditoria por request de IA** (§41/§43): ator, ferramentas,
   dados consultados, provider, modelo, latência, tokens e traceId —
   pelo mecanismo de auditoria existente com ação `AI.*`; os campos
   extras (tokens/custo) ficam em metadata ou tabela própria,
   **schema decidido no S6** (migração pendente de ambiente).
6. **Limites no gateway** (§55): max tokens, max tool calls, timeout
   de investigação e max contexto configurados com default
   conservador desde o primeiro request.

## Consequências

- Toda entrega de IA das Fases 1–2 nasce compatível com as policies
  atuais — sem refatoração de autorização.
- Automação (Fase 5/§38–§40) fica bloqueada por decisão explícita:
  ADR próprio + aprovação humana + tool dedicada.
- Auditoria `AI.*` entra no mesmo relatório de auditoria já existente
  (AuditPage), sem console novo.
