# ADR-032 — Estrutura do Desenvolvimento no PDI (Competências, Objetivos, Estratégias, Fases e Atividades)

- **Status**: aceito
- **Data**: 2026-10-05
- **Contexto**: Fase 2 — Estrutura do desenvolvimento (`docs/30-PLANO-MODULO-PDI.md` §5–§8, §25). A Fase 1 (ADR-029) entregou o ciclo de vida do plano como entidade única (`IndividualDevelopmentPlan`); o plano de produto exige agora a árvore de desenvolvimento que o operador preenche dentro do plano.

---

## Problema

O docs/30 descreve, entre a concepção do plano e o acompanhamento:

1. **Diagnóstico/competências** (§4–§5): o plano parte de competências (gaps) e de uma situação inicial/desejada.
2. **Objetivos com estratégia** (§5–§6): cada objetivo carrega situação atual/desejada, resultado esperado, indicador, prazo e é perseguido por uma ou mais estratégias.
3. **Fases e atividades** (§7–§8): cada estratégia se desdobra em fases (com responsável, período, critério de conclusão, status e progresso) e cada fase em atividades com prazo e conclusão.

A entidade atual é uma linha por usuário — não existe onde morar a árvore, nem regras de ordenação, ownership por plano, nem integridade referencial entre os níveis. O histórico do plano (Fase 5) e a governança de fases (aprovação/aceite) também não existem ainda.

## Decisão

### 1. Cinco entidades próprias sob o plano (sub-agregados)

| Entidade | Tabela | Pai | Papel |
|---|---|---|---|
| `PdiCompetency` | `pdi_competencies` | — | catálogo de competências (**global**, sem dono) |
| `PdiObjective` | `pdi_objectives` | `IndividualDevelopmentPlan` (via `UserId`) | objetivo com diagnóstico e prazo |
| `PdiStrategy` | `pdi_strategies` | `PdiObjective` | estratégia que persegue o objetivo |
| `PdiPhase` | `pdi_phases` | `PdiStrategy` | fase com responsável, período, status e progresso |
| `PdiActivity` | `pdi_activities` | `PdiPhase` | atividade com prazo e conclusão |

Nada disso entra na entidade `IndividualDevelopmentPlan` — o PDI permanece o agregado raiz, mas cada nível tem responsabilidade própria (decisão arquitetural do usuário, docs/30 §25). Campos de negócio (títulos ≤ limites, período `EndDate ≥ StartDate`, `Percent` 0–100, status válido) são validados nas entidades, com mensagens pt-BR traduzidas a 400 pelo `GlobalExceptionHandler`.

### 2. Competências: catálogo global, escrita de Admin

`PdiCompetency` tem só `Name`/`Description` (sem `UserId`) — o catálogo é compartilhado por todos os planos, e a duplicata é rejeitada por **lowercase** (`LOWER(name)` na consulta; funciona em Postgres e SQLite). CRUD exige policy `Administer`; leitura exige `Operate`. `PdiObjective.CompetencyId` é opcional e resolve para o nome do catálogo na leitura.

### 3. Ordenação explícita por nível

Cada nível (`objective/strategy/phase/activity`) tem `Order` persistido como coluna **`sort_order`** (a tabela é `order`… não pode ser nome de coluna em SQL) com atribuição automática (`max+1` na criação, reordenação estável no update). A leitura ordena por `sort_order`.

### 4. Ownership único em uma porta: `PdiAccess`

Toda escrita da estrutura passa por `PdiAccess.EnsureCanManage(planOwnerId)`:

- **Admin** gerencia a estrutura de qualquer plano;
- **Operador** só do próprio plano (plano alheio → 403 `UnauthorizedAccessException`, pt-BR).

Como cada nó guarda só o id do pai, `PdiStructureLookup` sobe a cadeia `activity → phase → strategy → objective → plan` (`IPdiStructureRepository.ResolveStructureOwner`) antes de checar o acesso. Leitura `GET /pdi/{userId}/structure` usa a mesma regra.

### 5. Integridade: `Restrict` → 409

Todas as FKs são `DeleteBehavior.Restrict`: excluir um nó com filhos devolve **409** (`InvalidOperationException` traduzido pelo handler) com mensagem pt-BR — nada de cascade apagando meio plano sem o usuário perceber. A limpeza é sempre folha→raiz.

### 6. Status de fase provisório

`PdiPhaseStatus` = `NotStarted | InProgress | Completed` (enum → string), valores mínimos para a Fase 2 — a semântica de governança (aprovação, aceite do colaborador) vem na **Fase 5**; a Fase 2 apenas normaliza `Status` e `Percent` (`Completed ⇒ 100`) na criação e valida o intervalo. Nomenclatura e regras finais ficam com a Fase 5.

### 7. Fase 2 não escreve histórico nem eventos

A timeline do plano (`PdiHistoryEntry`) e os eventos de domínio da estrutura ficam para a **Fase 5 (auditoria)**; a Fase 2 é mutação silenciosa. O E2E da fase trava que a contagem do histórico não muda após CRUD completo da estrutura.

### 8. API e front

Superfície (`PdiStructureController`, rota `api/v1/pdi`):

- `GET  /pdi/{userId}/structure` — árvore completa (objetivos → estratégias → fases → atividades), `Operate`;
- `GET  /pdi/competencies` — catálogo, `Operate`;
- `POST/PUT/DELETE /pdi/competencies` — `Administer`;
- `POST /pdi/{objectives|strategies|phases|activities}` → 200 com o DTO criado; `PUT` → 204; `DELETE` → 204 — todos `Operate` + `PdiAccess`.

O front ganha `PdiStructureDialog` (árvore com formas de cada nível, forms por nível, catálogo de competências com criação Admin, `ConfirmDialog` nos deletes) acionado pelo botão "Estrutura" da `PdiPage`; responsável da fase resolve Admin → usuários, Operador → próprio plano + gestor. Datas em `DateOnly` serializadas como `yyyy-MM-dd` (anti-fuso).

## Consequências

- **Positivas**: cada nível evolui independente (Fase 3 check-ins, Fase 4 evidências, Fase 5 governança crescem sem tocar a entidade do plano); ownership centralizado em um único ponto testável; deletes seguros (409) e ordenação determinística.
- **Negativas**: escritas em profundidade pagam a resolução da cadeia pai (uma query por nó) — aceitável para a escala atual; catálogo de competências global exige semântica de "remover sem filhos" para não afetar planos alheios (FK `Restrict` resolve); `PdiPhaseStatus` provisório pode mudar de nome na Fase 5 (migração de dados pequena).

## Referências

- `docs/30-PLANO-MODULO-PDI.md` §4–§8, §25 (Fase 2)
- ADR-029 — ciclo de vida do plano (status, gestor, período, histórico)
- ADR-030 — matriz de CRUD/autorização (`Operate`/`Administer`)
