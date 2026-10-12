namespace InfraControl.Contracts.AI;

/// <summary>
/// Mensagem de requisição de inferência de IA para fila RabbitMQ (ADR-044).
/// Permite escalar o Ollama com múltiplos workers consumindo a fila.
/// </summary>
public record AiInferenceMessage
{
    public required string Id { get; init; }
    public required string Prompt { get; init; }
    public required string Model { get; init; }
    public string? SystemPrompt { get; init; }
    public int? MaxTokens { get; init; }
    public float? Temperature { get; init; }
    public required string ReplyToQueue { get; init; } // Fila para resposta
    public required string CorrelationId { get; init; }
}

/// <summary>
/// Mensagem de resposta de inferência de IA (ADR-044).
/// </summary>
public record AiInferenceResponse
{
    public required string Id { get; init; }
    public required string CorrelationId { get; init; }
    public required string Response { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public long TokensUsed { get; init; }
    public TimeSpan Duration { get; init; }
}
