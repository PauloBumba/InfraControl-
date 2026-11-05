# ADR-065: Modelo único de IA por default (override opcional por capacidade)

- **Status**: aceito (2026-10-27 — usuário direcionou "modelo único, não
  um modelo por função". Reversível por nova ADR.)
- **Data**: 2026-10-27
- **Tasks**: `F47-T001` (TASKS.md — Fase 47); relação com `F47-T002`
  (runtime Ollama ao vivo: profile `ai` + LB + modelo no cadastro)
- **Amenda**: ADR-037 §15 (roteamento por capacidade) e ADR-057 §3
  (rotas por capacidade no cadastro) — ambos **preservados**, com um
  nível `Default` acima

## Contexto

A resolução `capacidade → (provider, modelo)` existe em **dois lugares**,
ambos duplicados 5× (uma linha por capacidade em `AiCapability`):

1. **Config** — `Ai:Routing:{Copilot,IncidentAnalysis,KnowledgeGeneration,
   Reporting,Automation}` no `appsettings.json` (ADR-037 §15), hoje com
   as 5 entradas idênticas (`ollama` / `qwen2.5-coder:1.5b`).
2. **Cadastro** — tabela `ai_provider_capabilities`, populada pelo seed
   idempotente a partir da config (ADR-057 §3), com uma linha por
   capacidade.

Pedido do usuário (2026-10-27): **um único modelo**, não um modelo por
função — configurar o modelo **em um lugar só** e ter o mesmo valor em
tudo, sem espalhar a mesma string por 5 (config) + 5 (cadastro) entradas.

Também foi diagnosticado no mesmo dia o motivo de a IA não responder —
**3 camadas** (F47-T002): (1) o **LB do profile `ai` estava parado**, então
`ollama-lb` não resolvia de dentro da API; (2) o Ollama instalado na
máquina roda como usuário `ollama` e não enxerga os 13 GB de modelos de
`/home/bumba/.ollama/models` (e escuta só em `127.0.0.1`, inalcançável de
qualquer container); (3) os volumes do compose só carregavam
`qwen2.5-coder:1.5b` + `nomic-embed-text`. Esse é problema de **infra**,
independente desta ADR, mas pré-requisito prático para validá-la ao vivo
— resolvido em `F47-T002` pela arquitetura já decidida em ADR-041
(compose `profile ai` com 3 réplicas + LB), não pelo Ollama da máquina.

## Decisão (proposta)

1. **`Ai:Routing:Default` vira a fonte do modelo único.** Nova entrada:
   ```json
   "Routing": {
     "Default": {
       "Provider": "ollama",
       "DefaultModel": "qwen2.5-coder:1.5b",
       "FallbackProvider": null,
       "FallbackModel": null
     }
   }
   ```
   Um `Provider` + `DefaultModel` + `Fallback*` para **todas** as
   capacidades.
2. **As 5 entradas por capacidade viram override opcional.** Se
   ausentes, vazias ou nulas, a capacidade **herda o `Default`**. Isso
   mantém o ADR-037 §15 válido (o mapa por capacidade continua
   existindo) sem exigir 5 duplicatas no `appsettings`.
3. **Seed usa o `Default`.** `AiProviderSeeder` popula todas as
   capacidades a partir de `Routing:Default` (uma fonte). Linha já
   existente no cadastro **não é sobrescrita** (autoridade do operador,
   ADR-057 §2 preservada).
4. **Ordem de resolução no `AiProviderRouter`** (cascata, a primeira
   fonte que resolver):
   1. **Cadastro** (`ai_provider_capabilities`) — linha da capacidade,
      autoridade do operador (ADR-057 D3-A);
   2. **Config por capacidade** — só quando preenchida (override);
   3. **Config `Routing:Default`** — modelo único;
   4. nenhuma → `AiProviderNotConfiguredException` (estado §44,
      **nunca** resposta inventada — invariante preservada).
5. **Não é migração de banco.** A tabela `ai_provider_capabilities`
   permanece com uma linha por capacidade (a UI Admin de ADR-057 §6
   continua editando por capacidade). O que muda é a **fonte do
   default** ao criar/editar e o fallback de resolução.
6. **`qwen2.5-coder:1.5b`** é o modelo único de partida — o mesmo que já
   roda nas 3 réplicas do compose (nenhum download novo; pedido do
   usuário em 2026-10-27: "usa apenas o 1.5").

## Alternativas

| # | Alternativa | Por que não |
|---|-------------|-------------|
| A | **Tabela única `ai_provider_default` (uma linha global)** | Exige migração nova e reescreve a UI Admin de rotas do ADR-057 §6 por um ganho que a cascata `Default` já dá sem tocar no schema |
| B | **Só unificar os valores** (manter 5 entradas idênticas) | Não resolve o pedido — continua "modelo por função", só sem divergência acidental |
| C | **Modelo único rígido, sem override** | Perde a capacidade de exceção legítima (ex.: `Automation` com modelo maior depois) — o override opcional custa uma cascata e mantém a porta aberta |

## Decisões fechadas (2026-10-27)

| # | Questão | Decisão |
|---|---------|---------|
| D1 | **Onde vive o modelo único** | **(A) `Ai:Routing:Default` na config** com cascata de resolução — sem migração de schema; cadastro continua autoridade quando a linha existe |
| D2 | **Override por capacidade** | **(A) mantido e opcional** — as 5 entradas podem ser omitidas; herdam o `Default` |
| D3 | **Modelo de partida** | **(A) `qwen2.5-coder:1.5b`** — já presente nas 3 réplicas do compose (986 MB, roda bem em CPU), zero download novo; um modelo maior (ex.: `7b`) é evolução futura se a latência/qualidade exigir |

## Consequências

- `AiRoutingOptionsConfiguration` ganha `Default`; `AiProviderRouter` e
  `AiProviderSeeder` passam a ler `Default` antes do mapa por capacidade
  (testes novos + ajuste nos existentes).
- `appsettings.json` (API/Workers) encolhe de 5 blocos duplicados para 1
  (`Routing:Default`); as entradas por capacidade são omitidas por padrão
  e reaparecem só quando o operador quiser exceção. Compat: config antiga
  com só as 5 entradas continua funcionando (sem `Default` → cascatada cai
  no mapa por capacidade).
- A **infra** (`F47-T002`) é pré-requisito: sem modelo em `/api/tags` ou
  sem LB alcançável qualquer configuração cai em §44 — esta ADR não muda
  isso.
- UI Admin de rotas (ADR-057 §6) pode, num passo futuro, oferecer
  "aplicar modelo único a todas as capacidades" — **fora do escopo**
  desta ADR (seria criar/editar 5 linhas via API já existente).
