using System.Reflection;
using FluentAssertions;
using InfraControl.ArchitectureTests.Common;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace InfraControl.ArchitectureTests.Rules;

/// <summary>
/// F1-T015/ADR-017: a proteção de CSRF só vale se estiver LIGADA. O
/// middleware é registrado em <c>Program.cs</c> por texto, então um merge
/// careto (ou um comentário apagando a linha) derrubaria a política inteira sem
/// erro de build nem teste unitário — foi o que o teste unitário mais recente
/// não pegaria, porque ele instancia o middleware na mão.
///
/// Por isso o teste lê o <c>Program.cs</c> e exige o registro, a ordem e a
/// configuração compartilhada.
/// </summary>
public sealed class AntiforgeryRegistrationTests : BaseArchitectureTest
{
    private static string ProgramSource()
    {
        var candidates = new[]
        {
            Path.Combine(FindRepositoryRoot(), "src", "InfraControl.Api", "Program.cs")
        };

        return candidates
            .Where(File.Exists)
            .Select(File.ReadAllText)
            .FirstOrDefault()
            ?? throw new FileNotFoundException(
                "Program.cs do InfraControl.Api não encontrado — o teste de registro não pode passar por ausência.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "InfraControl.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Raiz do repositório (InfraControl.slnx) não encontrada.");
    }

    [Fact]
    public void Antiforgery_Should_Be_Registered_With_The_Shared_Setup()
    {
        var source = ProgramSource();

        // AddAntiforgery(...) com a configuração compartilhada: se o Program
        // passar a configurar direto, o teste unitário passa a validar uma
        // configuração que não é a de produção
        source.Should().Contain("AddAntiforgery(AntiforgeryCookieSetup.Configure)");
        source.Should().NotContain("new AntiforgeryOptions()");
    }

    [Fact]
    public void Antiforgery_Middleware_Should_Be_In_The_Pipeline()
    {
        var source = ProgramSource();

        source.Should().Contain("UseMiddleware<AntiforgeryCookieMiddleware>()");
    }

    [Fact]
    public void Antiforgery_Middleware_Should_Run_After_Authentication_And_Before_Authorization()
    {
        var source = ProgramSource();

        var authentication = source.IndexOf("app.UseAuthentication()", StringComparison.Ordinal);
        var antiforgery = source.IndexOf("UseMiddleware<AntiforgeryCookieMiddleware>()", StringComparison.Ordinal);
        var authorization = source.IndexOf("app.UseAuthorization()", StringComparison.Ordinal);

        authentication.Should().BeGreaterThan(0, "UseAuthentication precisa existir");
        antiforgery.Should().BeGreaterThan(authentication,
            "sem UseAuthentication antes, o middleware não sabe se a request está autenticada e deixa passar toda escrita");
        authorization.Should().BeGreaterThan(antiforgery,
            "a validação tem de vir antes de resolver a autorização do endpoint");
    }

    [Fact]
    public void Antiforgery_Should_Be_Validated_Before_The_Exception_Handler_Takes_Over()
    {
        // o handler global traduz AntiforgeryValidationException em 400; se a
        // exceção subir sem ele, a resposta vira 500 e o usuário é expulso da
        // sessão por um erro de front
        ProgramSource().Should().Contain("app.UseExceptionHandler()");

        var handlerPath = Path.Combine(
            FindRepositoryRoot(), "src", "InfraControl.Api", "Middleware", "GlobalExceptionHandler.cs");

        File.Exists(handlerPath).Should().BeTrue();
        File.ReadAllText(handlerPath).Should().Contain("AntiforgeryValidationException");
    }

    [Theory]
    [InlineData("XSRF-TOKEN")]
    [InlineData("X-XSRF-TOKEN")]
    public void The_Antiforgery_Names_Should_Match_The_Frontend_Client(string name)
    {
        // o cliente HTTP do dashboard tem os mesmos nomes literais: se um lado
        // mudar sozinho, toda escrita autenticada começa a dar 400
        var clientPath = Path.Combine(
            FindRepositoryRoot(), "frontend", "InfraControl.Web", "src", "services", "api", "client.ts");

        File.Exists(clientPath).Should().BeTrue("o cliente HTTP do dashboard é quem envia o header");
        File.ReadAllText(clientPath).Should().Contain(name);
    }

    [Fact]
    public void Antiforgery_Should_Split_The_Secret_Cookie_From_The_Readable_One()
    {
        // O IAntiforgery usa DOIS valores: o cookie carrega o cookie-token (o
        // segredo, que só o servidor vê) e o header exige o request-token. O
        // JS só consegue ler um cookie, então o servidor precisa publicar o
        // request-token num segundo cookie legível. Se os dois tiverem o mesmo
        // nome, um sobrescreve o outro e toda escrita autenticada cai em 400
        // com "cookie token and request token were swapped".
        var setup = typeof(InfraControl.Api.Security.AntiforgeryCookieSetup);
        var configure = setup.GetMethod(nameof(InfraControl.Api.Security.AntiforgeryCookieSetup.Configure))
            ?? throw new MissingMethodException(setup.FullName, "Configure");

        var options = new AntiforgeryOptions();
        configure.Invoke(null, [options]);

        options.Cookie.Name.Should().Be(InfraControl.Api.Security.AntiforgeryCookieSetup.CookieName);
        options.Cookie.Name.Should().NotBe("XSRF-TOKEN", "o segredo não pode ocupar o nome do legível");
        options.Cookie.HttpOnly.Should().BeTrue("é o segredo do servidor");
        options.HeaderName.Should().Be("X-XSRF-TOKEN");
        options.Cookie.SecurePolicy.Should().Be(CookieSecurePolicy.Always);
    }
}
