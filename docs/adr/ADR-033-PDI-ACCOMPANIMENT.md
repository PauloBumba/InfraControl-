# ADR-033 — Acompanhamento Bilateral no PDI (Progresso, Check-ins, Impedimentos e Feedback)

- **Status**: aceito
- **Data**: 2026-10-06
- **Contexto**: Fase 3 — Acompanhamento (`docs/30-PLANO-MODULO-PDI.md` §9–§11, §13, §25). A Fase 1 (ADR-029) entregou o ciclo de vida do plano e a Fase 2 (ADR-032) a árvore objetivos → estratégias → fases → atividades; o plano de produto exige agora o acompanhamento bilateral dentro de cada fase — a característica principal do módulo (§9).

---

## Problema

O docs/30 descreve o acompanhamento como relação de duas mãos sob a **fase**:

1. **Colaborador relata** (§9): progresso, comentários, dificuldade, evidência;
2. **Gestor responde** (§10): feedback e próxima ação em check-ins periódicos;
3. **Impedimentos são entidade própria** (§11): tipo, descrição, impacto, data, responsável, ação tomada, status e "resolvido em" — "não apenas campo de texto" para gerar indicadores;
4. **Feedback é histórico** (§13): data, autor, destinatário, fase, tipo, conteúdo — "não sobrescrever".

A entidade `IndividualDevelopmentPlan` é uma linha por usuário e a árvore da Fase 2 guarda apenas metadados de planejamento — não existe onde morar o acompanhamento, nem regra de bilateralidade (quem fala o quê), nem máquina de status de impedimento, nem separação entre relato e resposta. A decisão arquitetural do usuário (docs/30 §25) mantém evidência/check-in/feedback/impedimento como **sub-agregados independentes** do plano.

## Decisão

### 1. Quatro entidades próprias sob a fase

| Entidade | Tabela | Pai | Papel |
|---|---|---|---|
| `PdiProgress` | `pdi_progresses` | `PdiPhase` | relato de progresso + comentário (§9) |
| `PdiCheckIn` | `pdi_check_ins` | `PdiPhase` | check-in bilateral (§10) |
| `PdiImpediment` | `pdi_impediments` | `PdiPhase` | dificuldade/impedimento com status (§11) |
| `PdiFeedback` | `pdi_feedbacks` | `PdiPhase` | histórico de feedback append-only (§13) |

Pai é a **fase** porque o diagrama da §9 ancora o acompanhamento no ramo FASE da árvore. FKs `DeleteBehavior.Restrict` (excluir fase com acompanhamento → 409, mesma regra da Fase 2). Referências a pessoas (`AuthorId`, `CreatedBy`, `RespondedById`, `RecipientId`, `ResponsibleId`) **não têm FK** — o critério de acesso é por `UserId`/`ManagerId` do plano (mesma escolha do ator no histórico, ADR-029). Enums viram `string(32)`, `DateOnly` vira `date`, datetimes ficam `timestamptz`.

**"Comentários" (§25) = `PdiProgress.Comment`** — não existe entidade `Comment` separada: o progresso é o veículo do comentário do colaborador (decisão registrada para não reabrir a questão na Fase 8).

### 2. Acompanhamento lido em um GET único do plano

`GET /pdi/{userId}/accompaniment` devolve as quatro listas do plano (`PdiAccompanimentDto`); o diálogo de front abre por fase e filtra por `phaseId`. Uma leitura resolve dono/gestor/Admin (`PdiAccess.EnsureCanView`) e evita N requests na árvore.

### 3. Permissões por ação em `PdiAccess` (§9 tem papéis diferentes por operação)

| Ação | Regra | Backend |
|---|---|---|
| Ler acompanhamento | Admin · dono · gestor | `EnsureCanView` |
| Criar/excluir progresso, check-in, impedimento | Admin · dono | `EnsureCanReport` |
| Responder check-in (lado gestor) | Admin · gestor | `EnsureCanRespond` |
| Editar impedimento | Admin · dono · gestor | `EnsureCanAssist` |
| Criar feedback | quem vê o plano (Admin · dono · gestor) | `EnsureCanView` |
| Excluir feedback | **só Admin** | `EnsureIsAdmin` + policy `Administer` no endpoint |

Operador estrangeiro → 403 pt-BR em tudo; a UI apenas **esconde** os botões que o papel costuma não usar — quem autoriza é o backend.

### 4. Check-in bilateral com PUT por lado

`UpdatePdiCheckIn` distingue os lados por **presença do campo**:

- campo enviado (`""` incluso) → grava (`""` normaliza a `null` = limpar);
- campo não enviado (`null`) → preserva o valor atual;
- lado relato (`whatDone/whatLearned/difficulty/needSupport`) só por dono/Admin; lado resposta (`managerFeedback/nextAction`) só por gestor/Admin; nenhum dos dois → 403;
- `RespondedAt`/`RespondedById` só mudam quando o **lado resposta** é enviado — um PUT de relato nunca re-carimba a resposta (regressão coberta no teste bilateral);
- limpar os dois campos de resposta zera `RespondedAt` (desfazer).

### 5. Impedimento: máquina de status com `ResolvedAt` derivado

`PdiImpedimentStatus` = `Open | Resolved` (→ string). `Update(..., Resolved)` preenche `ResolvedAt` (hoje, UTC) preservando data já gravada; voltar para `Open` limpa; status fora do par → 400 `Status do impedimento inválido (use Open ou Resolved)`. `PDIImpedimentType` como **lookup de tipos** fica para a Fase 8 (analytics) — hoje `Type` é texto livre ≤100 com o exemplo da §11 ("recurso indisponível").

### 6. Fase 3 não escreve histórico nem eventos

Assim como a Fase 2 (ADR-032 §7): mutação silenciosa; a timeline/auditoria é da Fase 5. O E2E da fase trava que a contagem do histórico não muda após o CRUD completo do acompanhamento.

### 7. API e front

Superfície (`PdiAccompanimentController`, rota `api/v1/pdi`, policy `Operate`; DELETE de feedback é `Administer`):

- `GET  /pdi/{userId}/accompaniment` — quatro listas do plano;
- `POST/DELETE /pdi/progresses[/{id}]`;
- `POST/PUT/DELETE /pdi/checkins[/{id}]`;
- `POST/PUT/DELETE /pdi/impediments[/{id}]`;
- `POST/DELETE /pdi/feedbacks[/{id}]`.

Creates respondem 200 com o DTO; updates/deletes → 204; validação de domínio → 400 pt-BR; fase inexistente → 404; escopo alheio → 403.

O front ganha `PdiAccompanimentDialog` (abas Check-ins · Progresso · Impedimentos · Feedback com contadores, forms por tipo, `ConfirmDialog` nos deletes, chips de status) acionado pelo botão **"Acompanhar"** em cada fase do `PdiStructureDialog`; `pdi.ts` espelha DTOs/rotas e os rótulos `Aberto/Resolvido`. Datas `yyyy-MM-dd` formatadas à mão (anti-fuso).

## Consequências

- **Positivas**: acompanhamento cresce por entidade sem tocar no plano nem na árvore (Fase 4 evidências entra no mesmo padrão sob a fase); bilateralidade explícita no contrato (PUT por lado) em vez de campo compartilhado; impedimento já nasce estruturado para os indicadores da Fase 8; permissão por ação evita o "dono não pode nada / gestor pode tudo" raso.
- **Negativas**: referências de pessoa sem FK deixam linhas órfãs possíveis se um usuário for excluído (mesma acepção do histórico — aceito); `GET` carrega as quatro listas do plano inteiro e o front filtra por fase (ok na escala de um plano por usuário; se crescer, paginar por fase); "comentários" dentro de `PdiProgress` acopla comentário ao relato (se a Fase 8 exigir comentário solto, é nova entidade + ADR).

## Referências

- `docs/30-PLANO-MODULO-PDI.md` §9–§11, §13, §25 (Fase 3)
- ADR-029 — ciclo de vida do plano (histórico, atores sem FK)
- ADR-030 — matriz de CRUD/autorização (`Operate`/`Administer`)
- ADR-032 — estrutura do desenvolvimento (árvore, `PdiAccess`, FKs `Restrict`)
