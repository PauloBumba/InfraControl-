using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Auth;
using InfraControl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using DomainUser = InfraControl.Domain.Entities.User;

namespace InfraControl.IntegrationTests.EndToEnd;

/// <summary>
/// Fase Q — e2e HTTP pelo pipeline completo (pendência declarada em ADR-017/018).
/// Sobe o host REAL de <c>Program</c> via <see cref="WebApplicationFactory{TEntryPoint}"/>:
/// routing, policies de autorização, antiforgery, rate limiting, sessão e
/// GlobalExceptionHandler intactos — contra PostgreSQL (Testcontainers) e Redis
/// (rate limit distribuído e cache híbrido de sessão usam Redis em produção;
/// a aplicação até sobe sem ele, mas os limitadores fariam fallback — aqui o
/// cenário é o de deploy real).
///
/// Usuários semeados (senha compartilhada <see cref="UserPassword"/>):
/// admin vem do bootstrap real de <c>Program</c>; operador/viewer/sem-papel
/// são criados pelo mesmo caminho de Identity do provisionador.
/// </summary>
public sealed class E2EHost : IAsyncLifetime
{
    public const string AdminEmail = "admin@e2e.test";
    public const string AdminDisplayName = "Admin E2E";
    public const string AdminPassword = "E2e@Admin123!";
    public const string OperatorEmail = "operator@e2e.test";
    public const string ViewerEmail = "viewer@e2e.test";
    public const string NoRoleEmail = "norole@e2e.test";
    public const string UserPassword = "E2e@User123!";
    public const string ZabbixToken = "e2e-zabbix-token";

    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    private readonly IContainer _redis =
        new ContainerBuilder("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("Ready to accept connections"))
            .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        // Schema ANTES do boot: o startup de Program roda o bootstrap do admin
        // (BootstrapAdmin:Enabled=true por override de configuração do teste).
        await using (var schema = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options))
        {
            await schema.Database.EnsureCreatedAsync();
        }

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis",
                $"localhost:{_redis.GetMappedPublicPort(6379)}");

            // Bootstrap real do primeiro admin (caminho de produção).
            builder.UseSetting("BootstrapAdmin:Enabled", "true");
            builder.UseSetting("BootstrapAdmin:Email", AdminEmail);
            builder.UseSetting("BootstrapAdmin:DisplayName", AdminDisplayName);
            builder.UseSetting("BootstrapAdmin:Password", AdminPassword);

            // Os testes compartilham o mesmo "IP" do TestServer; sem folga o
            // limite real (5 login/min) derrubaria a própria suíte.
            builder.UseSetting("RateLimit:Rules:Login:PermitLimit", "1000");
            builder.UseSetting("RateLimit:Rules:PasswordReset:PermitLimit", "1000");

            // Webhook de Zabbix habilitado (token divergente/ausente = 401).
            builder.UseSetting("Integrations:Zabbix:Token", ZabbixToken);
        });

        // Boot do host (bootstrap do admin incluído) antes do seed dos demais.
        Factory.CreateClient().Dispose();

        await SeedUserAsync(OperatorEmail, "Operador E2E", UserRole.Operator);
        await SeedUserAsync(ViewerEmail, "Visualizador E2E", UserRole.Viewer);
        await SeedUserAsync(NoRoleEmail, "Sem Papel E2E", role: null);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();

        await _redis.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task SeedUserAsync(string email, string displayName, UserRole? role)
    {
        using var scope = Factory.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var userManager = provider.GetRequiredService<UserManager<InfraControlUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var db = provider.GetRequiredService<AppDbContext>();

        if (role is not null && !await roleManager.RoleExistsAsync(role.Value.ToString()))
        {
            var roleResult = await roleManager.CreateAsync(
                new IdentityRole<Guid>(role.Value.ToString()));
            roleResult.Succeeded.Should().BeTrue(
                $"criação do papel {role}: {Describe(roleResult.Errors)}");
        }

        var user = new InfraControlUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };

        var createResult = await userManager.CreateAsync(user, UserPassword);
        createResult.Succeeded.Should().BeTrue(
            $"criação de {email}: {Describe(createResult.Errors)}");

        if (role is not null)
        {
            var roleAdd = await userManager.AddToRoleAsync(user, role.Value.ToString());
            roleAdd.Succeeded.Should().BeTrue(
                $"atribuição do papel {role} a {email}: {Describe(roleAdd.Errors)}");
        }

        // Linha de domínio espelhando o provisionador real (FKs de sessão/notificação).
        db.DomainUsers.Add(DomainUser.Create(user.Id, email, displayName,
            role ?? UserRole.Viewer));
        await db.SaveChangesAsync();

        if (role is null)
        {
            // Usuário SEM role no domínio (a fábrica do agregado exige uma role
            // inicial): a atribuição é removida depois para exercitar a segunda
            // camada de autorização do MediatR com claims de role vazias.
            var assignment = db.UserRoleAssignments
                .Single(current => current.UserId == user.Id);
            db.UserRoleAssignments.Remove(assignment);
            await db.SaveChangesAsync();
        }
    }

    private static string Describe(IEnumerable<IdentityError> errors)
        => string.Join("; ", errors.Select(error => error.Description));
}
