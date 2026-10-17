# ADR-037: Provider & Model Manager — abstração de provedores e modelos

- **Status**: aceito
- **Data**: 2026-10-08
- **Tasks**: FAI-T001 (Fase AI — sprint S1 de `docs/35`; blueprint `docs/34` §9–§15)

## Contexto

A blueprint pede: abstração única de provider (§10), Ollama como default
local provisionado por Docker (§11), provedores externos configuráveis
pelo admin (§11–§12), teste de conexão (§13), descoberta de modelos
(§14) e modelos por capacidade com default + fallback (§15). O
repositório não tem provedor, chave, cliente ou orçamento de IA; o
AGENTS.md proíbe usar credenciais reais. Entregar de uma vez contrato +
adaptadores + tabelas + UI Admin violaria a regra de "task pequena".

## Decisão

1. **Contrato `IAiProvider`** em `Application/AI/` (assinatura exata no
   sprint S3) com adaptador **`OllamaProvider`** em
   `Infrastructure/AI/` — HTTP puro via `IHttpClientFactory` +
   `System.Text.Json`, **sem SDK de terceiros** (§10). URL interna
   `http://ollama:11434` no compose (ADR-041); `localhost:11434` em
   dev local fora do Docker.
2. **Registro de providers em duas fases**, atrás da mesma interface
   `IProviderRegistry` (consumidores não mudam):
   - v1: `ConfigurationProviderRegistry` — `.env`/appsettings (`Ai__*`,
     já preparados no compose desta task);
   - evolução: `DatabaseProviderRegistry` — tabelas `ai_providers` /
     `ai_models` + interface Admin da §11–§12 (nome, tipo/protocolo,
     base URL, API key, status), quando a task S4 assim o exigir.
3. **Modelos por capacidade** (§15): enum `AiCapability` (Copilot,
   IncidentAnalysis, KnowledgeGeneration, Reporting, Automation) com
   mapeamento `(provider, modelo)` por capacidade + `DefaultModel` +
   `FallbackModel` na configuração — o registro de execução guarda
   provider/modelo/capacidade por request (§15).
4. **Fallback** (§54): primary → erro/timeout → fallback (se
   configurado) → erro final; toda troca auditada com motivo.
5. **Teste de conexão/descoberta** (§13–§14): Ollama expõe
   `GET /api/tags` (modelos instalados) — a mesma chamada serve de
   health check e "teste de conexão"; provider sem endpoint de
   descoberta = cadastro manual na configuração. **OpenAI-compatible /
   Gemini ficam fora** desta fase: exigem ADR próprio + chave real.
6. **Modelo inicial**: `qwen2.5-coder:1.5b` como default de config
   (literal da §11); revisão do modelo com **evidência de execução**
   no S4 (§63: config sem execução não é feature concluída).

## Consequências

- Fase 1 sem credencial, sem custo e sem provider pago — decisão de
  provedor externo vira ADR novo.
- A "Provider & Model Manager" da §11 é entrega incremental (config →
  banco → UI) sem reescrever o gateway. **Concretizada por
  [ADR-057](ADR-057-AI-PROVIDER-REGISTRY.md) (2026-10-16)**: cadastro
  no banco (`ai_providers`/`ai_provider_capabilities`) + UI Admin
  (`/admin/ai-providers`); o config por env permanece como fallback
  (D3-A) e o estado "não configurado" continua valendo (§44).
- Capacidades dão telemetria/custo por caso de uso desde o 1º request
  (§15/§55).
