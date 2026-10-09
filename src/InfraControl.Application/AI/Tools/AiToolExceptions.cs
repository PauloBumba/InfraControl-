namespace InfraControl.Application.AI.Tools;

/// <summary>Erro base das tools de IA (mensagens em pt-BR).</summary>
public class AiToolException : Exception
{
    public AiToolException(string message) : base(message) { }
    public AiToolException(string message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>Tool inexistente no registry (estado "não configurado" §44).</summary>
public sealed class AiToolNotFoundException(string toolName)
    : AiToolException($"Tool de IA '{toolName}' não está registrada (estado não configurado, §44).");

/// <summary>Ator sem a role exigida pela tool (§41 — mesma regra do pipeline MediatR).</summary>
public sealed class AiToolAuthorizationException(string message)
    : AiToolException(message);

/// <summary>Tool excedeu o timeout do descriptor (§52).</summary>
public sealed class AiToolTimeoutException(string toolName, TimeSpan timeout)
    : AiToolException($"Tool '{toolName}' excedeu o timeout de {timeout.TotalSeconds:0.#} s.");

/// <summary>Argumento inválido para a tool (contrato §52).</summary>
public sealed class AiToolArgumentException(string message)
    : AiToolException(message);
