# ADR-067: Checklist genérico — entidade polimórfica espelhando TaskCriterion

- **Status**: aceito (2026-11-10 — direção do usuário na Fase 50:
  "checklist = entidade genérica espelhando TaskCriterion". Reversível
  por nova ADR.)
- **Data**: 2026-11-10
- **Tasks**: `F50-T007` (TASKS.md — Fase 50)
- **Relacionado**: F23-T003 (`TaskCriterion`, docs/04 §Critérios de
  aceite), F24-T004 (`KnowledgeArticleOrigin` — precedente polimórfico),
  ADR-030 §5 (eventos CRUD), ADR-033 (refs sem FK — órfãos aceitos),
  ADR-066 (módulos de acesso), ADR-016 (defaults de papel)

## Contexto

O pedido da Fase 50 é um **checklist genérico**: itens verificáveis
(descrição + marcação com carimbo) que sirvam a qualquer entidade do
produto, não só a tasks. O molde é o `TaskCriterion` (F23-T003,
docs/04 §Critérios de aceite): `description` ≤300 (trim, obrigatório),
`is_done`, `done_at?` (carimbo em `SetDone`), CRUD com 404/400 pt-BR,
no-op sem efeito colateral, eventos de domínio (ADR-030).

Hoje esse checklist está preso ao agregado Task (`task_criteria`, FK
Cascade). Reutilizar `task_criteria` reinterpretando `task_id` como id
genérico quebraria a FK e a semântica da coluna; duplicar a tabela por
entidade (`device_checklists`, `location_checklists`, ...) multiplicaria
migrações, comandos e UI sem ganho algum — e não seria "genérico".

O codebase já tem um precedente de referência polimórfica aceito:
`KnowledgeArticleOrigin` (F24-T004) — `origin_type` (enum persistido
como string(16)) + `origin_id` **sem FK**, existência validada no
handler por tipo (padrão derivado das refs sem FK do ADR-033).

## Decisão (proposta)

1. **Entidade `ChecklistItem`** (`Domain/Entities`) espelhando
   `TaskCriterion`: `Description` ≤300 (trim, obrigatório), `IsDone`,
   `DoneAt?` (carimba/limpa em `SetDone`), eventos
   `ChecklistItemCreated/Updated/DeletedDomainEvent` (ADR-030 §5).
2. **Dono polimórfico**: `OwnerType` (`ChecklistOwnerType`, enum
   persistido como `string(16)`) + `OwnerId` (Guid, **sem FK** —
   precedente `KnowledgeArticleOrigin` / ADR-033).
3. **Conjunto inicial do enum**: `Device`, `Location`, `Incident`,
   `Project` — telas de detalhe existentes onde um checklist
   operacional é natural. `Task` fica de fora: o `TaskCriterion`
   continua sendo o checklist de task (sem migração de dados, sem dito
   sistema duplicado). Extensão futura = valor novo no enum + casa no
   switch de validação (o próprio enum já documenta essa regra).
4. **Tabela `checklist_items`**: `id`, `owner_type`, `owner_id`,
   `Description`, `IsDone`, `done_at`, `CreateDateTime`,
   `UpdateDateTime` — mesmo mapeamento misto de `task_criteria`
   (snake_case só nos nomes próprios; `Description`/`IsDone` seguem
   PascalCase como lá); índice `(owner_type, owner_id)`; **sem FK**
   (ver Consequências).
5. **Validação de existência do dono no handler por tipo**: switch sobre
   `IDeviceRepository` / `ILocationRepository` / `IIncidentRepository` /
   `IProjectRepository`, 404 pt-BR no formato do precedente
   (`Device '{id}' não encontrado.`, `Local '{id}' não encontrado.`,
   `Incidente '{id}' não encontrado.`, `Projeto '{id}' não encontrado.`).
6. **Application `InfraControl.Application.Checklists`** com
   `AddChecklistItemCommand` / `UpdateChecklistItemCommand` /
   `DeleteChecklistItemCommand` (`BaseCommand` + `IAuthorizedRequest`
   → `RoleAccess.Operate`) e `GetChecklistItemsQuery` (`IQuery` +
   `RoleAccess.Read`) — semântica idêntica ao `TaskCriterion`: `null`
   = mantém, no-op não persiste, 404/400 pt-BR, ordem de criação na
   listagem. **Sem histórico de timeline**: não existe timeline
   genérica — o histórico continua sendo responsabilidade do agregado
   dono (o `TaskCriterion` segue gravando em `TaskHistory`).
7. **API** `api/v1/checklists`: `GET/POST {ownerType}/{ownerId}` e
   `PUT/DELETE items/{itemId}` — leitura `[Authorize]`, escrita
   `Operate` (mesmos verbos/rotas dos critérios de task); parse do
   `ownerType` ignoreCase com 400 pt-BR para valor desconhecido.
8. **Módulo `checklists` no `AccessModuleCatalog`** (ADR-066): rota
   `/api/v1/checklists` e namespace `InfraControl.Application.Checklists`
   resolvem o módulo; sem regras gravadas valem os defaults ADR-016
   (Viewer→Read, Operator→Operate, Admin→Administer) — mesmo
   comportamento do fallback legado de hoje; admin pode restringir na
   matriz sem deploy.
9. **Sem UI nesta entrega**: o pedido definiu a *entidade* genérica; a
   tela (componente reutilizável + host) entra em task própria quando
   houver pantalla escolhida — não inventar consumidor.

## Alternativas

| # | Alternativa | Por que não |
| --- | --- | --- |
| 1 | Tabela por dono (`device_checklists`, `location_checklists`, ...) | Não é genérico: N migrações, N comandos, UI duplicada — contradiz o pedido. |
| 2 | Colunas FK espalhadas (`device_id`, `location_id`, ... nullable) | Vira 4+ colunas mutuamente exclusivas, índice parcial e validação mista; o codebase já aceitou o padrão sem FK (ADR-033, `KnowledgeArticleOrigin`). |
| 3 | Reaproveitar `task_criteria` reinterpretando `task_id` | Quebra a FK Cascade e a semântica da coluna (`task_id` deixa de ser task) — risco silencioso de dado errado. |
| 4 | Espelhar também o `TaskHistory` do critério | Não existe timeline genérica nem dono de histórico; inventar `checklist_history` não foi pedido. |
| 5 | `owner_type` livre (string sem enum) | A casa usa enum persistido como string (`KnowledgeOriginType` etc.); enum dá parse 400 pt-BR e catálogo de valores. |
| 6 | Validar existência do dono só na leitura | POST criaria itens órfãos por typo; o precedente valida no handler. |

## Decisões fechadas (2026-11-10)

- Conjunto inicial do enum: **Device, Location, Incident, Project**
  (Task fora — `TaskCriterion` permanece; sem migração de dados).
- **Sem UI nesta entrega** (consumidor indefinido no pedido — não
  inventar tela).
- **Sem limpeza automática** na exclusão do dono nesta entrega: não há
  itens reais até a UI existir; quando um consumidor passar a criar
  itens, o comando de exclusão do agregado dono deve removê-los (ver
  Consequências).

## Consequências

- **Sem FK ⇒ a exclusão do dono não apaga os itens** (mesma limitação
  aceita em `KnowledgeArticleOrigin` e nas refs do ADR-033). Mitigações:
  validação de existência na criação/leitura impede typo; cleanup
  explícito fica a cargo do comando de exclusão do consumidor quando a
  UI existir — até lá nenhum item nasce.
- Item órfão também surge se o dono for apagado por SQL direto — fora do
  caminho do produto.
- O módulo `checklists` aparece na matriz ADR-066 com defaults; perfis
  customizados ficam em None até o admin conceder (opt-in do próprio
  ADR-066).
- `TaskCriterion` não é afetado: FK, `TaskHistory` e card do
  TaskDetailPage seguem como estão.
- Frontend: nenhuma mudança nesta task.
