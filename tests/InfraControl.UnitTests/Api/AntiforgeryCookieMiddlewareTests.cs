using System.Net;
using System.Security.Claims;
using FluentAssertions;
using InfraControl.Api.Middleware;
using InfraControl.Api.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.UnitTests.Api;

/// <summary>
/// F1-T015 / ADR-017: a política é "escrita autenticada sem token não passa".
/// O teste monta o <see cref="IAntiforgery"/> REAL, com a mesma configuração do
/// Program.cs (via AntiforgeryCookieSetup) — um double-submit de mentira
/// passaria verde e não provaria nada sobre o token.
///
/// O round trip é em DUAS requests, como no navegador: uma emite o cookie
/// (GET /auth/me), a outra o devolve (POST de escrita). Não dá para fazer na
/// mesma: a feature de cookies da request é materializada durante a emissão.
///
/// O que fica de fora, e é uma pendência real (TASKS.md): o repositório não tem
/// WebApplicationFactory, então não há prova e2e pelo pipeline HTTP completo.
/// </summary>
public sealed class AntiforgeryCookieMiddlewareTests
{
    private const string VictimId = "11111111-1111-1111-1111-111111111111";
    private const string AttackerId = "99999999-9999-9999-9999-999999999999";

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddAntiforgery(AntiforgeryCookieSetup.Configure);
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal User(string id) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Name, $"{id}@exemplo.test")],
        "Test", ClaimTypes.Name, ClaimTypes.Role));

    private static DefaultHttpContext Request(
        string method, ClaimsPrincipal? principal = null, (string Cookie, string Header)? token = null)
    {
        var context = new DefaultHttpContext { RequestServices = Services() };
        context.Request.Method = method;
        context.Request.Path = "/api/v1/inventory/devices";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("app.exemplo.test");
        context.User = principal ?? new ClaimsPrincipal(new ClaimsIdentity());

        if (token is not null)
        {
            context.Request.Headers.Cookie = $"{AntiforgeryCookieSetup.CookieName}={token.Value.Cookie}";
            context.Request.Headers[AntiforgeryCookieSetup.HeaderName] = token.Value.Header;
        }

        return context;
    }

    /// <summary>
    /// GET /auth/me: o servidor emite o cookie e devolve o token que o JS
    /// copiou para o header. Reproduz a emissão de verdade e verifica as
    /// propriedades do cookie — inclusive que ele NÃO é HttpOnly.
    /// </summary>
    private static (string Cookie, string Header) IssueToken(ClaimsPrincipal principal)
    {
        var issuing = Request("GET", principal);
        var tokens = issuing.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(issuing);

        var setCookie = issuing.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain(AntiforgeryCookieSetup.CookieName);
        setCookie.Should().Contain("samesite=strict");
        setCookie.Should().Contain("secure");
        setCookie.Should().NotContain("httponly", "sem HttpOnly=false o JS não tem o que mandar no header");

        // São DOIS tokens: o cookie carrega o cookie-token (o segredo que o
        // navegador devolve sozinho) e o header carrega o request-token (o
        // que o JS leu). Mandar o mesmo valor nos dois lados é justamente o
        // que o ASP.NET rejeita como "swapped".
        var cookieToken = setCookie
            .Split(';')[0]
            .Split('=', 2)[1];

        cookieToken.Should().NotBe(tokens.RequestToken);
        return (cookieToken, tokens.RequestToken!);
    }

    private static Task Act(HttpContext context)
        => new AntiforgeryCookieMiddleware(
                _ => Task.CompletedTask,
                context.RequestServices.GetRequiredService<IAntiforgery>())
            .InvokeAsync(context);

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Authenticated_Write_Without_Token_Should_Be_Rejected(string method)
    {
        var act = () => Act(Request(method, User(VictimId)));
        await act.Should().ThrowAsync<AntiforgeryValidationException>();
    }

    [Fact]
    public async Task Authenticated_Write_With_Valid_Token_Should_Pass()
    {
        var principal = User(VictimId);
        var reached = false;
        var context = Request("POST", principal, IssueToken(principal));

        await new AntiforgeryCookieMiddleware(
                _ => { reached = true; return Task.CompletedTask; },
                context.RequestServices.GetRequiredService<IAntiforgery>())
            .InvokeAsync(context);

        reached.Should().BeTrue("token válido não pode barrar a requisição");
    }

    [Fact]
    public async Task Token_Issued_To_Another_User_Should_Not_Open_The_Request()
    {
        var token = IssueToken(User(VictimId));

        var act = () => Act(Request("POST", User(AttackerId), token));

        (await act.Should().ThrowAsync<AntiforgeryValidationException>())
            .WithMessage("*");
    }

    [Fact]
    public async Task Header_Without_Cookie_Should_Be_Rejected()
    {
        // metade do double-submit: o header sozinho não vale nada
        var token = IssueToken(User(VictimId));
        var context = Request("POST", User(VictimId));
        context.Request.Headers[AntiforgeryCookieSetup.HeaderName] = token.Header;

        var act = () => Act(context);
        await act.Should().ThrowAsync<AntiforgeryValidationException>();
    }

    [Fact]
    public async Task Cookie_Without_Header_Should_Be_Rejected()
    {
        var token = IssueToken(User(VictimId));
        var context = Request("POST", User(VictimId));
        context.Request.Headers.Cookie = $"{AntiforgeryCookieSetup.CookieName}={token.Cookie}";

        var act = () => Act(context);
        await act.Should().ThrowAsync<AntiforgeryValidationException>();
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task Read_Should_Not_Require_Token(string method)
    {
        await Act(Request(method, User(VictimId)));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Anonymous_Write_Should_Pass_Without_Token(string method)
    {
        // login, registro, convite, reset de senha e /auth/google: não há
        // cookie de sessão para explorar, e no login o token ainda nem existe
        await Act(Request(method));
    }

    [Fact]
    public async Task Malformed_Token_Should_Be_Rejected()
    {
        var act = () => Act(Request("POST", User(VictimId), ("token-forjado", "token-forjado")));

        await act.Should().ThrowAsync<AntiforgeryValidationException>();
    }

    [Fact]
    public void Cookie_Configuration_Must_Stay_Readable_By_The_Script_And_Tls_Only()
    {
        // se HttpOnly voltar a true, o JS não tem o que mandar no header e
        // toda escrita autenticada passa a dar 400 — o sintoma aparece em
        // produção, não no build
        var options = new AntiforgeryOptions();
        AntiforgeryCookieSetup.Configure(options);

        options.Cookie.Name.Should().Be("XSRF-TOKEN");
        options.Cookie.HttpOnly.Should().BeFalse();
        options.Cookie.SecurePolicy.Should().Be(CookieSecurePolicy.Always);
        options.Cookie.SameSite.Should().Be(SameSiteMode.Strict);
        options.Cookie.Path.Should().Be("/");
        options.HeaderName.Should().Be("X-XSRF-TOKEN");
    }

    [Fact]
    public void Issued_Token_Should_Not_Carry_The_User_Id_In_Clear()
    {
        var token = IssueToken(User(VictimId));

        // o par "user|token" cru (formato do ASP.NET MVC clássico) não é o
        // que o cookie carrega: é o request token protegido
        WebUtility.UrlDecode(token.Header).Should().NotContain(VictimId);
        WebUtility.UrlDecode(token.Cookie).Should().NotContain(VictimId);
    }
}
