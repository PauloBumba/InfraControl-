using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace InfraControl.IntegrationTests.EndToEnd;

/// <summary>
/// F46-T003 — caça a N+1/queries por request: cada teste reseta o
/// <see cref="DbCommandCounter"/>, dispara UMA requisição como o front faz e
/// limita quantos comandos SQL o EF pode executar. Um N+1 real (query por item
/// da lista) estoura o limite na hora. Os números também alimentam o relatório
/// do F46-T005 (queries por endpoint).
/// </summary>
public sealed class QueryCountRegressionE2ETests(
    QuietE2EHost fixture, ITestOutputHelper output) : IClassFixture<QuietE2EHost>
{
    private readonly E2EHost host = fixture.Host;

    // Limites GENEROSOS de propósito: pegam N+1 (dezenas/centenas de queries)
    // sem serem frágeis a comportamentos legítimos (audit, sessão, cache miss).
    // /tasks é rico por design (group joins de assignees/devices/incidents/
    // articles/pdi/stats/pais — F7-T002, O(1) por página) e tem budget próprio.
    private const int ReadBudget = 6;
    private const int TasksReadBudget = 12;
    private const int WriteBudget = 14;

    private HttpClient NewClient() =>
        host.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private async Task<HttpClient> LoginAsync(string email)
    {
        var password = email == E2EHost.AdminEmail
            ? E2EHost.AdminPassword
            : E2EHost.UserPassword;
        var client = NewClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"login de {email}: {await response.Content.ReadAsStringAsync()}");
        return client;
    }

    private async Task<string> XsrfAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/me");
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            .Should().BeTrue();
        return cookies!.Single(c => c.StartsWith("XSRF-TOKEN="))
            .Split("XSRF-TOKEN=")[1].Split(";")[0];
    }

    /// <summary>Mede os comandos SQL de UMA requisição e devolve o total.</summary>
    private async Task<long> CountForAsync(Func<Task<HttpResponseMessage>> request)
    {
        DbCommandCounter.Instance.Reset(host.SqlCounterKey);
        var response = await request();
        var total = DbCommandCounter.Instance.Total(host.SqlCounterKey);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        return total;
    }

    private async Task SeedBaselineAsync(HttpClient client, string xsrf)
    {
        // Listas vazias escondem N+1 (sem itens não há "por item"): semeia
        // volume representativo de cada módulo pela própria API.
        for (var i = 0; i < 12; i++)
        {
            using var incident = new HttpRequestMessage(HttpMethod.Post, "/api/v1/incidents")
            {
                Content = JsonContent.Create(new
                {
                    title = $"N+1 baseline incident {i}",
                    severity = "medium"
                })
            };
            incident.Headers.Add("X-XSRF-TOKEN", xsrf);
            (await client.SendAsync(incident)).StatusCode
                .Should().Be(HttpStatusCode.Created);

            using var task = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tasks")
            {
                Content = JsonContent.Create(new
                {
                    title = $"N+1 baseline task {i}",
                    kind = "Manual"
                })
            };
            task.Headers.Add("X-XSRF-TOKEN", xsrf);
            (await client.SendAsync(task)).StatusCode
                .Should().Be(HttpStatusCode.Created);
        }
    }

    [Fact]
    public async Task ReadEndpoints_ShouldStayWithinQueryBudget()
    {
        var client = await LoginAsync(E2EHost.AdminEmail);
        var xsrf = await XsrfAsync(client);
        await SeedBaselineAsync(client, xsrf);

        var reads = new (string Route, Func<Task<HttpResponseMessage>> Call)[]
        {
            ("/api/v1/incidents?page=1&pageSize=50",
                () => client.GetAsync("/api/v1/incidents?page=1&pageSize=50")),
            ("/api/v1/tasks?page=1&pageSize=50",
                () => client.GetAsync("/api/v1/tasks?page=1&pageSize=50")),
            ("/api/v1/inventory/devices?page=1&pageSize=50",
                () => client.GetAsync("/api/v1/inventory/devices?page=1&pageSize=50")),
            ("/api/v1/inventory/locations?page=1&pageSize=50",
                () => client.GetAsync("/api/v1/inventory/locations?page=1&pageSize=50")),
            ("/api/v1/monitoring/alerts?page=1&pageSize=20",
                () => client.GetAsync("/api/v1/monitoring/alerts?page=1&pageSize=20")),
            ("/api/v1/knowledge/articles?page=1&pageSize=20",
                () => client.GetAsync("/api/v1/knowledge/articles?page=1&pageSize=20")),
            ("/api/v1/admin/audit?page=1&pageSize=20",
                () => client.GetAsync("/api/v1/admin/audit?page=1&pageSize=20")),
            ("/api/v1/notifications/rules",
                () => client.GetAsync("/api/v1/notifications/rules")),
            ("/api/v1/sla/services?page=1&pageSize=50",
                () => client.GetAsync("/api/v1/sla/services?page=1&pageSize=50"))
        };

        foreach (var (route, call) in reads)
        {
            var queries = await CountForAsync(call);
            var budget = route.StartsWith("/api/v1/tasks", StringComparison.Ordinal)
                ? TasksReadBudget
                : ReadBudget;
            output.WriteLine($"[queries] {route}: {queries} comandos SQL " +
                             $"(orçamento {budget})");
            if (queries > budget)
            {
                output.WriteLine($"[sql] comandos de {route}:");
                foreach (var text in DbCommandCounter.Instance.Recent(host.SqlCounterKey))
                    output.WriteLine("  " + text.ReplaceLineEndings(" "));
            }
            queries.Should().BeLessThanOrEqualTo(budget,
                $"{route} executou {queries} comandos SQL — acima do orçamento " +
                $"{budget} (suspeita de N+1 ou join por item; veja [sql] no output)");
        }
    }

    [Fact]
    public async Task Diagnostics_InterceptorIsVisibleInDI()
    {
        using var scope = host.Factory.Services.CreateScope();
        var interceptors = scope.ServiceProvider
            .GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>()
            .ToList();
        output.WriteLine($"[diag] IInterceptor no DI: {interceptors.Count} — " +
            string.Join(", ", interceptors.Select(i => i.GetType().Name)));
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        output.WriteLine($"[diag] AppDbContext criado: {context.GetType().Name}");
        output.WriteLine($"[diag] chave do host: {host.SqlCounterKey}");
        await Task.CompletedTask;
        interceptors.Should().NotBeEmpty("o interceptor deve estar no DI");
    }

    [Fact]
    public async Task WriteCommand_ShouldStayWithinQueryBudget()
    {        var client = await LoginAsync(E2EHost.AdminEmail);
        var xsrf = await XsrfAsync(client);

        DbCommandCounter.Instance.Reset(host.SqlCounterKey);
        var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/incidents")
        {
            Content = JsonContent.Create(new
            {
                title = $"N+1 write {Guid.NewGuid():N}",
                severity = "high",
                description = "orçamento de escrita F46-T003"
            })
        };
        create.Headers.Add("X-XSRF-TOKEN", xsrf);
        var response = await client.SendAsync(create);
        var queries = DbCommandCounter.Instance.Total(host.SqlCounterKey);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            await response.Content.ReadAsStringAsync());
        output.WriteLine($"[queries] POST /api/v1/incidents: {queries} comandos " +
                         $"SQL (orçamento {WriteBudget})");
        queries.Should().BeLessThanOrEqualTo(WriteBudget,
            $"POST /incidents executou {queries} comandos SQL — acima do " +
            $"orçamento {WriteBudget} (persistência+audit+outbox devem ser poucos)");
    }
}

/// <summary>
/// E2EHost sem os workers de notificações/saúde: o poll deles a cada 2s
/// contamina o DbCommandCounter e falsa "N+1" no teste de orçamento.
/// </summary>
public sealed class QuietE2EHost : IAsyncLifetime
{
    public E2EHost Host { get; } = new();

    public Task InitializeAsync()
    {
        Host.DisableApiWorkers();
        return Host.InitializeAsync();
    }

    public Task DisposeAsync() => Host.DisposeAsync();
}
