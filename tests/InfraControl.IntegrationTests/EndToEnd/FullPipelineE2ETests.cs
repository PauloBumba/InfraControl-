using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InfraControl.IntegrationTests.EndToEnd;

/// <summary>
/// Fase Q — e2e HTTP pelo pipeline completo (pendência de ADR-017/018):
/// host real de <c>Program</c> + banco/Redis reais, validando o contrato EXATO
/// do qual o frontend depende — envelope {isSuccess,data,errors}, cookie de
/// sessão, XSRF-TOKEN no /auth/me, mensagens pt-BR e a matriz de papéis nas
/// duas camadas de autorização (policy da API + AuthorizationBehavior do MediatR).
/// </summary>
public sealed class FullPipelineE2ETests(E2EHost host) : IClassFixture<E2EHost>
{
    // ── helpers ─────────────────────────────────────────────────────────────

    private HttpClient NewClient() =>
        host.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            // CookieSecurePolicy.Always: sobre http o CookieContainer não
            // devolve o cookie de sessão — https virtual resolve como no
            // navegador real (o TestServer não liga para o scheme).
            BaseAddress = new Uri("https://localhost")
        });

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password });
        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"login de {email} deve passar: {await response.Content.ReadAsStringAsync()}");
        return client;
    }

    private Task<HttpClient> LoginAsync(string email)
        => LoginAsync(email, E2EHost.UserPassword);

    /// <summary>Pega o XSRF-TOKEN como o front pega: lido do Set-Cookie do /auth/me.</summary>
    private async Task<string> XsrfAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            .Should().BeTrue("o /auth/me emite o token de antiforgery ao JS");

        var entry = cookies!.FirstOrDefault(c =>
            c.StartsWith("XSRF-TOKEN=", StringComparison.OrdinalIgnoreCase));
        entry.Should().NotBeNull("o cookie XSRF-TOKEN precisa existir antes da primeira escrita");

        var value = entry!["XSRF-TOKEN=".Length..];
        var semicolon = value.IndexOf(';');
        return semicolon >= 0 ? value[..semicolon] : value;
    }

    private async Task<(bool IsSuccess, JsonElement Data, string[] Errors)>
        EnvelopeAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        return (
            root.GetProperty("isSuccess").GetBoolean(),
            root.GetProperty("data").Clone(),
            root.GetProperty("errors").EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty).ToArray());
    }

    private static HttpRequestMessage Write(
        HttpMethod method, string url, object payload, string? xsrf = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(payload) };
        if (xsrf is not null)
            request.Headers.Add("X-XSRF-TOKEN", xsrf);
        return request;
    }

    /// <summary>
    /// Replica o <c>client.ts</c> numa escrita SEM body (api.delete /
    /// api.post sem payload): só o header de antiforgery, sem Content-Type.
    /// É o formato literal em que o navegador envia essas chamadas.
    /// </summary>
    private static HttpRequestMessage HeaderOnly(HttpMethod method, string url, string xsrf)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-XSRF-TOKEN", xsrf);
        return request;
    }

    // ── contrato de sessão (o que o front consome no boot) ──────────────────

    [Fact]
    public async Task Login_e_me_devolvem_o_contrato_que_o_frontend_consome()
    {
        var client = NewClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = E2EHost.AdminEmail, password = E2EHost.AdminPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var (loginOk, _, loginErrors) = await EnvelopeAsync(login);
        loginOk.Should().BeTrue();
        loginErrors.Should().BeEmpty();

        var me = await client.GetAsync("/api/v1/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var (meOk, data, _) = await EnvelopeAsync(me);
        meOk.Should().BeTrue();

        data.GetProperty("email").GetString().Should().Be(E2EHost.AdminEmail);
        data.GetProperty("userId").GetString().Should().NotBeNullOrWhiteSpace();
        data.GetProperty("displayName").GetString().Should().NotBeNullOrWhiteSpace();
        data.GetProperty("roles").EnumerateArray()
            .Select(role => role.GetString()).Should().Contain("Admin");

        // O boot do SessionGate depende do XSRF nascer no /auth/me.
        (await XsrfAsync(client)).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_senha_errada_devolve_401_com_mensagem_ptbr_no_envelope()
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = E2EHost.AdminEmail, password = "senha-errada" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var (ok, _, errors) = await EnvelopeAsync(response);
        ok.Should().BeFalse();
        errors.Should().ContainSingle().Which.Should().Be("Credenciais inválidas.");
    }

    [Fact]
    public async Task Body_de_login_invalido_devolve_400_de_modelstate_no_envelope()
    {
        var client = NewClient();
        // Tipo errado quebra o binding (F0-T023: ModelState também embrulhado).
        var response = await client.PostAsync("/api/v1/auth/login",
            new StringContent("""{"email":123,"password":true}""",
                System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (ok, _, errors) = await EnvelopeAsync(response);
        ok.Should().BeFalse();
        errors.Should().NotBeEmpty();
    }

    // ── matriz de papéis (camada 1: policy da API) ──────────────────────────

    [Fact]
    public async Task Matriz_Operate_admin_e_operador_passam_viewer_nao_e_anonimo_vai_para_401()
    {
        var admin = await LoginAsync(E2EHost.AdminEmail, E2EHost.AdminPassword);
        var operador = await LoginAsync(E2EHost.OperatorEmail);
        var viewer = await LoginAsync(E2EHost.ViewerEmail);
        var anonimo = NewClient();

        (await admin.GetAsync("/api/v1/admin/users"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await operador.GetAsync("/api/v1/admin/users"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.GetAsync("/api/v1/admin/users"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonimo.GetAsync("/api/v1/admin/users"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── matriz de papéis (camada 2: AuthorizationBehavior do MediatR) ───────

    [Fact]
    public async Task Segunda_camada_bloqueia_usuario_sem_papel_com_403_no_envelope()
    {
        // [Authorize] puro deixa passar; quem reprova é o RequiredRoles do
        // handler — e essa reprovação precisa sair no envelope, não vazia.
        var semPapel = await LoginAsync(E2EHost.NoRoleEmail);
        var response = await semPapel.GetAsync("/api/v1/tasks");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var (ok, _, errors) = await EnvelopeAsync(response);
        ok.Should().BeFalse();
        errors.Should().NotBeEmpty();
    }

    // ── F18-T001: feed de notificações ──────────────────────────────────────

    [Fact]
    public async Task Feed_de_notificacoes_anonimo_vai_para_401()
    {
        var anonimo = NewClient();

        (await anonimo.GetAsync("/api/v1/notifications"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Feed_de_notificacoes_logado_devolve_o_envelope_paginado()
    {
        var admin = await LoginAsync(E2EHost.AdminEmail, E2EHost.AdminPassword);

        var response = await admin.GetAsync("/api/v1/notifications?page=1&pageSize=20");
        response.StatusCode.Should().Be(
            HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var (ok, data, errors) = await EnvelopeAsync(response);
        ok.Should().BeTrue();
        errors.Should().BeEmpty();

        data.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        data.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        data.GetProperty("page").GetInt32().Should().Be(1);
        data.GetProperty("pageSize").GetInt32().Should().Be(20);
    }

    // ── F18-T008: clientes ao vivo ──────────────────────────────────────────

    [Fact]
    public async Task Clients_anonimo_vai_para_401()
    {
        var anonimo = NewClient();

        (await anonimo.GetAsync("/api/v1/clients"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Clients_logado_devolve_o_contrato_paginado_agregado()
    {
        var admin = await LoginAsync(E2EHost.AdminEmail, E2EHost.AdminPassword);

        var response = await admin.GetAsync("/api/v1/clients?page=1&pageSize=20");
        response.StatusCode.Should().Be(
            HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var (ok, data, errors) = await EnvelopeAsync(response);
        ok.Should().BeTrue();
        errors.Should().BeEmpty();

        data.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        data.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        data.GetProperty("page").GetInt32().Should().Be(1);
        foreach (var item in data.GetProperty("items").EnumerateArray())
        {
            // Contrato do F18-T008: ao menos um identificador e o visto-vez.
            (item.GetProperty("ip").ValueKind != JsonValueKind.Null ||
             item.GetProperty("mac").ValueKind != JsonValueKind.Null)
                .Should().BeTrue("um cliente precisa de ip ou mac");
            item.GetProperty("lastSeenAt").GetDateTime().Should().NotBe(default);
        }
    }

    // ── escrita: Administer + antiforgery (ADR-017) ─────────────────────────

    [Fact]
    public async Task Escrita_Administer_exige_papel_admin_e_token_antiforgery()
    {
        var admin = await LoginAsync(E2EHost.AdminEmail, E2EHost.AdminPassword);
        var operador = await LoginAsync(E2EHost.OperatorEmail);
        var viewer = await LoginAsync(E2EHost.ViewerEmail);

        var me = await operador.GetAsync("/api/v1/auth/me");
        var (meOk, data, _) = await EnvelopeAsync(me);
        meOk.Should().BeTrue();
        var operadorId = data.GetProperty("userId").GetString();
        var url = $"/api/v1/admin/users/{operadorId}/status";
        var payload = new { active = true };

        // Sem o papel: reprova na policy mesmo com token válido.
        var viewerXsrf = await XsrfAsync(viewer);
        var viewerResponse = await viewer.SendAsync(
            Write(HttpMethod.Patch, url, payload, viewerXsrf));
        viewerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var operadorXsrf = await XsrfAsync(operador);
        var operadorResponse = await operador.SendAsync(
            Write(HttpMethod.Patch, url, payload, operadorXsrf));
        operadorResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Admin SEM o token: 400 de antiforgery no envelope (nunca 403/500).
        var semToken = await admin.SendAsync(Write(HttpMethod.Patch, url, payload));
        semToken.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (semTokenOk, _, semTokenErrors) = await EnvelopeAsync(semToken);
        semTokenOk.Should().BeFalse();
        semTokenErrors.Should().NotBeEmpty();

        // Admin COM o token: passa (idempotente — operador já está ativo).
        var adminXsrf = await XsrfAsync(admin);
        var comToken = await admin.SendAsync(
            Write(HttpMethod.Patch, url, payload, adminXsrf));
        comToken.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── escrita Operate + leitura Read ──────────────────────────────────────

    [Fact]
    public async Task Operate_cria_task_para_operador_viewer_le_mas_nao_escreve()
    {
        var operador = await LoginAsync(E2EHost.OperatorEmail);
        var viewer = await LoginAsync(E2EHost.ViewerEmail);

        // Read: viewer lê.
        (await viewer.GetAsync("/api/v1/tasks"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Operate: viewer não escreve (mesmo com token antiforgery válido).
        var viewerXsrf = await XsrfAsync(viewer);
        var viewerCreate = await viewer.SendAsync(Write(HttpMethod.Post, "/api/v1/tasks",
            new { title = "viewer não deve criar", kind = "Manual" }, viewerXsrf));
        viewerCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Operate: operador cria.
        var operadorXsrf = await XsrfAsync(operador);
        var criada = await operador.SendAsync(Write(HttpMethod.Post, "/api/v1/tasks",
            new { title = $"e2e {Guid.NewGuid():N}", kind = "Manual" }, operadorXsrf));
        ((int)criada.StatusCode).Should().BeInRange(200, 299);
        var (ok, _, errors) = await EnvelopeAsync(criada);
        ok.Should().BeTrue(string.Join("; ", errors));
    }

    // ── webhook público do Zabbix (ADR-023) ─────────────────────────────────

    [Fact]
    public async Task Webhook_do_zabbix_e_publico_e_confiavel_pelo_token()
    {
        var client = NewClient();
        var payload = new
        {
            eventId = "1001",
            host = "e2e-host",
            ip = "10.0.0.9",
            name = "CPU em 99%",
            severity = 3,
            status = "PROBLEM"
        };

        var semToken = await client.SendAsync(
            Write(HttpMethod.Post, "/api/v1/integrations/zabbix/problems", payload));
        semToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var (semTokenOk, _, semTokenErrors) = await EnvelopeAsync(semToken);
        semTokenOk.Should().BeFalse();
        semTokenErrors.Should().ContainSingle()
            .Which.Should().Be("Token do Zabbix inválido.");

        var comToken = new HttpRequestMessage(HttpMethod.Post,
            "/api/v1/integrations/zabbix/problems") { Content = JsonContent.Create(payload) };
        comToken.Headers.Add("X-Zabbix-Token", E2EHost.ZabbixToken);
        var criado = await client.SendAsync(comToken);
        criado.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnvelopeAsync(criado)).IsSuccess.Should().BeTrue();

        // Reenvio do mesmo evento: contrato segue 200 (deduplicação é do handler).
        // HttpRequestMessage é de uso único — o pedido é reconstruído.
        var repetidoRequest = new HttpRequestMessage(HttpMethod.Post,
            "/api/v1/integrations/zabbix/problems") { Content = JsonContent.Create(payload) };
        repetidoRequest.Headers.Add("X-Zabbix-Token", E2EHost.ZabbixToken);
        var repetido = await client.SendAsync(repetidoRequest);
        repetido.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnvelopeAsync(repetido)).IsSuccess.Should().BeTrue();
    }

    // ── 404 embrulhado ──────────────────────────────────────────────────────

    [Fact]
    public async Task Recurso_inexistente_devolve_404_no_envelope()
    {
        var viewer = await LoginAsync(E2EHost.ViewerEmail);
        var response = await viewer.GetAsync($"/api/v1/tasks/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var (ok, _, errors) = await EnvelopeAsync(response);
        ok.Should().BeFalse();
        errors.Should().NotBeEmpty();
    }

    // ── alinhamento front×back: o que o client.ts realmente envia ──────────

    [Fact]
    public async Task Logout_sem_body_envia_204_e_mata_a_sessao()
    {
        var client = await LoginAsync(E2EHost.AdminEmail, E2EHost.AdminPassword);
        var xsrf = await XsrfAsync(client);

        // auth.ts: api.post("/auth/logout") sem payload — logo SEM body e sem
        // Content-Type, só o X-XSRF-TOKEN. Se o [FromBody] nullable do
        // controller não aceitasse o vazio, a tela sairia com o cookie vivo.
        var response = await client.SendAsync(
            HeaderOnly(HttpMethod.Post, "/api/v1/auth/logout", xsrf));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            $"o logout como o front envia deve passar: {await response.Content.ReadAsStringAsync()}");

        // O cookie morreu junto: o próximo boot (/auth/me) é 401.
        (await client.GetAsync("/api/v1/auth/me")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sessoes_no_formato_da_SessionsPage_revogam_sem_vir_oraculo()
    {
        var a = await LoginAsync(E2EHost.AdminEmail, E2EHost.AdminPassword);
        var b = await LoginAsync(E2EHost.AdminEmail, E2EHost.AdminPassword);
        var xsrf = await XsrfAsync(a);

        var list = await a.GetAsync("/api/v1/auth/sessions");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var (ok, data, _) = await EnvelopeAsync(list);
        ok.Should().BeTrue();
        data.ValueKind.Should().Be(JsonValueKind.Array);

        // As duas sessões desta tarefa convivem com as deixadas pelos outros
        // facts (fixture compartilhada) — o mínimo é 2.
        data.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);

        // Contrato exato de SessionItem (sessions.ts): estes 6 campos, no
        // camelCase, e um isCurrent por cookie.
        var items = data.EnumerateArray().ToList();
        foreach (var item in items)
        {
            foreach (var prop in new[] { "id", "createdAt", "lastSeenAt", "expiresAt", "absoluteExpiresAt", "isCurrent" })
                item.TryGetProperty(prop, out _).Should().BeTrue($"a sessão precisa expor {prop}");
        }

        items.Count(i => i.GetProperty("isCurrent").GetBoolean())
            .Should().Be(1, "só a sessão do cookie atual é 'Este dispositivo'");

        // "Sair de todos os outros dispositivos": a outra janela morre na hora.
        var others = await a.SendAsync(
            HeaderOnly(HttpMethod.Delete, "/api/v1/auth/sessions/others", xsrf));
        others.StatusCode.Should().Be(HttpStatusCode.OK,
            $"revoke-others deve passar: {await others.Content.ReadAsStringAsync()}");
        (await b.GetAsync("/api/v1/auth/me")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);

        // Sid aleatório: 404 no envelope (nunca 403 — não vira oráculo).
        var inexistente = await a.SendAsync(HeaderOnly(HttpMethod.Delete,
            $"/api/v1/auth/sessions/{Guid.NewGuid()}", xsrf));
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var (naoOk, _, erros) = await EnvelopeAsync(inexistente);
        naoOk.Should().BeFalse();
        erros.Should().NotBeEmpty();

        // Revogar a própria: 200 e o cookie morre (a tela trata como logout).
        var currentId = items.Single(i => i.GetProperty("isCurrent").GetBoolean())
            .GetProperty("id").GetString();
        var propria = await a.SendAsync(HeaderOnly(HttpMethod.Delete,
            $"/api/v1/auth/sessions/{currentId}", xsrf));
        propria.StatusCode.Should().Be(HttpStatusCode.OK,
            $"revoke da sessão atual deve passar: {await propria.Content.ReadAsStringAsync()}");
        (await a.GetAsync("/api/v1/auth/me")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Forgot_password_devolve_a_mensagem_generica_que_a_tela_exibe()
    {
        var client = NewClient();

        // auth.ts: data?..message — anti-enumeração (a resposta é igual para
        // e-mail inexistente) e a tela tem fallback pronto.
        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { email = "nao-cadastrado@e2e.test" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (ok, data, _) = await EnvelopeAsync(response);
        ok.Should().BeTrue();
        data.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
