namespace InfraControl.Application.AI;

/// <summary>
/// Estado "não configurado" (docs/34 §44): capacidade sem mapeamento no
/// router ou provider ausente do registro. A chamada falha de forma
/// explícita — nunca com resposta inventada (§3).
/// </summary>
public sealed class AiProviderNotConfiguredException(string message)
    : InvalidOperationException(message);

/// <summary>
/// Falha de execução no gateway: primário (sem fallback disponível ou
/// desabilitado) ou fallback — com a causa original preservada em
/// <see cref="Exception.InnerException"/>.
/// </summary>
public sealed class AiGatewayException(string message, Exception? innerException = null)
    : Exception(message, innerException);
