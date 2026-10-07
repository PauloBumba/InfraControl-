# ADR-034 — Evidências do PDI (Upload, Revisão do Gestor e Storage)

- **Status**: aceito
- **Data**: 2026-10-07
- **Contexto**: Fase 4 — Evidências (`docs/30-PLANO-MODULO-PDI.md` §12, §17, §25). As Fases 1–3 (ADR-029, ADR-032, ADR-033) entregaram o ciclo de vida do plano, a árvore objetivos → estratégias → fases → atividades e o acompanhamento bilateral; o plano de produto exige agora o mecanismo genérico de evidência por fase — o elo entre execução e validação do fluxo oficial da §24 (EXECUÇÃO → **EVIDÊNCIAS** → VALIDAÇÃO).

---

## Problema

O §12 define um mecanismo genérico de evidência:

1. **9 tipos** fixos: certificado · documento · imagem · arquivo · atividade realizada · avaliação · feedback · resultado · outro;
2. **Fluxo**: colaborador envia → evidência **pendente** → gestor analisa → **validada / rejeitada (com motivo da rejeição)**;
3. O §7 lista "evidências" como atributo da fase e o §17 pede "certificado anexado" na timeline.

Não existe entidade onde a evidência mora, onde o **arquivo** é gravado, quem pode **revisar** (e quantas vezes), nem como isso entra no `GET /pdi/{userId}/accompaniment` e no histórico append-only. A decisão arquitetural do usuário (docs/30 §25) mantém evidência como **sub-agregado independente** do plano.

## Decisão

### 1. Entidade `PdiEvidence` sob a fase

| | |
|---|---|
| Entidade / tabela | `PdiEvidence` / `pdi_evidences` (FK `Restrict` → `pdi_phases`, mesma regra das Fases 2–3) |
| Campos | `PhaseId`, `Type`, `Status`, `Description?` (≤500), `FileName`, `ContentType`, `FileSize`, `CreatedBy/CreatedAt`, `ReviewedBy?/ReviewedAt?`, `RejectionReason?` (≤500) |
| Enums (→ `string(32)`) | `PdiEvidenceType` (9 valores do §12 + rótulos pt-BR via `ToDisplayLabel`), `PdiEvidenceStatus` (`Pending`/`Validated`/`Rejected`), `PdiEvidenceReviewDecision` (`Validated`/`Rejected`) |

Regras de domínio (erros em pt-BR → 400):

- arquivo **obrigatório, não vazio e ≤ 10 MB** (`PdiEvidence.MaxFileSize`);
- **nome saneado**: só o basename sobrevive (`Path.GetFileName`) — `../evil.sh` vira `evil.sh`; a extensão também é saneada no domínio;
- o **storage** só conhece `{evidenceId}{extensão}` — nome/caminho do usuário nunca chega ao disco;
- **revisão uma única vez**: segunda revisão da mesma evidência → **409**;
- **rejeição exige motivo**; validação não aceita motivo;
- `ReviewedBy/ReviewedAt` carimbam a decisão (quem revisou = ator).

### 2. Storage em disco dev/containers (`PhysicalPdiEvidenceFileStore`)

- Caminho **relativo à base da aplicação**, padrão `var/pdi-evidence` (seção de config `PdiEvidence:StoragePath`); opção de apontar para storage externo continua **fora do escopo** (futura ADR própria quando houver requisito real);
- porta `IPdiEvidenceFileStore` (`SaveAsync`/`OpenReadAsync`/`DeleteAsync`); o comando grava o arquivo **antes** do commit do DB e, se o save falhar, apaga o arquivo (rollback best-effort) — nunca sobra arquivo órfão de escrita parcial;
- **sem endpoint de delete** no escopo da Fase 4 (o §12 não pede; remoção/retention vêm com governança da Fase 5);
- **Descobertas do E2E (corrigidas nesta fase):**
  1. a API roda como `USER $APP_UID` e `/app/var` nasce root-owned → `Directory.CreateDirectory` levantava `UnauthorizedAccessException` (403 do `GlobalExceptionHandler`) em **todo upload**; corrigido com `mkdir/chown` no estágio `api` do `docker/Dockerfile` + **named volume** `pdi-evidence:/app/var/pdi-evidence` no compose (a ownership da imagem é copiada para o volume na 1ª montagem; arquivos sobrevivem a recreate);
  2. o nginx proxy (`docker/nginx.conf`) estava no default de **1 MB** de `client_max_body_size` → 413 antes da API validar o teto de 10 MB; corrigido para **16 MB** (folga do multipart de 10 MB).

### 3. API — upload, revisão e download

| Método | Rota | Detalhe |
|---|---|---|
| `POST` | `/api/v1/pdi/evidences` | `multipart/form-data` (`file`, `phaseId`, `type`, `description`); tipo parseado com `Enum.TryParse` ignoreCase (mesmo padrão do resto da API) |
| `PUT` | `/api/v1/pdi/evidences/{id}/review` | JSON `{decision, rejectionReason}` — `decision` é enum com **`JsonStringEnumConverter` explícito na VM** (descoberta do E2E: sem ele o STJ só aceitava número e **toda** revisão caía em 400 de model binding; o front envia `"Validated"`/`"Rejected"`) |
| `GET` | `/api/v1/pdi/evidences/{id}/download` | download autenticado; `X-Content-Type-Options: nosniff` + `Content-Disposition` pelo content-type armazenado |

- Leitura: as evidências entram no **`GET /pdi/{userId}/accompaniment` único** (5ª lista de `PdiAccompanimentDto`) — mesma economia de requests da Fase 3;
- Histórico: `PdiHistoryType.EvidenceAdded = 4` / `EvidenceReviewed = 5` com `Detail` em pt-BR (`"evidência anexada: certificado — …"`, `"validada"`/`"rejeitada"` + motivo em `Reason`); enum gravado por **conversion string** — **sem migração de histórico**;
- Erros via `GlobalExceptionHandler` (pt-BR): 400 validação, 403 permissão, 404 fase/evidência inexistente, 409 dupla revisão.

### 4. Permissões por ação (mesmo `PdiAccess` da Fase 3)

| Ação | Quem |
|---|---|
| Enviar evidência (`EnsureCanReport`) | Admin ou dono do plano |
| Revisar (`EnsureCanReview`) | Admin ou **gestor do plano** (`plan.ManagerId`) |
| Ler / baixar (`EnsureCanView`) | Admin, dono ou gestor |

Escopo é resolvido pela **fase → estratégia → objetivo → plano** (lookup já existente); estranho ao plano → 403 **antes** de tocar no disco.

### 5. Frontend — 5ª aba do `PdiAccompanimentDialog`

- Aba **"Evidências"** com contador; botão **"+ Evidência"** para Admin/dono: form com input de arquivo (mostra nome/tamanho), select dos 9 tipos e descrição; cliente valida (obrigatório, ≤10 MB) antes de mandar;
- Card por evidência: chips de tipo + status, nome/tamanho, autor/data, motivo de rejeição, **Baixar** (blob → objectURL) e, para Admin/gestor com status pendente, **Revisar** (form com decisão Validar/Rejeitar; rejeitar exige motivo);
- `client.ts` ganhou `download()` (Blob) e passou a aceitar `FormData` sem forçar `Content-Type: application/json`.

## Consequências

- O fluxo §12 (enviar → pendente → validar/rejeitar com motivo) fecha de ponta a ponta com histórico e permissão por ação; a Fase 5 (governança/auditoria) só precisa ler o que já está aqui.
- Storage em disco é **dev/containers**; produção com storage externo (S3 etc.) será ADR própria — a porta `IPdiEvidenceFileStore` já isola a troca.
- Revisão única e ausência de delete/retention são escolhas deliberadas (§12 + escopo da fase); usuários legítimos de "reenviar após rejeição" virão da Fase 5 se o produto pedir.
- Dois fixes de deployment entraram junto (volume gravável + `client_max_body_size`): sem eles a feature só funcionava fora do container.

## Validação (2026-10-07)

- TDD: `PdiEvidenceTests` **10/10** (upload+histórico, fase desconhecida, tipo/arquivo inválidos, regra de revisão/409/motivo, permissões, download, acompanhamento com evidências);
- Suítes: unit **1594/1594** · arch **117/117** · PDI **139/139** · front `tsc`/eslint/build verdes;
- Migração `20261007015235_PdiEvidence` aplicada no dev; containers `api`/`frontend` reconstruídos (healthy);
- E2E `/tmp/opencode/pdi_fase4_e2e.py`: **50/50** (upload válido/400s/traversal saneado, download com bytes+nosniff, revisão validada/rejeitada/dupla/400 sem XSRF, histórico `EvidenceAdded`/`EvidenceReviewed`, 4×403 de operador em plano alheio, purga de resíduo SQL + storage).
