# ADR-039 — Workflow dos status do artigo de Knowledge (8 estados)

- **Status**: aceito
- **Data**: 2026-10-08
- **Contexto**: F24-T002 (K2) — `docs/32-PLANO-MODULO-KNOWLEDGE.md` §7. O F10-T003 entregou o ciclo mínimo (`Draft → Published → Archived → Draft`, 3 estados); o §7 descreve o workflow editorial completo da base de conhecimento: `RASCUNHO → EM REVISÃO → APROVADA → PUBLICADA → EM REVISÃO → ATUALIZADA`, mais os estados residuais `Arquivada`, `Obsoleta` e `Rejeitada`. Aguarda desde 2026-09-26 ("workflow dos status = ADR antes de implementar").

---

## Problema

- o enum `KnowledgeArticleStatus` tem 3 membros (`Draft=0, Published=1, Archived=2`) — não há `EM REVISÃO`/`APROVADA`/`ATUALIZADA`/`REJEITADA`, ou seja, não existe revisão editorial nem o ciclo de atualização do §7;
- a regra de visibilidade esconde **apenas** `Draft` de quem não escreve — os estados intermediários novos (ainda não publicados) vazariam para leitores;
- a matriz de transição (3 setas) e as mensagens (409/400) estão espalhadas entre domínio, handler e testes; a 409 devolve os **nomes de membro em inglês** (`'Draft' → 'Archived'`), enquanto todo o resto da API é pt-BR;
- sem ADR, mexer no workflow seria alterar regra de domínio por conta própria (AGENTS: arquitetura exige ADR).

## Decisão

### 1. Enum de 8 estados, *append-only* (estabilidade do JSON numérico)

O MVC não tem `JsonStringEnumConverter` — o `status` viaja no JSON como **número** e o front usa constantes (`ARTICLE_STATUS_DRAFT = 0` etc.). Os 3 valores existentes ficam fixos; os 5 novos entram **depois**, preservando contrato e linhas no banco (storage é string com o nome do membro — nada muda em dados existentes):

| Valor | Membro (storage) | Rótulo pt-BR |
|---|---|---|
| 0 | `Draft` | Rascunho |
| 1 | `Published` | Publicada |
| 2 | `Archived` | Arquivada |
| 3 | `InReview` | Em revisão |
| 4 | `Approved` | Aprovada |
| 5 | `Updated` | Atualizada |
| 6 | `Obsolete` | Obsoleta |
| 7 | `Rejected` | Rejeitada |

Rótulos vivem no domínio (`KnowledgeArticleStatusExtensions.ToPtBrLabel`) e no catálogo do front (`knowledgeCatalogs.ts`).

### 2. Matriz de transição (estado único — `KnowledgeArticleStatusExtensions`)

O workflow é uma **máquina de estados sem histórico**: `EM REVISÃO` é um único estado, portanto tem as duas saídas que o §7 descreve em momentos diferentes (antes e depois da publicação) — aceito como simplificação documentada.

```text
RASCUNHO    → EM REVISÃO
EM REVISÃO  → APROVADA | ATUALIZADA | REJEITADA
APROVADA    → PUBLICADA
PUBLICADA   → EM REVISÃO | ARQUIVADA | OBSOLETA
ATUALIZADA  → EM REVISÃO | ARQUIVADA | OBSOLETA
ARQUIVADA   → RASCUNHO          (reabrir — paridade com o ciclo antigo)
OBSOLETA    → RASCUNHO          (reeditar)
REJEITADA   → RASCUNHO          (retrabalho → resubmeter)
```

- qualquer outra (inclusive **repetir o mesmo estado**) = `InvalidOperationException` → **409** `Transição inválida: 'X' → 'Y'.` com **rótulos pt-BR** (mudança de UX sobre o F10-T003 — testes/docs atualizados);
- não há retrocesso `EM REVISÃO → RASCUNHO` nem `APROVADA → EM REVISÃO` (revisão mata o artigo ou o aprova; reabrir é rejeitar/arquivar).

### 3. Visibilidade para leitores (estende o "só quem escreve vê rascunho" do F10-T003)

`KnowledgeArticleStatusExtensions.IsVisibleToReaders()`:

- **ocultos** (busca filtra; `GET /{id}` = 404 sem vazamento): `RASCUNHO`, `EM REVISÃO`, `APROVADA`, `REJEITADA` — estados não publicados ou reprovados;
- **visíveis**: `PUBLICADA`, `ATUALIZADA`, `ARQUIVADA`, `OBSOLETA` — histórico público da base.

A guarda continua sendo `Operate` (`RoleAccess.Operate` = Admin/Operator, `CanSeeDrafts` → `CanSeeUnpublished`): quem tem nível de escrita vê tudo. Implementação: `SearchAsync` filtra por `!HiddenFromReaders.Contains(status)` (EF → `NOT IN`); o `GET` checa na projeção já materializada.

### 4. Validação do status no comando (400)

`Enum.TryParse` aceita dígitos (`"99"` → valor fora do enum) — o handler passa a exigir `Enum.IsDefined`, cobrindo "status fora do enum = 400". Mensagem (8 opções, case-insensitive, camelCase nos tokens):

`Status inválido: 'promoted'. Use 'draft', 'inReview', 'approved', 'published', 'updated', 'archived', 'obsolete' ou 'rejected'.`

### 5. Sem migração e sem UI de troca

- **migração nenhuma**: coluna `status` já é string(32) com os valores existentes; os 5 novos só aparecem quando alguém transitar;
- **sem UI de troca de status** (não existe hoje e não é critério da task) — transição via `POST /knowledge/articles/{id}/status`; o front só ganha os 8 rótulos/cores nos chips (catálogo em `knowledgeCatalogs.ts`, herança do K1).

## Consequências

- artigos legados permanecem `Published`/`Archived`/`Draft` e nenhuma linha é reescrita; um `Draft` legado passa a ser oculto **também** para `APROVADA`… irrelevante para ele (ele já era oculto);
- `GET /{id}` de um artigo intermediário sem `Operate` responde 404 — leitores não descobrem o que está em revisão;
- rótulos 409 mudaram de inglês para pt-BR: consumidores que casassem a mensagem antiga (só testes/`docs/04`) precisam do texto novo;
- matriz cresce de 3 para 12 setas — testes de domínio cobrem todas as válidas por origem e as inválidas por amostragem;
- workflow com **aprovação em duas etapas** (`EM REVISÃO → APROVADA → PUBLICADA`) não tem `aprovador`/`versão` ainda — é o escopo do K3 (§Governança), que poderá exigir um ADR próprio.

## Validação (2026-10-08)

- `KnowledgeArticleTests` (bloco de transição reescrito: fluxo §7,
  residuais, branches, 5×409 pt-BR, Theory de visibilidade 8 casos) e
  `KnowledgeArticleStatusCommandTests` (reescrito: seed por matriz
  `PathTo`, ciclo via handler, 400 string/"99", busca/GET com Viewer);
  RED antes do GREEN (10 erros CS0117/CS1061 só em `tests/`);
- suítes: unit **1850/1850** · arch **119/119** · integração
  **193/193** (ciclo com revisão, 409 pt-BR e "Em revisão" oculto no
  Viewer via DI real);
- E2E `/tmp/opencode/k2_e2e.py` **31/31** (ciclo §7 com os números,
  residuais → Rascunho, case-insensitive, 409 com rótulos ×3, 400
  8-opções/`"99"`, convidado Viewer via convite: busca esconde não
  publicados, GET 404, troca de status 403) + regressões K1 **34/34**
  e PDI fase 7 **32/32**;
- front `tsc`/lint/build verdes · containers api/frontend reconstruídos
  healthy · **sem migração nova**.

## Referências

- `docs/32-PLANO-MODULO-KNOWLEDGE.md` §7 (workflow) e §6 (governança futura — K3);
- `docs/04-TASK-SLA-SLO-KB.md` §Publicação (ciclo anterior F10-T003);
- ADR-030 (`Operate`/`Administer`), ADR-031 (DDD/TDD vertical slice);
- precedente de enum pt-BR com transições: `PdiStatus` (ADR-029 §1–§2).
