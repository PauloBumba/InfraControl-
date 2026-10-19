using InfraControl.Application.AI;
using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Application;
using InfraControl.BuildingBlocks.Application.Response;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;

namespace InfraControl.Api.Middleware;

/// <summary>
/// F0-T023: ponto único que embrulha exceções em ApiResponse
/// { isSuccess, data, errors } com o status HTTP correto. O conteúdo
/// (mensagem pt-BR) nasce nos handlers/serviços/domínio — aqui só o
/// transporte embrulha. Detalhes internos nunca vazam para o cliente.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var isAuthenticated = httpContext.User?.Identity?.IsAuthenticated == true;
        var (statusCode, errors) = Map(exception, isAuthenticated);

        if (statusCode >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception → {StatusCode} (TraceId {TraceId})",
                statusCode, httpContext.TraceIdentifier);
        else
            logger.LogWarning(exception, "Handled exception → {StatusCode} (TraceId {TraceId})",
                statusCode, httpContext.TraceIdentifier);

        // F35-T002: 429 Too Many Requests leva Retry-After quando o
        // limiter informou a janela restante.
        if (exception is RateLimitExceededException { RetryAfter: { } retryAfter })
            httpContext.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(errors), cancellationToken);
        return true;
    }

    /// <summary>
    /// Exceção → (status, mensagens para a UX). A ORDEM importa:
    /// RequestValidationException e DuplicateRequestException herdam de
    /// InvalidOperationException e precisam ser casadas antes dele.
    /// </summary>
    private static (int StatusCode, IReadOnlyCollection<string> Errors) Map(
        Exception exception,
        bool isAuthenticated)
        => exception switch
        {
            RequestValidationException validation => (
                StatusCodes.Status400BadRequest, validation.Errors),
            // F1-T015/ADR-017: token de antiforgery ausente, forjado ou de
            // outro usuário. 400 (não 403): o usuário ESTÁ autenticado, o que
            // falta é um header válido — é request inválida, não falta de
            // permissão. A mensagem é genérica de propósito: distinguir
            // "faltou" de "invalidou" seria oráculo sobre o token em uso.
            AntiforgeryValidationException => (
                StatusCodes.Status400BadRequest,
                ["A requisição não pôde ser validada. Recarregue a página e tente novamente."]),
            // IA (ADR-052 §6 · docs/34 §44/§57): estado degradado e falha
            // de provider viram status amigáveis ANTES do catch-all de
            // InvalidOperationException — nunca 500 genérico nem resposta
            // inventada. Herdam de InvalidOperationException: a ORDEM
            // destes casos importa.
            AiProviderNotConfiguredException => (
                StatusCodes.Status503ServiceUnavailable,
                ["O serviço de IA não está configurado neste ambiente. Contate o administrador."]),
            AiGatewayException => (
                StatusCodes.Status502BadGateway,
                ["Não foi possível obter resposta do serviço de IA. Tente novamente em instantes."]),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound, [UserMessage(exception)]),
            UnauthorizedAccessException => (
                isAuthenticated
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status401Unauthorized,
                [UserMessage(exception)]),
            ArgumentException => (
                StatusCodes.Status400BadRequest, [UserMessage(exception)]),
            // F35-T002 (caça-bugs backend F2): rate limit é 429 Too Many
            // Requests (com Retry-After), nunca 409. ANTES de Invalid-
            // OperationException — RateLimitExceededException herda de
            // Exception, mas a ordem documenta a precedência.
            RateLimitExceededException => (
                StatusCodes.Status429TooManyRequests, [UserMessage(exception)]),
            DuplicateRequestException => (
                StatusCodes.Status409Conflict, [UserMessage(exception)]),
            InvalidOperationException => (
                StatusCodes.Status409Conflict, [UserMessage(exception)]),
            _ => (
                StatusCodes.Status500InternalServerError,
                ["Ocorreu um erro inesperado. Tente novamente ou contate o suporte."])
        };

    /// <summary>
    /// Mensagem pronta para a UX: remove o sufixo técnico
    /// "(Parameter 'x')" que o .NET acrescenta ao ArgumentException.
    /// </summary>
    private static string UserMessage(Exception exception)
    {
        var message = exception.Message;
        var parameterIndex = message.IndexOf(" (Parameter '", StringComparison.Ordinal);
        return parameterIndex >= 0 ? message[..parameterIndex].Trim() : message;
    }
}
