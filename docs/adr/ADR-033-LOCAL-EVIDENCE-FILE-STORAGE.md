# ADR-033 — Evidências de Task em disco local

## Status

Accepted (2026-10-07 — escolha "Disco local + ADR-033" do usuário via
opção apresentada no checkpoint da F23-T004/P4c; decisão arquitetural
sob controle humano, conforme AGENTS.md).

## Contexto

A F23-T004/P4c (Plano de Execução §11) exige anexo de **arquivo de
evidência** na task — é o primeiro upload de arquivo do produto, sem
precedente no código. Três caminhos avaliados no checkpoint:

| Opção | Prós | Contras |
|---|---|---|
| **Disco local** (escolhido) | sem infra nova; funciona sem Docker no dev | arquivo fora do backup do banco; limitado a uma réplica |
| Bytea no banco | backup junto com o DB | infla o banco; limite menor |
| Só link/nota | nenhuma decisão de storage | não é anexo de arquivo |

## Decisão

1. **Pasta configurável** `Storage:EvidencePath` (seção `Storage` no
   appsettings; caminho absoluto ou relativo à pasta da aplicação,
   default `evidence-files`), resolvido com `Path.GetFullPath`.
2. **Nome em disco = `GUID + extensão`** (`StoredName` gerado pela
   entidade). O nome original do usuário é **só metadado** na tabela
   `task_evidences` — nunca vira caminho (travessia de diretório fora
   de questão por construção).
3. **Porta `IEvidenceFileStore`** (Application) com implementação
   `LocalEvidenceFileStore` (Infrastructure): trocar de backend no
   futuro (S3/MinIO) = outra implementação da porta, sem tocar nos
   handlers.
4. **Limites validados no domínio** (400 pt-BR): 10 MB
   (`TaskEvidence.MaxFileSizeBytes`), allowlist de extensões
   (`png jpg jpeg gif webp pdf txt log csv json xml zip`), nome ≤ 200
   (normalizado para o último segmento antes de validar), nota ≤ 300,
   tipo de conteúdo ≤ 100 (default `application/octet-stream`).
5. **Exclusão**: registro sai da tabela E arquivo sai do disco; erro de
   IO no disco é logado e **não** bloqueia a exclusão do registro (o
   banco é a fonte de verdade).
6. **Download** pela API autenticada (`GET /tasks/evidences/{id}/download`,
   leitura `Authorize`) com `Content-Disposition` do nome original —
   o arquivo não é servido estaticamente.

## Consequências

- O **backup do PostgreSQL não leva os arquivos** (pendência conhecida;
  revisar se o backup ganhar cópia de diretórios).
- Pode sobrar arquivo órfão se a escrita do banco falhar após o save
  em disco (tolerado: extensão pequena e caso raro).
- **Uma réplica só**: réplicas/múltiplos hosts exigem storage
  compartilhado → nova decisão (novo ADR) antes de escalar.
- Multipart de até ~10 MB usa a validação do domínio (limit default do
  Kestrel, 30 MB, deixa passar e o 400 pt-BR responde primeiro).
