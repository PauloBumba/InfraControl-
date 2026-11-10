using System.Diagnostics;
using InfraControl.Infrastructure.Services;

namespace InfraControl.Api.Middleware;

/// <summary>
/// Métricas de tráfego interno da plataforma: mede cada requisição HTTP
/// (template da rota, classe de status, latência em histograma) e alimenta
/// o <see cref="RequestMetricsRecorder"/> em memória — o flush para o
/// Postgres é assíncrono e nunca bloqueia a request.
///
/// Ignora health checks, hubs SignalR (conexões longas distorceriam a
/// latência) e arquivos estáticos de upload. O template da rota evita
/// cardinalidade por id — mesmo princípio do RequestLoggingMiddleware.
/// </summary>
public sealed class RequestMetricsMiddleware(RequestDelegate next)
{
    private static readonly string[] IgnoredPrefixes =
        ["/health", "/hubs/", "/uploads/"];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (IgnoredPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var recorder = context.RequestServices.GetRequiredService<RequestMetricsRecorder>();
        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        catch
        {
            recorder.Record(RouteOf(context) ?? "unmatched", 500, sw.Elapsed.TotalMilliseconds);
            throw;
        }

        var status = context.Response.StatusCode;
        if (status is >= 100 and < 600)
            recorder.Record(RouteOf(context) ?? "unmatched", status, sw.Elapsed.TotalMilliseconds);
    }

    private static string? RouteOf(HttpContext context)
    {
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        if (!string.IsNullOrWhiteSpace(route)) return route;
        // Fallback: path sem query (pode ter cardinalidade por id — raro,
        // só para endpoints fora do roteamento de controllers).
        var path = context.Request.Path.Value ?? string.Empty;
        return path.Length > 200 ? path[..200] : path;
    }
}
