using InfraControl.Application.Common.Interfaces;

namespace InfraControl.Api.Middleware;

/// <summary>
/// ADR-021: o correlation id da requisição em andamento, lendo o que o
/// RequestLoggingMiddleware colocou em <see cref="HttpContext.Items"/>. Fora de
/// uma requisição (ou se o middleware não rodou) devolve <c>null</c>.
/// </summary>
public sealed class HttpContextCorrelation(IHttpContextAccessor accessor) : ICurrentCorrelation
{
    public string? Id
        => accessor.HttpContext?.Items.TryGetValue(RequestLoggingMiddleware.CorrelationIdProperty, out var value) == true
            ? value?.ToString()
            : null;
}
