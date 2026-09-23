using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Application.Response;
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
            KeyNotFoundException => (
                StatusCodes.Status404NotFound, [UserMessage(exception)]),
            UnauthorizedAccessException => (
                isAuthenticated
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status401Unauthorized,
                [UserMessage(exception)]),
            ArgumentException => (
                StatusCodes.Status400BadRequest, [UserMessage(exception)]),
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
