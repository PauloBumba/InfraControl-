# 02 — Provider & Model Manager (S4)

> Sprint S4 da Fase 0 (AI Foundation) — Provider & Model Manager v1.
> ADR-037 · Data: 2026-10-08

## Objetivo

Implementar o registro de providers e modelos de IA por configuração
(ADR-037 §2), com Ollama como primeiro provider (ADR-037 §10) e
roteamento por capacidade (ADR-037 §15).

## Escopo

- **ConfigurationProviderRegistry**: registro v1 de providers por `Ai__*`
  (appsettings/.env) — apenas "ollama" nesta fase
- **OllamaProvider**: adaptador HTTP puro (sem SDK) usando
  `IHttpClientFactory` + `System.Text.Json`
- **AiProviderRouter**: mapeamento de capacidade em (provider, modelo)
  primário + fallback
- **Model discovery**: `GET /api/tags` (Ollama) — serve também como
  teste de conexão (ADR-037 §13)
- **Configuração**: seção `Ai` em appsettings.json (API + Workers)
- **DI**: registro em `DependencyInjection.cs` (Infrastructure)
- **Testes unitários**: validação com fakes (sem Docker nesta máquina)

## Arquitetura

```text
Application/AI/
├── ConfigurationProviderRegistry.cs   (IAiProviderRegistry v1)
├── AiProviderRouter.cs                (IAiProviderRouter)
├── AiOllamaOptions.cs                  (options de config)
└── IAiProvider.cs                     (contrato, S3)

Infrastructure/AI/
└── OllamaProvider.cs                   (IAiProvider implementação)

tests/InfraControl.UnitTests/AI/
├── OllamaProviderTests.cs              (9 testes)
├── ConfigurationProviderRegistryTests.cs (4 testes)
└── AiProviderRouterTests.cs           (6 testes)
```

## Mapeamento de capacidades (ADR-037 §15)

| Capacidade | Modelo default | Fallback |
|------------|----------------|----------|
| Copilot | qwen2.5-coder:1.5b | null |
| IncidentAnalysis | qwen2.5-coder:1.5b | null |
| KnowledgeGeneration | qwen2.5-coder:1.5b | null |
| Reporting | qwen2.5-coder:1.5b | null |
| Automation | qwen2.5-coder:1.5b | null |

## Configuração

### appsettings.json (API + Workers)

```json
{
  "Ai": {
    "Ollama": {
      "BaseUrl": "http://localhost:11434"
    },
    "Routing": {
      "Copilot": {
        "DefaultModel": "qwen2.5-coder:1.5b",
        "FallbackModel": null
      },
      "IncidentAnalysis": {
        "DefaultModel": "qwen2.5-coder:1.5b",
        "FallbackModel": null
      },
      "KnowledgeGeneration": {
        "DefaultModel": "qwen2.5-coder:1.5b",
        "FallbackModel": null
      },
      "Reporting": {
        "DefaultModel": "qwen2.5-coder:1.5b",
        "FallbackModel": null
      },
      "Automation": {
        "DefaultModel": "qwen2.5-coder:1.5b",
        "FallbackModel": null
      }
    }
  }
}
```

### Docker Compose (já configurado no S2)

- Serviço `ollama` no profile `ai`
- Base URL interna: `http://ollama:11434` (DNS por nome de serviço)
- Env: `OLLAMA_BASE_URL` → `Ai__Ollama__BaseUrl` no app-env

## Testes

### OllamaProviderTests (9 testes)

- Sucesso com tokens
- Modelo default qwen2.5-coder:1.5b
- Modelo custom repassado
- Resposta inválida → InvalidOperationException
- HTTP error → HttpRequestException
- ListModelsAsync com lista
- ListModelsAsync vazio
- TestConnectionAsync sucesso → true
- TestConnectionAsync falha → false

### ConfigurationProviderRegistryTests (4 testes)

- Provider ollama existente
- Case-insensitive (OLLAMA)
- Provider inexistente → AiProviderNotConfiguredException
- Provider null → AiProviderNotConfiguredException

### AiProviderRouterTests (6 testes)

- Capacidade válida retorna seleção
- Capacidade com fallback retorna seleção com fallback
- Capacidade inexistente → AiProviderNotConfiguredException
- Modelo default vazio → AiProviderNotConfiguredException
- Modelo default nulo → AiProviderNotConfiguredException
- Todas as 5 capacidades mapeadas retornam seleção válida

## Evolução futura (ADR-037 §2)

- **v2 (banco + UI Admin)**: tabelas `ai_providers` / `ai_models` +
  interface de administração (nome, tipo/protocolo, base URL, API key,
  status) — mesma interface `IAiProviderRegistry` para consumidores

## Pendências

- **Build/testes**: validação manual nesta máquina (sem SDK .NET 10.0.100)
- **Execução/health check**: pendente de ambiente Docker (docs/35 §12)
- **UI Admin**: não implementada nesta fase (ADR-037 §2: "só se o design
  da task exigir")

## Estado em 2026-10-08

- **Concluído**: ConfigurationProviderRegistry, OllamaProvider,
  AiProviderRouter, options, DI, appsettings, testes unit
- **Próximo**: S5 — Runtime health + observabilidade de IA
