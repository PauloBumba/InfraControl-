namespace InfraControl.Domain.Enums;

/// <summary>
/// F49-T002: protocolo do provider de IA — escolhido pelo operador no
/// cadastro (não por código/fornecedor). `OpenAiCompatible` é o padrão
/// universal (DeepSeek, Mistral, Groq, Together…); `Anthropic`/`Gemini`/
/// `Ollama` têm protocolos próprios. Adicionar um fornecedor que fale
/// um destes protocolos = configuração no admin, sem deploy.
/// </summary>
public enum AiProviderProtocol
{
    OpenAiCompatible = 0,
    Ollama = 1,
    Gemini = 2,
    Anthropic = 3,
}
