using FluentAssertions;
using InfraControl.ArchitectureTests.Common;

namespace InfraControl.ArchitectureTests.Rules;

/// <summary>
/// Boot dos hosts (API e Workers).
///
/// O repo não tem WebApplicationFactory (pendência declarada nas ADR-017/018),
/// então nada na suíte executa o pipeline HTTP de verdade: um Program.cs que
/// não sobe passa verde em todos os testes e só aparece quando o container sobe.
/// Estes dois casos são exatamente isso — derrubavam os hosts no boot e foram
/// pegos só no smoke com Docker.
/// </summary>
public sealed class HostBootTests : BaseArchitectureTest
{
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "InfraControl.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Raiz do repositório (InfraControl.slnx) não encontrada.");
    }

    private static string Read(params string[] parts)
    {
        var path = Path.Combine(new[] { FindRepositoryRoot() }.Concat(parts).ToArray());

        return File.Exists(path)
            ? File.ReadAllText(path)
            : throw new FileNotFoundException($"Arquivo não encontrado: {path}");
    }

    [Fact]
    public void Api_Should_Register_ProblemDetails_For_The_Empty_UseExceptionHandler()
    {
        // No .NET 10 o construtor do ExceptionHandlerMiddleware lança quando
        // UseExceptionHandler() vem sem delegate e não há IProblemDetailsService
        // — o IExceptionHandler registrado (GlobalExceptionHandler) não basta;
        // ele só é consultado DEPOIS, na hora de tratar. Sem AddProblemDetails a
        // API morre no boot ("Application startup exception") e /health nunca
        // responde.
        var source = Read("src", "InfraControl.Api", "Program.cs");

        source.Should().Contain("builder.Services.AddProblemDetails()");
        source.Should().Contain("app.UseExceptionHandler()");
        source.Should().Contain("AddExceptionHandler<GlobalExceptionHandler>()");
    }

    [Fact]
    public void Example_Admin_Password_Should_Satisfy_The_Identity_Policy()
    {
        // A política (AddIdentityCore: RequiredLength 12 + digit/upper/lower/
        // non-alphanumeric) é do código; a senha de exemplo é do .env.example.
        // Fora da política o AdminBootstrapper lança PasswordRequiresUpper na
        // linha 33 e a API morre em restart loop antes de servir qualquer
        // rota — o deployment novo simplesmente não sobe.
        var example = Read(".env.example");
        var password = example
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("BootstrapAdmin__Password=", StringComparison.Ordinal))
            .Select(line => line["BootstrapAdmin__Password=".Length..].Trim())
            .SingleOrDefault();

        password.Should().NotBeNullOrEmpty(".env.example precisa documentar BootstrapAdmin__Password");
        password.Should().MatchRegex(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{12,}$",
            "tem de cumprir a política de senha do Identity (src/InfraControl.Infrastructure/Services/DependencyInjection.cs)");
    }

    [Fact]
    public void MassTransit_Should_Be_Configured_Exactly_Once_Per_Repo()
    {
        // AddMassTransit() só pode ser chamado uma vez por container de
        // DI. AddInfrastructure já configura o bus; uma segunda chamada no host
        // (como a que os Workers faziam para registrar consumers) derruba o boot
        // com MassTransit.ConfigurationException.
        var srcRoot = Path.Combine(FindRepositoryRoot(), "src");

        var calls = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(file => (file, source: File.ReadAllText(file)))
            .SelectMany(entry => entry.source
                .Split('\n')
                .Select((line, index) => (entry.file, line, index))
                .Where(entry => entry.line.Contains(".AddMassTransit(", StringComparison.Ordinal)))
            .ToList();

        calls.Should().HaveCount(1,
            "o bus deve ser configurado uma única vez (o registro de consumers/consumers observers "
            + "entra pela configuração do AddInfrastructure); chamadas extras: "
            + string.Join(", ", calls.Select(call => $"{Path.GetFileName(call.file)}:{call.index + 1}")));
    }
}
