using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit.Abstractions;

namespace InfraControl.IntegrationTests.EndToEnd;

/// <summary>
/// F46-T003 — coleta de latência ponta-a-ponta (o mesmo caminho que o
/// navegador percorre: rede virtual do TestServer → pipeline completo →
/// banco/Redis reais). Para cada endpoint representativo roda N iterações,
/// calcula p50/p95 e grava <c>artifacts/latency-e2e.json</c> — a base numérica
/// do relatório do F46-T005. Os tetos de p95 são generosos (host de teste
/// compartilhado): servem como detector de regressão grosseira, não como SLA.
/// </summary>
public sealed class LatencySweepE2ETests(
    E2EHost host, ITestOutputHelper output) : IClassFixture<E2EHost>
{
    private const int Iterations = 20;
    private const double P95CeilingMs = 1500;

    private sealed record Sample(
        double P50Ms, double P95Ms, double MaxMs, long Queries);

    private sealed record EndpointReport(
        string Method, string Route, int Ok, int Errors,
        double P50Ms, double P95Ms, double MaxMs);

    private HttpClient NewClient() =>
        host.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private async Task<(HttpClient Client, string Xsrf)> LoginAsync(string email)
    {
        var password = email == E2EHost.AdminEmail
            ? E2EHost.AdminPassword
            : E2EHost.UserPassword;
        var client = NewClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());

        var me = await client.GetAsync("/api/v1/auth/me");
        me.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var xsrf = cookies!.Single(c => c.StartsWith("XSRF-TOKEN="))
            .Split("XSRF-TOKEN=")[1].Split(";")[0];
        return (client, xsrf);
    }

    private static Sample Measure(
        IEnumerable<double> milliseconds, long totalQueries, int ok, int errors)
    {
        var sorted = milliseconds.OrderBy(v => v).ToArray();
        double Percentile(double p)
            => sorted[Math.Min(sorted.Length - 1,
                (int)Math.Ceiling(p * sorted.Length) - 1)];
        _ = (totalQueries, ok, errors);
        return new Sample(
            Math.Round(Percentile(0.50), 1),
            Math.Round(Percentile(0.95), 1),
            Math.Round(sorted[^1], 1),
            totalQueries);
    }

    [Fact]
    public async Task Latency_Sweep_ShouldCollectPercentiles_WithinCeiling()
    {
        var (client, xsrf) = await LoginAsync(E2EHost.AdminEmail);

        var getRoutes = new[]
        {
            "/api/v1/auth/me",
            "/api/v1/incidents?page=1&pageSize=20",
            "/api/v1/tasks?page=1&pageSize=20",
            "/api/v1/inventory/devices?page=1&pageSize=20",
            "/api/v1/inventory/locations?page=1&pageSize=20",
            "/api/v1/monitoring/alerts?page=1&pageSize=20",
            "/api/v1/knowledge/articles?page=1&pageSize=20",
            "/api/v1/admin/audit?page=1&pageSize=20",
            "/api/v1/notifications/rules",
            "/api/v1/sla/services?page=1&pageSize=20",
            "/api/v1/admin/users?page=1&pageSize=20"
        };

        var reports = new List<EndpointReport>();

        foreach (var route in getRoutes)
        {
            var (p50, p95, max, ok, errors) =
                await SweepAsync($"GET {route}", async () =>
                {
                    var response = await client.GetAsync(route);
                    return (response.StatusCode,
                        (int)response.StatusCode < 400);
                },
                keepResponses: true);
            reports.Add(new EndpointReport("GET", route, ok, errors, p50, p95, max));
        }

        // Commands representativos (escrita do formulário da tela).
        var commands = new (string Method, string Route, object Payload)[]
        {
            ("POST", "/api/v1/incidents", new
            {
                title = $"latency sweep {Guid.NewGuid():N}",
                severity = "low"
            }),
            ("POST", "/api/v1/tasks", new
            {
                title = $"latency sweep {Guid.NewGuid():N}",
                kind = "Manual"
            }),
            ("GET", "/api/v1/incidents?page=1&pageSize=50", new { })
        };

        foreach (var (method, route, payload) in commands)
        {
            if (method == "GET")
            {
                var (p50, p95, max, ok, errors) = await SweepAsync(
                    $"GET {route}",
                    async () =>
                    {
                        var response = await client.GetAsync(route);
                        return (response.StatusCode,
                            (int)response.StatusCode < 400);
                    },
                    keepResponses: true);
                reports.Add(new EndpointReport("GET", route, ok, errors, p50, p95, max));
                continue;
            }

            var result = await SweepAsync($"POST {route}", async () =>
            {
                using var request = new HttpRequestMessage(
                    new HttpMethod(method), route)
                {
                    Content = JsonContent.Create(payload)
                };
                request.Headers.Add("X-XSRF-TOKEN", xsrf);
                var response = await client.SendAsync(request);
                return (response.StatusCode,
                    (int)response.StatusCode is >= 200 and < 300);
            },
            keepResponses: true);
            reports.Add(new EndpointReport(method, route,
                result.Ok, result.Errors, result.P50Ms, result.P95Ms, result.MaxMs));
        }

        var root = FindRepoRoot();
        var artifacts = Path.Combine(root, "artifacts");
        Directory.CreateDirectory(artifacts);
        var file = Path.Combine(artifacts, "latency-e2e.json");
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(
            new
            {
                collectedAt = DateTimeOffset.Now.ToString("O"),
                iterations = Iterations,
                environment = "E2E host (TestServer + Testcontainers)",
                endpoints = reports
            },
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            }));

        foreach (var report in reports)
        {
            report.Errors.Should().Be(0,
                $"{report.Method} {report.Route} teve falhas no sweep " +
                $"({report.Errors}/{Iterations})");
            report.P95Ms.Should().BeLessThan(P95CeilingMs,
                $"p95 de {report.Method} {report.Route} = {report.P95Ms}ms " +
                $"acima do teto {P95CeilingMs}ms");
        }

        var summary = string.Join(Environment.NewLine, reports
            .OrderByDescending(r => r.P95Ms)
            .Select(r => $"{r.Method,-4} {r.Route,-55} p50={r.P50Ms,7}ms " +
                         $"p95={r.P95Ms,7}ms max={r.MaxMs,7}ms"));
        // Tabela visível na saída do teste — insumo direto do relatório F46-T005.
        output.WriteLine(
            $"F46-T003 coleta gravada em artifacts/latency-e2e.json{Environment.NewLine}" +
            $"Ordenado por p95:{Environment.NewLine}{summary}");
    }

    /// <summary>
    /// Roda <see cref="Iterations"/> chamadas, mede cada uma e calcula
    /// percentis. <c>keepResponses</c> só documenta a intenção (o sucesso é
    /// checado dentro do delegate).
    /// </summary>
    private async Task<(double P50Ms, double P95Ms, double MaxMs,
        int Ok, int Errors)> SweepAsync(
        string name,
        Func<Task<(HttpStatusCode Status, bool Success)>> call,
        bool keepResponses = false)
    {
        _ = keepResponses;
        var samples = new List<double>(Iterations);
        var ok = 0;
        var errors = 0;
        var failures = new List<string>();

        // Descarta a primeira chamada (cold start de pooled connection/handler).
        await call();

        for (var i = 0; i < Iterations; i++)
        {
            var timestamp = Stopwatch.GetTimestamp();
            var (status, success) = await call();
            samples.Add(Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds);
            if (success) ok++;
            else
            {
                errors++;
                failures.Add(((int)status).ToString());
            }
        }

        if (errors > 0)
            output.WriteLine($"[sweep:{name}] {errors}/{Iterations} falhas " +
                             $"(status: {string.Join(",", failures.Distinct())})");

        var sample = Measure(samples, 0, ok, errors);
        return (sample.P50Ms, sample.P95Ms, sample.MaxMs, ok, errors);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null &&
               !File.Exists(Path.Combine(dir.FullName, "InfraControl.slnx")))
        {
            dir = dir.Parent!;
        }
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
