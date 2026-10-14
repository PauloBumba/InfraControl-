namespace InfraControl.Application.AI;

/// <summary>
/// Opções do provider OpenAI-compatible (ADR-050 §3): seção
/// <c>Ai:OpenAiCompatible</c> + chave por env
/// (<c>Ai__OpenAiCompatible__ApiKey</c>). <c>BaseUrl</c> vazio = provider
/// "não configurado" (§44). Cobre OpenAI, OpenRouter, Groq, Together e
/// servidores locais (vLLM, LM Studio, Ollama <c>/v1</c>).
/// </summary>
public sealed class AiOpenAiCompatibleOptions
{
    public string Name { get; set; } = "openai";
    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }

    /// <summary>
    /// Cadastro manual de modelos (ADR-037 §14): devolvido por
    /// <c>ListModelsAsync</c> quando o provider não está configurado.
    /// </summary>
    public List<string> Models { get; set; } = [];
}

/// <summary>
/// Opções do provider Gemini (ADR-050 §4): seção <c>Ai:Gemini</c> +
/// chave por env (<c>Ai__Gemini__ApiKey</c>). ApiKey é obrigatória (a
/// API do Google a exige como query string); sem ela o provider fica
/// "não configurado" (§44).
/// </summary>
public sealed class AiGeminiOptions
{
    public string Name { get; set; } = "gemini";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
    public string? ApiKey { get; set; }
    public List<string> Models { get; set; } = [];
}
