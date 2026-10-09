# ADR-040 — Versionamento do artigo de Knowledge (snapshots v1.0 → v1.1 → v2.0)

- **Status**: aceito
- **Data**: 2026-10-08
- **Contexto**: F24-T003 (K3) — `docs/32-PLANO-MODULO-KNOWLEDGE.md` §8:
  "A Knowledge nunca deve perder versões anteriores" (v1.0 → v1.1 →
  v2.0), e **cada versão registra alteração, autor, data, motivo e
  aprovador**. Hoje o `PUT /knowledge/articles/{id}` sobrescreve
  `title/content/type/description/tags/responsible` sem guardar nada —
  o histórico só existe na auditoria genérica, não como versões
  consultáveis. O plano da Fase 24 manda "tabela nova de snapshots +
  endpoints de listagem/comparação + testes" (molde: histórico/versionamento
  do PDI — docs/30 §14, ADR-035).

---

## Problema

- `KnowledgeArticle` é **single-row**: editar perde o conteúdo anterior
  (viola o "nunca perder versões");
- não há onde registrar **motivo** da alteração nem **aprovador** da
  publicação (o §8 exige os 5 campos por versão);
- sem endpoints de listagem/comparação não há como auditar "o que mudou
  entre v1.0 e v1.1".

## Decisão

### 1. Tabela `knowledge_article_versions` — snapshot completo, append-only

Entidade `KnowledgeArticleVersion` (FK `article_id` **cascade** — apagar
artigo apaga as versões, como os vínculos/tags; PK composta
`(article_id, major, minor)`; nenhuma coluna nova em
`knowledge_articles`):

| Coluna | Conteúdo |
|---|---|
| `major`, `minor` | número da versão (exibição `"1.1"`) |
| `title`, `content`, `category_id`, `type`, `description`, `responsible_id`, `tags` (JSON) | **snapshot do conteúdo inteiro** (fonte do diff) |
| `author_id` | quem fez a alteração que gerou a versão (§8 "autor") |
| `reason` | motivo ≤500 (§8 "motivo") — obrigatório no `PUT` |
| `approved_by` | aprovador (§8) — preenchido na publicação |
| `status` | status do artigo no momento do snapshot |
| `create_datetime` | data (UTC, `AppTimeProvider` — §8 "data") |

"Alteração" do §8 = o snapshot novo em si (a comparação campo a campo é
o diff). Append-only: a API nunca altera nem apaga linha de versão.

### 2. Regras de numeração — 2 incrementos, tudo derivado

```text
criação ............... artigo corrente = v1.0 "virtual" (sem linha)
→ PUBLICADA (1ª vez) ... cria (1,0)      approved_by = quem publicou,
                                          reason = "Publicação inicial"
PUT em publicado-visível* ... (major, minor+1)  author = editor,
                                          reason = motivo do PUT
→ PUBLICADA (de novo) .. cria (major+1, 0)      approved_by = quem
                                          publicou  ← o v2.0 do §8
PUT em rascunho/em revisão/rejeitado .... sem snapshot (nada publicado
                                          a preservar; edições do
                                          rascunho entram na publicação)
```

- **publicado-visível\*** = `Published`, `Updated`, `Archived`,
  `Obsolete` (a regra de visibilidade do K2/ADR-039 §3);
- **artigo legado** (Published de antes do K3, zero linhas): a 1ª edição
  cria `(1,0)` com o motivo dela;
- derivado da tabela (`MAX(major/minor)` por artigo) — **sem coluna de
  versão no artigo** e portanto sem backfill; artigo nunca publicado
  lista **vazio** (a v1.0 só "existe" quando publicada);
- aprovar sem mudar conteúdo (`→ APROVADA → PUBLICADA` logo após) ainda
  cria o snapshot do major novo (a publicação é o evento mandatório).

### 3. `PUT` exige `reason` (motivo por versão)

`UpdateKnowledgeArticleCommand` ganha `Reason` (`VM [Required]` → 400
`O motivo da alteração é obrigatório.`; ≤500 no domínio → 400 pt-BR).
Precedente: PDI ADR-035 (motivo exigido quando campo muda). Aqui é
**sempre** exigido — o `PUT` inteiro é uma alteração. Sem UI de edição
hoje, o contrato novo só afeta API/testes/E2E.

### 4. Endpoints de listagem/detalhe/comparação (`RoleAccess.Read`)

| Rota | Devolve |
|---|---|
| `GET /knowledge/articles/{id}/versions` | lista — `version "1.1"`, campos do snapshot, `authorName`/`approvedByName` **em lote** (`ResolveUserNamesAsync`, sem N+1) |
| `GET /knowledge/articles/{id}/versions/{major}/{minor}` | snapshot completo |
| `GET /knowledge/articles/{id}/versions/compare?from=1.0&to=1.1` | `{field: {before, after}}` dos campos versionados + `reason`/`authorId`/`approvedById` da versão nova |

Artigo inexistente = 404 pt-BR (mesma regra das queries do módulo —
inclusive para leitor sem acesso ao estado atual); número de versão com
formato inválido (`"x.y"` não numérico) = **400**; versão pedida /
intervalo sem linha = **404**.

### 5. Escopo deliberado

- **sem UI** (não há nem tela de edição de artigo hoje — a task pede
  tabela + endpoints + testes; UI de versões vai para backlog);
- **sem diff em texto corrido** (campo a campo; `content` volta inteiro
  nos dois lados — render de diff visual é UI futura);
- sem retenção/purga de versões (append-only indefinido — nº de linhas é
  pequeno; purga seria política nova);
- `DELETE` do artigo continua em cascata (não existe "apagar versão");
- snapshots de **rascunho não versionado** são aceitos como perda
  deliberada (nunca foi público).

## Consequências

- `PUT` passa a ser breaking para clientes antigos (reason obrigatório) —
  E2E K1 e testes de Update são atualizados junto;
- o artigo pode ter N snapshots de 20k de `content` — payload da lista
  é o maior risco; se crescer, lista resumida (sem `content`) é o ajuste
  natural;
- o diff compara qualquer par (não só adjacentes), pois cada linha tem o
  conteúdo completo;
- regra de numeração acopla versão a **publicação**: quem quiser
  "v2.0" por outra razão (ex.: troca de tipo) terá de vir aqui;
- `approved_by` fica `null` nas linhas geradas por edição (só a
  publicação aprova) — coerente com o §8 (aprovador é da versão
  publicada).

## Validação (2026-10-08)

_Preenchida na execução da F24-T003._

## Referências

- `docs/32-PLANO-MODULO-KNOWLEDGE.md` §8 (versionamento) e §6
  (governança — K5/K3 vizinhos);
- molde: `docs/30-PLANO-MODULO-PDI.md` §14 + ADR-035 (motivo por
  alteração), `PdiHistoryEntry` (append-only, leitura pela API);
- ADR-039 (visão de estados usada em "publicado-visível");
- precedente de tabela do módulo: `knowledge_article_tags` (PK
  composta + cascade).
