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
    /// GET /auth/me: o servidor emite os cookies e o JS devolve o legível no
    /// header. Reproduz a emissão de verdade (mesmo EmitForJavaScript do
    /// controller) e devolve o MESMO par que o navegador usaria.
    ///
    /// O header sai do cookie legível, e é isso que o client.ts faz. Não é
    /// detalhe: o IAntiforgery exige o request-token no header e o
    /// cookie-token no cookie, e o segundo request-token nunca sai do servidor
    /// por conta própria.
    /// </summary>
    private static (string Cookie, string Header) IssueToken(ClaimsPrincipal principal)
    {
        var issuing = Request("GET", principal);
        AntiforgeryCookieSetup.EmitForJavaScript(
            issuing, issuing.RequestServices.GetRequiredService<IAntiforgery>());

        var jar = CookieJar(issuing);

        jar.Should().ContainKey(AntiforgeryCookieSetup.CookieName, "o segredo do framework");
        jar.Should().ContainKey(AntiforgeryCookieSetup.ReadableCookieName, "o que o JS lê");

        // o header é o valor do cookie legível, não o segredo
        return (jar[AntiforgeryCookieSetup.CookieName], jar[AntiforgeryCookieSetup.ReadableCookieName]);
    }

    /// <summary>Cookie jar do navegador: todos os Set-Cookie viram um cookie só.</summary>
    private static Dictionary<string, string> CookieJar(HttpContext context) =>
        context.Response.Headers.SetCookie
            .Select(value => value!.Split(';')[0])
            .Select(value => value.Split('=', 2))
            .ToDictionary(part => part[0], part => part[1]);

    [Fact]
    public async Task Browser_Round_Trip_Should_Pass_Using_Only_What_The_Script_Can_See()
    {
        // O teste que faltava na F1-T015 e que reprova a versão anterior:
        // o servidor publicava SÓ o cookie-token, o JS o copiava para o
        // header, e o ASP.NET rejeitava como "swapped" — ou seja, TODA escrita
        // autenticada da aplicação devolvia 400 e os testes passavam porque
        // aqui o request-token era usado de mão.
        var principal = User(VictimId);

        var issuing = Request("GET", principal);
        AntiforgeryCookieSetup.EmitForJavaScript(
            issuing, issuing.RequestServices.GetRequiredService<IAntiforgery>());

        // o navegador manda TODOS os cookies de volta, e o JS copia só o legível
        var jar = CookieJar(issuing);
        var writing = Request("POST", principal);
        writing.Request.Headers.Cookie = string.Join("; ", jar.Select(pair => $"{pair.Key}={pair.Value}"));
        writing.Request.Headers[AntiforgeryCookieSetup.HeaderName] = jar[AntiforgeryCookieSetup.ReadableCookieName];

        var reached = false;
        await new AntiforgeryCookieMiddleware(
                _ => { reached = true; return Task.CompletedTask; },
                writing.RequestServices.GetRequiredService<IAntiforgery>())
            .InvokeAsync(writing);

        reached.Should().BeTrue("o fluxo real do navegador tem que passar");
    }

    [Fact]
    public void Readable_Cookie_Should_Carry_The_Request_Token_And_The_Secret_Cookie_Stay_HttpOnly()
    {
        var options = new AntiforgeryOptions();
        AntiforgeryCookieSetup.Configure(options);

        // o segredo é HttpOnly e tem nome próprio...
        options.Cookie.Name.Should().Be(AntiforgeryCookieSetup.CookieName);
        options.Cookie.Name.Should().NotBe(AntiforgeryCookieSetup.ReadableCookieName, "se colidissem, um sobrescreveria o outro");
        options.Cookie.HttpOnly.Should().BeTrue();
        options.Cookie.SecurePolicy.Should().Be(CookieSecurePolicy.Always);
        options.Cookie.SameSite.Should().Be(SameSiteMode.Strict);
        options.Cookie.Path.Should().Be("/");
        options.HeaderName.Should().Be("X-XSRF-TOKEN");

        // ...e o legível é o que o JS copia para o header
        var issuing = Request("GET", User(VictimId));
        AntiforgeryCookieSetup.EmitForJavaScript(
            issuing, issuing.RequestServices.GetRequiredService<IAntiforgery>());

        // olha SÓ o cookie legível: o segredo, ao lado, é HttpOnly de propósito
        var readable = issuing.Response.Headers.SetCookie
            .Select(value => value!.Split(';')[0])
            .Single(line => line.StartsWith(AntiforgeryCookieSetup.ReadableCookieName + "="));

        readable.Should().NotContain("httponly", "o JS precisa ler o cookie legível");
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
    public void Issued_Token_Should_Not_Carry_The_User_Id_In_Clear()
    {
        var token = IssueToken(User(VictimId));

        // o par "user|token" cru (formato do ASP.NET MVC clássico) não é o
        // que o cookie carrega: é o request token protegido
        WebUtility.UrlDecode(token.Header).Should().NotContain(VictimId);
        WebUtility.UrlDecode(token.Cookie).Should().NotContain(VictimId);
    }
}
