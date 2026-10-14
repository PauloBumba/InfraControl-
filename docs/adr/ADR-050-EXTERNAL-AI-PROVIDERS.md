# ADR-050: External AI Providers — OpenAI-compatible e Gemini

- **Status**: aceito
- **Data**: 2026-10-13
- **Tasks**: Fase A do plano de execução da IA (`docs/36-PLANO-AI-EXECUCAO.md`);
  amenda do ADR-037 §5 ("OpenAI-compatible / Gemini ficam fora desta
  fase: exigem ADR próprio + chave real")

## Contexto

A blueprint prevê providers externos configuráveis (docs/34 §10–§15):
tipos `OpenAI Compatible`, `Google Gemini` e `Custom HTTP`, com o admin
cadastrando Nome, Protocolo, Base URL, API Key e Status. O ADR-037
adiou os externos até haver ADR próprio + chave real. O registro v1
(`ConfigurationProviderRegistry`) só resolvia `"ollama"` e o
`AiProviderRouter` hardcodava o provider primário — impossível rotear
`Copilot→ollama` e `IncidentAnalysis→openai` por configuração. O
AGENTS.md proíbe credenciais reais no repositório: a chave só pode
chegar por env/config do operador, nunca no repo.

## Decisão

1. **Registro multi-provider**: `ConfigurationProviderRegistry` passa
   a receber `IEnumerable<IAiProvider>` e resolve por `Name`
   (case-insensitive). Consumidores (gateway, router, health) não mudam.
2. **Roteamento com provider por capacidade**: `AiRoutingModelOptions`
   ganha `Provider` e `FallbackProvider`; vazio = `"ollama"` (backward
   compat com ADR-037 §15). Fallback: `FallbackProvider ?? Provider`,
   validado no registry no `Resolve` (falha cedo, §44).
3. **`OpenAiCompatibleProvider`** (Infrastructure/AI): HTTP puro via
   `IHttpClientFactory` (named client `openai-compatible`) +
   `System.Text.Json`, sem SDK (ADR-037 §10). `POST
   {BaseUrl}/chat/completions` com `Authorization: Bearer {ApiKey}`
   **opcional** (servidores locais sem chave: vLLM, LM Studio, Ollama
   `/v1`); `GET {BaseUrl}/models` = descoberta + teste de conexão
   (§13–§14). Nome default `openai`; cobre OpenAI, OpenRouter, Groq e
   Together (docs/34 §12).
4. **`GeminiProvider`**: `POST
   {BaseUrl}/models/{model}:generateContent?key={ApiKey}`;
   `GET {BaseUrl}/models?key={ApiKey}` = descoberta + teste. Nome
   default `gemini`. ApiKey é obrigatória (a API do Google a exige).
5. **Estado degradado §44**: BaseUrl vazio (ou ApiKey vazia no Gemini)
   ⇒ `CompleteAsync` lança `AiProviderNotConfiguredException`,
   `TestConnectionAsync` = false e `ListModelsAsync` devolve o cadastro
   manual da configuração (`Models`, ADR-037 §14 "provider sem endpoint
   de descoberta = cadastro manual"). Nunca resposta inventada.
6. **Chaves só por env**: `Ai__OpenAiCompatible__ApiKey` /
   `Ai__Gemini__ApiKey` vindas de `OPENAI_API_KEY` / `GEMINI_API_KEY` no
   `.env` do operador; placeholders vazios no `.env.example`,
   appsettings e compose — nunca valor real no repositório (AGENTS.md).
7. **Escopo desta fase**: registro + configuração + adaptadores + testes
   com `HttpMessageHandler` mockado. Evidência live (chave real, geração
   real) fica para o ambiente do operador (docs/35 §46: documentação ≠
   evidência).

## Consequências

- Capacidades podem misturar provedores por configuração sem tocar em
  código de negócio; auditoria/telemetria por provider continuam valendo.
- Sem Docker/chave neste ambiente, validação = unit/arch; live depende
  do operador.
- `DatabaseProviderRegistry` + UI Admin (ADR-037 §2/§11–§12) continuam
  como evolução desta mesma interface — esta fase é a v1 multi-provider
  da configuração.
- `Custom HTTP` fica para demanda real (ADR próprio); proibição de SDK
  de terceiros mantida (ADR-037 §10).
