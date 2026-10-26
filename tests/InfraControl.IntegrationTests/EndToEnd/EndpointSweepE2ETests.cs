using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using InfraControl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.EndToEnd;

/// <summary>
/// F46-T002 — varredura ponta-a-ponta dos endpoints que o frontend chama:
/// cada teste exercita o MESMO contrato HTTP do front (envelope, cookie,
/// XSRF, papéis) e confere o EFEITO NO BANCO — query/command por um. A ideia
/// não é cobrir os 281 endpoints de uma vez, mas os fluxos de leitura e
/// escrita das telas principais, com verificação de persistência (perda de
/// dados front↔banco aparece aqui como falha de asserção).
/// </summary>
public sealed class EndpointSweepE2ETests(E2EHost host) : IClassFixture<E2EHost>
{
    // ── helpers (espelham FullPipelineE2ETests) ────────────────────────────

    private HttpClient NewClient() =>
        host.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private async Task<HttpClient> LoginAsync(string email)
    {
        // O admin vem do bootstrap real com senha própria; os demais do seed
        // compartilham UserPassword (mesma regra do FullPipelineE2ETests).
        var password = email == E2EHost.AdminEmail
            ? E2EHost.AdminPassword
            : E2EHost.UserPassword;
        var client = NewClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"login de {email}: {await response.Content.ReadAsStringAsync()}");
        return client;
    }

    private async Task<string> XsrfAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            .Should().BeTrue("o /auth/me emite o XSRF-TOKEN ao JS");
        var xsrf = cookies!.Single(c => c.StartsWith("XSRF-TOKEN="))
            .Split("XSRF-TOKEN=")[1].Split(";")[0];
        return xsrf;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("isSuccess").GetBoolean()
            .Should().BeTrue($"envelope de sucesso: {json.RootElement.GetRawText()}");
        return json.RootElement.GetProperty("data").Clone();
    }

    private async Task<HttpResponseMessage> WriteAsync(
        HttpClient client, HttpMethod method, string url, object payload, string xsrf)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-XSRF-TOKEN", xsrf);
        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> HeaderOnlyAsync(
        HttpClient client, HttpMethod method, string url, string xsrf)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-XSRF-TOKEN", xsrf);
        return await client.SendAsync(request);
    }

    private IServiceScope NewScope() => host.Factory.Services.CreateScope();

    // ── Incidents ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Incident_CreateListUpdateStatus_ShouldPersistToDatabase()
    {
        var client = await LoginAsync(E2EHost.OperatorEmail);
        var xsrf = await XsrfAsync(client);
        var title = $"E2E incidente {Guid.NewGuid():N}";

        var create = await WriteAsync(client, HttpMethod.Post, "/api/v1/incidents",
            new { title, severity = "critical", description = "varredura F46" }, xsrf);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await DataAsync(create);
        var id = created.GetProperty("id").GetGuid();

        // Banco: a escrita persistiu com os valores enviados.
        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Incidents.FindAsync(id);
            row.Should().NotBeNull("POST /incidents precisa persistir no banco");
            row!.Title.Should().Be(title);
            row.Severity.ToString().Should().Be("Critical");
            row.Status.ToString().Should().Be("Open");
        }

        // Listagem (mesmo contrato da tela): envelope paginado contém o item.
        var list = await client.GetAsync("/api/v1/incidents?page=1&pageSize=50");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await DataAsync(list);
        page.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        page.GetProperty("items").EnumerateArray()
            .Any(item => item.GetProperty("id").GetGuid() == id)
            .Should().BeTrue("o item criado aparece na lista");

        // Mudança de status → banco acompanha.
        var status = await WriteAsync(client, HttpMethod.Put,
            $"/api/v1/incidents/{id}/status",
            new { status = "investigating" }, xsrf);
        status.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Incidents.FindAsync(id);
            row!.Status.ToString().Should().Be("Investigating",
                "PUT /status precisa refletir no banco");
        }
    }

    [Fact]
    public async Task Incident_ViewerCannotCreate_ShouldReturn403()
    {
        var client = await LoginAsync(E2EHost.ViewerEmail);
        var xsrf = await XsrfAsync(client);

        var create = await WriteAsync(client, HttpMethod.Post, "/api/v1/incidents",
            new { title = "não deveria passar", severity = "low" }, xsrf);

        create.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "matriz Operate: Viewer não escreve");
    }

    // ── Inventory: locations ───────────────────────────────────────────────

    [Fact]
    public async Task Location_CreateListDelete_ShouldPersistToDatabase()
    {
        var client = await LoginAsync(E2EHost.OperatorEmail);
        var xsrf = await XsrfAsync(client);
        var name = $"E2E local {Guid.NewGuid():N}";

        var create = await WriteAsync(client, HttpMethod.Post,
            "/api/v1/inventory/locations",
            new { name, description = "varredura F46" }, xsrf);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await DataAsync(create);
        var id = created.GetProperty("id").GetGuid();

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Locations.FindAsync(id)).Should().NotBeNull(
                "POST /inventory/locations precisa persistir");
        }

        var list = await client.GetAsync("/api/v1/inventory/locations?page=1&pageSize=50");
        (await DataAsync(list)).GetProperty("items").EnumerateArray()
            .Any(item => item.GetProperty("id").GetGuid() == id)
            .Should().BeTrue();

        var delete = await HeaderOnlyAsync(client, HttpMethod.Delete,
            $"/api/v1/inventory/locations/{id}", xsrf);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Locations.FindAsync(id)).Should().BeNull(
                "DELETE precisa remover do banco (sem perda/órfão)");
        }
    }

    // ── Inventory: devices ─────────────────────────────────────────────────

    [Fact]
    public async Task Device_CreateReadDelete_ShouldPersistToDatabase()
    {
        var client = await LoginAsync(E2EHost.OperatorEmail);
        var xsrf = await XsrfAsync(client);

        // Local primeiro (device exige FK de location).
        var locationResponse = await WriteAsync(client, HttpMethod.Post,
            "/api/v1/inventory/locations",
            new { name = $"E2E local device {Guid.NewGuid():N}" }, xsrf);
        locationResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var locationId = (await DataAsync(locationResponse))
            .GetProperty("id").GetGuid();

        var name = $"E2E device {Guid.NewGuid():N}";
        var create = await WriteAsync(client, HttpMethod.Post,
            "/api/v1/inventory/devices",
            new { locationId, name, type = "Server", ip = "10.77.0.9" }, xsrf);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await DataAsync(create);
        var deviceId = created.GetProperty("id").GetGuid();

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Devices.FindAsync(deviceId);
            row.Should().NotBeNull("POST /inventory/devices precisa persistir");
            row!.Name.Should().Be(name);
            row.LocationId.Should().Be(locationId);
            row.Ip.Should().Be("10.77.0.9");
        }

        var read = await client.GetAsync($"/api/v1/inventory/devices/{deviceId}");
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DataAsync(read)).GetProperty("name").GetString().Should().Be(name);

        var delete = await HeaderOnlyAsync(client, HttpMethod.Delete,
            $"/api/v1/inventory/devices/{deviceId}", xsrf);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Devices.FindAsync(deviceId)).Should().BeNull();
        }
    }

    [Fact]
    public async Task Device_DuplicateName_ShouldReturn409_AndNotPersistTwice()
    {
        var client = await LoginAsync(E2EHost.OperatorEmail);
        var xsrf = await XsrfAsync(client);

        var locationResponse = await WriteAsync(client, HttpMethod.Post,
            "/api/v1/inventory/locations",
            new { name = $"E2E local dup {Guid.NewGuid():N}" }, xsrf);
        var locationId = (await DataAsync(locationResponse))
            .GetProperty("id").GetGuid();
        var name = $"E2E device dup {Guid.NewGuid():N}";

        var first = await WriteAsync(client, HttpMethod.Post,
            "/api/v1/inventory/devices",
            new { locationId, name, type = "Switch" }, xsrf);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await WriteAsync(client, HttpMethod.Post,
            "/api/v1/inventory/devices",
            new { locationId, name, type = "Switch" }, xsrf);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "unicidade de nome é regra de domínio — precisa chegar na API");

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Devices.CountAsync(d => d.Name == name))
                .Should().Be(1, "o conflito não pode criar linha duplicada");
        }
    }

    // ── Tasks (fluxo da tela: criar → mudar estado) ────────────────────────

    [Fact]
    public async Task Task_CreateChangeState_ShouldPersistToDatabase()
    {
        var client = await LoginAsync(E2EHost.OperatorEmail);
        var xsrf = await XsrfAsync(client);
        var title = $"E2E task {Guid.NewGuid():N}";

        var create = await WriteAsync(client, HttpMethod.Post, "/api/v1/tasks",
            new { title, kind = "Manual", priority = "high" }, xsrf);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await DataAsync(create)).GetProperty("id").GetGuid();

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Tasks.FindAsync(id);
            row.Should().NotBeNull("POST /tasks precisa persistir");
            row!.Title.Should().Be(title);
        }

        var state = await WriteAsync(client, HttpMethod.Put,
            $"/api/v1/tasks/{id}/state", new { state = "inprogress" }, xsrf);
        state.StatusCode.Should().Be(HttpStatusCode.OK,
            $"transição Open→InProgress é válida: {await state.Content.ReadAsStringAsync()}");
        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Tasks.FindAsync(id))!.State.ToString()
                .Should().Be("InProgress",
                    "PUT /tasks/{id}/state precisa refletir no banco");
        }
    }

    // ── Notification rules (F42: comando de admin → banco → leitura) ──────

    [Fact]
    public async Task NotificationRules_GetSave_ShouldPersistToDatabase()
    {
        var client = await LoginAsync(E2EHost.AdminEmail);
        var xsrf = await XsrfAsync(client);

        var get = await client.GetAsync("/api/v1/notifications/rules");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var rules = await DataAsync(get);
        rules.ValueKind.Should().Be(JsonValueKind.Array,
            "GET /notifications/rules devolve array do catálogo");
        rules.GetArrayLength().Should().BeGreaterThan(10);

        var save = await WriteAsync(client, HttpMethod.Put,
            "/api/v1/notifications/rules",
            new
            {
                rules = new[]
                {
                    new
                    {
                        eventType = "IncidentCreatedDomainEvent",
                        enabled = false,
                        channels = Array.Empty<string>()
                    }
                }
            }, xsrf);
        save.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.NotificationEventRules.SingleOrDefaultAsync(
                r => r.EventType == "IncidentCreatedDomainEvent");
            row.Should().NotBeNull("o PUT precisa fazer upsert no banco");
            row!.Enabled.Should().BeFalse();
        }

        var reload = await client.GetAsync("/api/v1/notifications/rules");
        var reloaded = await DataAsync(reload);
        reloaded.EnumerateArray()
            .Single(r => r.GetProperty("eventType").GetString()
                == "IncidentCreatedDomainEvent")
            .GetProperty("enabled").GetBoolean()
            .Should().BeFalse("a leitura volta o estado persistido");
    }

    // ── Knowledge (artigo → banco com author do usuário logado) ───────────

    [Fact]
    public async Task KnowledgeArticle_CreateList_ShouldPersistWithAuthor()
    {
        var client = await LoginAsync(E2EHost.AdminEmail);
        var xsrf = await XsrfAsync(client);
        var title = $"E2E artigo {Guid.NewGuid():N}";

        var create = await WriteAsync(client, HttpMethod.Post,
            "/api/v1/knowledge/articles",
            new { title, content = "Conteúdo da varredura F46-T002." }, xsrf);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await DataAsync(create);
        var id = created.GetProperty("id").GetGuid();

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.KnowledgeArticles.FindAsync(id);
            row.Should().NotBeNull("POST /knowledge/articles precisa persistir");
            row!.Title.Should().Be(title);
            row.AuthorId.Should().NotBeEmpty(
                "authorId vem da sessão do usuário logado (FK obrigatória)");
        }

        var list = await client.GetAsync("/api/v1/knowledge/articles?page=1&pageSize=50");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DataAsync(list)).GetProperty("items").EnumerateArray()
            .Any(item => item.GetProperty("id").GetGuid() == id)
            .Should().BeTrue();
    }

    // ── Audit (escrita → banco → leitura pela API) ─────────────────────────

    [Fact]
    public async Task Audit_List_ShouldReturnPersistedEntries()
    {
        var client = await LoginAsync(E2EHost.AdminEmail);

        var response = await client.GetAsync("/api/v1/admin/audit?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await DataAsync(response);

        page.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0,
            "o pipeline de auditoria grava no banco e a tela lê de volta");
        var first = page.GetProperty("items").EnumerateArray().First();
        first.GetProperty("action").GetString().Should().NotBeNullOrWhiteSpace();
        first.GetProperty("occurredAt").GetString().Should().NotBeNullOrWhiteSpace();
    }

    // ── SLA: services ──────────────────────────────────────────────────────

    [Fact]
    public async Task SlaService_CreateListDelete_ShouldPersistToDatabase()
    {
        var client = await LoginAsync(E2EHost.OperatorEmail);
        var xsrf = await XsrfAsync(client);
        var name = $"E2E serviço {Guid.NewGuid():N}";

        var create = await WriteAsync(client, HttpMethod.Post,
            "/api/v1/sla/services", new { name, description = "varredura F46" }, xsrf);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await DataAsync(create)).GetProperty("id").GetGuid();

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Services.FindAsync(id)).Should().NotBeNull(
                "POST /sla/services precisa persistir");
        }

        var list = await client.GetAsync("/api/v1/sla/services?page=1&pageSize=50");
        (await DataAsync(list)).GetProperty("items").EnumerateArray()
            .Any(item => item.GetProperty("id").GetGuid() == id)
            .Should().BeTrue();

        var delete = await HeaderOnlyAsync(client, HttpMethod.Delete,
            $"/api/v1/sla/services/{id}", xsrf);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Services.FindAsync(id)).Should().BeNull();
        }
    }

    // ── Monitoring: leitura de alerts (contrato da tela) ──────────────────

    [Fact]
    public async Task MonitoringAlerts_List_ShouldReturnEnvelope()
    {
        var client = await LoginAsync(E2EHost.OperatorEmail);

        var response = await client.GetAsync(
            "/api/v1/monitoring/alerts?page=1&pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await DataAsync(response);

        page.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array,
            "a tela de alertas consome o envelope paginado (mesmo sem dados)");
        page.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }
}
