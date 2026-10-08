# ADR-038: AI Context Engine & Hybrid RAG — pgvector no Postgres atual

- **Status**: aceito
- **Data**: 2026-10-08
- **Tasks**: FAI-T001 (Fase AI — sprint S1 de `docs/35`; blueprint `docs/34` §16–§19, §35)

## Contexto

A §16 desenha o Context Engine como capacidade **transversal** (Copilot,
Investigation, Knowledge, Reporting, Automation) com busca híbrida
(§17), evidências estruturadas (§19) e dados em tempo real via tools
(§18). A §35 manda: desacoplar do provider de embeddings, **avaliar
Postgres + pgvector antes de introduzir outro banco** e "reutilizar
infraestrutura existente antes de adicionar outra tecnologia" (§6).
Hoje: PostgreSQL 17 no compose (ADR-002), módulo Knowledge com artigos
publicados, zero código vetorial.

## Decisão

1. **Vector store = pgvector no PostgreSQL existente** (extensão
   `CREATE EXTENSION IF NOT EXISTS vector`) — avaliação feita conforme
   §35/§6; nenhum banco vetorial externo (Pinecone/Weaviate/Chroma/…)
   nesta fase. **Pendência de imagem**: a `postgres:17-alpine` oficial
   não embute a extensão — o S8 avalia trocar para
   `pgvector/pgvector:pg17` ou build local; troca de imagem só com
   evidência (§46).
2. **Contratos em `Application/AI/`** (port — domínio/Infrastructure
   nunca referenciados de fora da camada):
   - `IEmbeddingProvider` (implementação Ollama, mesma base do
     ADR-037 — modelo de embedding definido no S8);
   - `IVectorStore` (implementação pgvector em Infrastructure);
   - `IRetrievalService` — **busca híbrida v1** (§17): vetor
     (`cosine`) + keyword (ILIKE/FTS — reaproveita o `term` do K7) +
     metadados (filtros do K7: categoria/autor/tipo/tag) → merge;
   - `IReranker` — v1 heurística (fora do match + fonte + recência);
     rerank por LLM = evolução com ADR;
   - `IContextBuilder` → **`EvidenceSet`** com o modelo da §19
     (fonte, tipo, timestamp, confiança Confirmed/Probable/Possible/
     Unknown, citação, tool, traceId).
3. **Fonte inicial = artigos `Published` do módulo Knowledge** (único
   texto curado do sistema); Incident/Task/Documento entram por task
   própria (§17 lista as fontes). Chunking por artigo/seção com limite
   documentado no S8; ingesta dispara na publicação (workflow ADR-034).
4. **Nada no domínio conhece vetor**: KnowledgeArticle não ganha
   campo de embedding — o índice vive em tabela própria da infra.

## Consequências

- Sem banco novo e sem custo (§35/§6 atendidos); arquitetura de
  contratos permite trocar pgvector por outro store sem tocar
  consumidores.
- Migração nova (extensão + índice vetorial) fica **pendente de
  ambiente**, como as 12 atuais — script validado, execução quando
  houver Docker.
- Sem Ollama em execução, embeddings não rodam: o Context Engine nasce
  em estado "não configurado" degradado (§44), nunca com dado
  inventado.
