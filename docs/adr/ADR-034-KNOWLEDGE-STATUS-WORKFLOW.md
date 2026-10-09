# ADR-034 — Workflow de status do artigo Knowledge (revisão, publicação e estados especiais)

- **Status**: aceito
- **Data**: 2026-10-07
- **Contexto**: Fase 24-T002 / K2 (`docs/32-PLANO-MODULO-KNOWLEDGE.md` §7). O F10-T003 entregou um ciclo próprio de 3 estados (`Draft → Published → Archived → Draft`); a spec descreve agora um fluxo de revisão com aprovação antes de publicar. Transição de status é regra de domínio → ADR antes de implementar (`AGENTS.md`).

---

## Problema

A spec §7 desenha:

```text
RASCUNHO → EM REVISÃO → APROVADA → PUBLICADA → EM REVISÃO → ATUALIZADA
```

mais os estados "Arquivada", "Obsoleta" e "Rejeitada". O código só tem `Draft/Published/Archived`, sem aprovação nem revisão, e a regra de visibilidade (F10-T003) só esconde `Draft`. Três ambiguidades precisam de decisão antes de código:

1. "EM REVISÃO" aparece **duas vezes** na cadeia (antes de APROVADA e depois de PUBLICADA) — é um estado ou dois?
2. De onde saem os três estados extras (Arquivada/Obsoleta/Rejeitada)?
3. Quais estados um leitor (sem nível de escrita) pode ver, agora que existem estados pré-publicação?

## Decisão

### 1. Oito estados (um `InReview` só)

A cadeia repetida é um único estado — o artigo não guarda a origem da revisão. `KnowledgeArticleStatus`:

| Valor | Nome | Rótulo pt-BR (spec §7) |
|---|---|---|
| 0 | `Draft` | Rascunho |
| 3 | `InReview` | Em revisão |
| 4 | `Approved` | Aprovada |
| 1 | `Published` | Publicada |
| 5 | `Updated` | Atualizada |
| 2 | `Archived` | Arquivada |
| 6 | `Obsolete` | Obsoleta |
| 7 | `Rejected` | Rejeitada |

### 2. Números legados estáveis, armazenamento segue string

`Draft=0`, `Published=1`, `Archived=2` **preservados** (contrato numérico do F10-T002/T003 — o MVC não registra `JsonStringEnumConverter` e o front já trata esses números); os novos entram 3–7. No banco continua string (`HasConversion<string>`): linhas antigas (`Draft`/`Published`/`Archived`) valem sem migração de dados — só a coluna nova do K1 exige migração.

### 3. Matriz de transições

| De | Para |
|---|---|
| `Draft` | `InReview` |
| `InReview` | `Approved`, `Updated`, `Rejected`, `Draft` |
| `Approved` | `Published`, `Draft` |
| `Published` | `InReview`, `Archived`, `Obsolete` |
| `Updated` | `InReview`, `Archived`, `Obsolete` |
| `Archived` | `Draft` |
| `Obsolete` | `Draft` |
| `Rejected` | `Draft` |

- **Cadeia principal** = a §7: rascunho vai para revisão; revisão de rascunho fecha em `Approved → Published`; revisão de publicado fecha em `Updated` (sem segundo ciclo de aprovação — é o que a cadeia desenha).
- `InReview` aceita **os dois** fechamentos (`Approved` e `Updated`) porque o estado não guarda origem: guardá-la exigiria histórico extra; a ADR escolhe a solução simples e registra a consequência.
- Estados de saída (`Archived`/`Obsolete`/`Rejected`) **voltam para `Draft`** (restauração/retrabalho — mesma restauração `Archived → Draft` do F10-T003).
- Fora da matriz (inclusive repetir o mesmo estado) = **409** `Transição inválida: 'X' → 'Y'.` (mecanismo inalterado).
- Nenhuma transição "de volta" de `Published` para estados pré-publicação: para revisar um publicado sai `Published → InReview`.

### 4. Visibilidade: generalização da regra F10-T003

`includeDrafts` vira **`includeUnpublished`**: escondidos de quem não tem nível de escrita = `Draft`, `InReview`, `Approved`, `Rejected` (nunca publicados); visíveis = `Published`, `Updated`, `Archived`, `Obsolete`. `GET/{id}` de estado não-publicado sem escrita = **404** como se não existisse (não vaza existência — mesma regra do F10-T003).

Consequência assumida: **rejeitar a revisão de um artigo já publicado o tira da vista dos leitores** até republicar (`Rejected → Draft → InReview → …`). Modelo de estado único não guarda "publicado com revisão rejeitada" — se a spec exigir isso, é histórico de revisões + ADR novo.

### 5. Mensagens e superfície

- 400 `Status inválido: 'x'. Use 'draft', 'inreview', 'approved', 'published', 'updated', 'archived', 'obsolete' ou 'rejected'.`
- Rota/contrato **inalterado**: `POST /api/v1/knowledge/articles/{id}/status` (`RoleAccess.Operate`), 200 devolve o DTO.
- A spec não define papéis por transição → **não inventar**: todas as transições seguem `Operate`.

### 6. Rótulos da UI

O front usa os rótulos **verbatim da §7** (feminino: "Publicada", "Arquivada" — antes eram "Publicado"/"Arquivado"). Novos chips: Em revisão (âmbar), Aprovada (verde-água), Atualizada (azul-céu), Obsoleta (cinza), Rejeitada (vermelho).

## Consequências

- **Positivas**: publicação passa por aprovação explícita (governança do conhecimento), ciclo de atualização de artigo publicado modelado sem inventar campos; enum sem quebra de contrato numérico nem migração de dados; regra de visibilidade única e testada.
- **Negativas**: matriz de transições maior (16 pares válidos × testes); `InReview` ambíguo (dois fechamentos) — aceito; `Published` e `Updated` são dois estados "vivos" que os indicadores (K5/F25) vão precisar somar; rejeição esconde artigo publicado (decisão 4); rótulos mudam de gênero na UI.

## Referências

- `docs/32-PLANO-MODULO-KNOWLEDGE.md` §7 (Status), §12 (Revisão)
- `docs/04-TASK-SLA-SLO-KB.md` — §Publicação (ciclo anterior F10-T003)
- F10-T003 — ciclo de 3 estados e regra de visibilidade original
- ADR-016 — níveis de acesso (escrever/ler)
- `AGENTS.md` — "Não alterar arquitetura sem ADR"
