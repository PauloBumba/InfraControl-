using Microsoft.AspNetCore.Antiforgery;

namespace InfraControl.Api.Middleware;

/// <summary>
/// F1-T015 / ADR-017: escrita autenticada por cookie exige token de
/// antiforgery. A política fica aqui, no pipeline, e não em atributo de
/// controller — assim um controller novo nasce protegido por padrão, em vez de
/// depender de alguém lembrar de declarar.
///
/// Regras:
/// - só método de escrita (POST/PUT/PATCH/DELETE) é validado; leitura não
///   muda estado e não paga o custo de um HMAC por request;
/// - anônimo não é validado: login, registro, convite, reset de senha e
///   POST /auth/google não têm cookie de sessão para explorar, e no login o
///   token ainda nem existe;
/// - falha é <see cref="AntiforgeryValidationException"/>, traduzida em 400 no
///   envelope pelo GlobalExceptionHandler (nunca 403 — o usuário está
///   autenticado — nem 500: é um problema de request, não do servidor).
/// </summary>
public sealed class AntiforgeryCookieMiddleware(RequestDelegate next, IAntiforgery antiforgery)
{
    private static readonly string[] WriteMethods =
        [HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete];

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresValidation(context))
            await antiforgery.ValidateRequestAsync(context);

        await next(context);
    }

    private static bool RequiresValidation(HttpContext context)
    {
        var isWrite = Array.Exists(
            WriteMethods,
            method => string.Equals(method, context.Request.Method, StringComparison.OrdinalIgnoreCase));

        return isWrite && context.User?.Identity?.IsAuthenticated == true;
    }
}
