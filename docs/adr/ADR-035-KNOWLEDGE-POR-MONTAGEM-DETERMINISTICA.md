# ADR-035: Knowledge gerada por montagem determinística (sem LLM)

- **Status**: aceito
- **Data**: 2026-10-07
- **Tasks**: F24-T006/K6 (spec docs/32 §2–§4)

## Contexto

A spec §2–§3 desenha "Ao concluir uma Task … > Gerar Knowledge", em que
"a IA poderá analisar" objetivo/timeline/decisões/evidências/etc. "e
gerar um primeiro rascunho", com duas regras duras:

- "A IA não deve inventar informações."
- "O conteúdo deve estar baseado nos registros existentes."

O repositório **não tem nenhuma infraestrutura de IA** (docs/31 §12): sem
provedor, sem chave, sem orçamento — e o AGENTS.md proíbe inventar
requisitos ou usar credenciais reais. A task determina, então, definir a
arquitetura antes de implementar.

## Decisão

Implementar a geração como **montagem determinística dos registros
existentes**, sem LLM:

- **Endpoints explícitos** (nada roda sozinho na conclusão, a §2 diz
  "o sistema poderá disponibilizar"):
  - `POST api/v1/tasks/{taskId}/generate-knowledge` (Operate);
  - `POST api/v1/pdi/{userId}/generate-knowledge` (Operate + dono/Admin).
- **Gate de conclusão** (§2/§4 "ao concluir"): Task em `Resolved` ou
  `Closed` e PDI em `Completed` — fora disso = 409 pt-BR.
- **Composer puro** (`KnowledgeDraftComposer`): seção só entra no
  rascunho se existir registro correspondente (objetivo, resultado,
  critérios, fases, subtask-stats, impedimentos, evidências, comentários,
  linha do tempo da Task; plano, objetivos e timeline do PDI). Texto
  ausente nunca é criado — a seção vazia é simplesmente omitida.
- **Nasce Draft** (§2 "Knowledge sugerida → Revisão"): o rascunho segue o
  workflow normal do ADR-034 — não existe caminho que pule revisão.
- **Proveniência visível**: banner no topo do rascunho indica que veio de
  registros da Task/PDI e pede revisão; versão v1.0 (K3) registra o motivo
  "Geração automática a partir de …"; origem Task/Pdi (K4) deixa o vínculo
  "criada a partir de" navegável na busca/detalhe.

## Consequências

- Atende literalmente "não inventar" (§3) com zero credenciais, zero
  serviço externo e zero migração nova (usa tabelas/vínculos K3/K4).
- O rascunho é mais "cru" que um LLM (seções estruturais com os campos
  registrados, não texto corrido otimizado) — adequado ao fluxo
  sugerida → revisão → aprovação.
- Evoluir para LLM real exige **ADR próprio** (provedor, orçamento, chave
  fora do repositório, avaliação de alucinação); o composer continuaria
  como fallback/validador de que nada fora dos registros entrou no texto.
- "sistema/setor" (§13) e seções sem registro correspondente ("pesquisas",
  "decisões" literais) ficam de fora até o modelo/fonte existir — não
  inventar campo.
