# 03 — Integração de fornecedores de IA (F49)

> Origem: Fase 49 — sistema universal de integração/validação de
> modelos. O usuário pediu explicitamente um fluxo em que **o admin
> tudo sabe** (protocolo, endpoint, chave, modelo) e a plataforma é
> escalável **sem codar um adaptador por fornecedor**.

## Princípio (F49)

**Protocolo é configuração, não código.** O cadastro
(`ai_providers.protocol`) diz qual protocolo o endpoint fala; o
registry instala o adaptador genérico correspondente. Um fornecedor
novo (DeepSeek, Mistral, Groq, Together…) = configuração no Admin —
zero deploy, zero adaptador.

| Protocolo | Adaptador | Quando usar |
| --- | --- | --- |
| `openai-compatible` (default) | `OpenAiCompatibleProvider` | OpenAI, DeepSeek, Mistral, Groq, Together, OpenRouter, vLLM, LM Studio, Ollama em `/v1` |
| `ollama` | `OllamaProvider` | API nativa do Ollama (`/api/tags`, `/api/generate`) |
| `gemini` | `GeminiProvider` | API do Google (`/v1beta`, key em query) |
| `anthropic` | `AnthropicProvider` | Messages API (`/v1/messages`, header `x-api-key` + `anthropic-version`) |

## Passo a passo — adicionar um fornecedor novo

1. **Validar antes de gravar** (dry-run, nada persiste): Admin →
   Providers de IA → **Validar conexão**. Informe protocolo, URL base,
   chave (write-only) e modelo (opcional). A resposta traz as fases:
   - `conectividade` — o endereço responde HTTP?;
   - `autenticação` — a chave é aceita no probe de modelos **do
     protocolo** (Bearer, `?key=`, `x-api-key`);
   - `modelos` — a lista oficial do fornecedor veio? (nunca invente
     nomes);
   - `modelo` — o modelo pedido existe na lista?
2. **Cadastrar** com o protocolo correto (mesmo formulário do dry-run,
   agora em "Cadastrar provider"). A chave é cifrada (AES-GCM) e nunca
   volta plaintext — só `••••XXXX`.
3. **Testar a linha gravada**: botão **Testar conexão** / **Listar
   modelos** da linha na tabela (usa a chave do banco — D3).
4. **Rotear a capacidade**: em "Rotas por capacidade", escolha o
   provider (e o fallback). O resolve é cadastro-first (ADR-057 D3).

### Ordem de resolução (D3 / D3-A)

`capability → linha ativa em ai_providers → adaptador do protocolo da
linha`; sem linha, cai na configuração (`Ai:*` no appsettings/env) com
o adaptador fixo registrado em código. Linha inativa **recusa**
(§44).

## Validação em fases (F49-T001/T004)

- Endpoint: `POST /api/v1/admin/ai-providers/validate` (Administer).
  Body: `{ "protocol": "OpenAiCompatible|Ollama|Gemini|Anthropic",
  "baseUrl": "...", "apiKey": "...", "model": "..." }`.
- Dry-run: aceita endpoint+chave **arbitrários** sem tocar no
  cadastro; a chave passa em memória e **nunca** é persistida, logada
  ou devolvida.
- HTTP 200 no caminhão **não** prova key válida — por isso as fases
  são separadas (401/403 ≠ "sem rede").
- **Google (Gemini)**: key inválida volta **400 `INVALID_ARGUMENT` +
  `reason: API_KEY_INVALID`** ("API key not valid") — não 401
  (formato oficial: `ai.google.dev/gemini-api/docs/generate-content/
  api-errors`). O validador detecta o motivo e reporta na fase
  **autenticação** (não em "modelos"). Causa comum de
  `API_KEY_INVALID` com a key "parecendo certa": a **Generative
  Language API não habilitada** no projeto da key (ou key de outra
  conta) — habilitar no Google AI Studio e aguardar ~1 min.
  Quota esgotada é 429; saldo é 402/403 — nunca 400.
- Mensagens em pt-BR, sem vazar a chave.

## Capabilities detectadas (§7 — honestidade)

`detectedCapabilities` só traz o que o **fornecedor expõe**:

- **Gemini**: `supportedGenerationMethods` por modelo
  (`generateContent` → `text`, `streamGenerateContent` → `streaming`).
- **OpenAI-compatible / Anthropic / Ollama**: a lista de modelos não
  traz capabilities → campo vazio ("não expostas pelo fornecedor").

Streaming/tools/imagens **não são inferidos por heurística de nome de
modelo** — quando o fornecedor não expõe, fica `unknown` documentado.
O roteamento por capacidade continua sendo decisão do admin na rota.

## Segurança

- Chave: AES-GCM no servidor; DTO de leitura só `apiKeySet` +
  máscara; write-only no form (vazio = preserva).
- Logs e erros de validação nunca contêm a chave (a mensagem traz
  apenas o texto de erro do fornecedor, truncado).
- Escrita atrás de policy `Administer`; backend é a autoridade.

## Evidências ao vivo (dev, 2026-11-05)

| Cenário | Resultado |
| --- | --- |
| Dry-run Gemini com key inválida | conectividade ✓, autenticação ✗ — "400 API_KEY_INVALID" (formato oficial do Google), modelos ignorado |
| Test-connection do provider `gemini` (key do banco) | `ok: true` |
| Dry-run Ollama real (`http://ollama-lb:80`) | fases todas ✓; modelos `nomic-embed-text:latest`, `qwen2.5-coder:1.5b`; `modelCheck` ✓ |
| Test-connection do provider `ollama` | `ok: true` |
