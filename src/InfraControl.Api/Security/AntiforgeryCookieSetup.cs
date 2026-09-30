using Microsoft.AspNetCore.Antiforgery;

namespace InfraControl.Api.Security;

/// <summary>
/// F1-T015 / ADR-017: configuração do double-submit de CSRF em UM lugar só.
/// O Program.cs e os testes usam este método — assim o teste não valida uma
/// configuração diferente da que roda em produção.
///
/// POR QUE DOIS COOKIES: o IAntiforgery do ASP.NET trabalha com DOIS valores.
/// O cookie guarda o <c>cookie-token</c> (o segredo que o navegador devolve
/// sozinho) e o header precisa do <c>request-token</c>, que é o cookie-token
/// mais um nonce. Mandar o cookie-token no header é o erro "swapped" e a
/// escrita cai em 400. Como o JavaScript não tem como deriving um do outro, o
/// servidor precisa publicar o request-token num cookie legível — e aí o
/// cookie-token precisa de outro nome, senão os dois se atropelam. Por isso:
/// <see cref="CookieName"/> é o segredo HttpOnly do framework e
/// <see cref="ReadableCookieName"/> é o que o JS copia para o header.
/// </summary>
public static class AntiforgeryCookieSetup
{
    /// <summary>Cookie legível pelo JS (mesmo nome do Angular, por convenção).</summary>
    public const string ReadableCookieName = "XSRF-TOKEN";

    /// <summary>
    /// O cookie-token do próprio framework. Precisa existir e precisa ser
    /// HttpOnly: é o segredo, e ninguém fora do servidor deve lê-lo.
    /// </summary>
    public const string CookieName = ".InfraControl.Antiforgery";

    /// <summary>Header em que o JS devolve o valor do cookie legível.</summary>
    public const string HeaderName = "X-XSRF-TOKEN";

    public static void Configure(AntiforgeryOptions options)
    {
        // HttpOnly = true aqui porque este é o segredo. A proteção não vem do
        // segredo do cookie, vem da SOP — um site cross-site não lê o cookie
        // legível, então não consegue montar o header.
        options.Cookie.Name = CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Path = "/";
        options.HeaderName = HeaderName;
    }

    /// <summary>
    /// Emite os cookies de antiforgery para o navegador: o segredo (HttpOnly) e
    /// o request-token no cookie legível que o JS devolve no header.
    ///
    /// Chamar SEMPRE no lugar que hoje chama só <c>GetAndStoreTokens</c>:
    /// publicar apenas o cookie-token faria toda escrita autenticada cair em
    /// 400, porque o JS mandaria o valor errado no header.
    /// </summary>
    public static void EmitForJavaScript(HttpContext context, IAntiforgery antiforgery)
    {
        // null: o IAntiforgery não tem token para este request (ou um double
        // devolveu o token set vazio) — não há o que publicar
        var tokens = antiforgery.GetAndStoreTokens(context);

        if (tokens?.RequestToken is not { } requestToken)
            return;

        context.Response.Cookies.Append(ReadableCookieName, requestToken, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
    }
}
