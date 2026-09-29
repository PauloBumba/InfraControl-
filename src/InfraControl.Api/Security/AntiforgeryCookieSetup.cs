using Microsoft.AspNetCore.Antiforgery;

namespace InfraControl.Api.Security;

/// <summary>
/// F1-T015 / ADR-017: configuração do double-submit de CSRF em UM lugar só.
/// O Program.cs e os testes usam este método — assim o teste não valida uma
/// configuração diferente da que roda em produção.
/// </summary>
public static class AntiforgeryCookieSetup
{
    /// <summary>Cookie legível pelo JS (mesmo nome do Angular, por convenção).</summary>
    public const string CookieName = "XSRF-TOKEN";

    /// <summary>Header em que o JS devolve o valor do cookie.</summary>
    public const string HeaderName = "X-XSRF-TOKEN";

    public static void Configure(AntiforgeryOptions options)
    {
        // HttpOnly = false é o ponto do double-submit: o navegador precisa que
        // o JavaScript leia o valor para devolver no header. A proteção não vem
        // do segredo do cookie, vem da SOP — um site cross-site não lê o cookie,
        // então não consegue montar o header. Já(HttpOnly: true) não há o que
        // mandar e toda escrita autenticada cairia em 400.
        options.Cookie.Name = CookieName;
        options.Cookie.HttpOnly = false;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Path = "/";
        options.HeaderName = HeaderName;
    }
}
