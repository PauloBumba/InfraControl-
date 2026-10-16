using FluentAssertions;
using InfraControl.ArchitectureTests.Common;

namespace InfraControl.ArchitectureTests.Rules;

/// <summary>
/// Alinhamento de endpoints entre nginx, frontend e API (ADR-022).
///
/// O nginx é o único entry point da aplicação: ele termina o TLS, serve a SPA
/// e faz proxy de /api, /hubs e /health para a API. Front e back só se falam
/// pela mesma origem — não existe CORS para consertar divergência de caminho.
///
/// Por isso cada ponte é conferida por texto nos arquivos de configuração:
/// uma troca de rota, um location a menos ou uma env não repassada ao build
/// derrubaria a comunicação sem erro de build nem teste unitário.
/// </summary>
public sealed class EndpointAlignmentTests : BaseArchitectureTest
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

    // ── API ────────────────────────────────────────────────────────────────
    [Fact]
    public void Api_Should_Consume_Forwarded_Headers_From_The_Proxy()
    {
        // Sem isto todo request chega com o IP do container nginx: o rate limit
        // por IP vira limite global e os links de e-mail saem com scheme/host
        // da rota interna (http://api:8080) em vez da origem do navegador.
        var source = Read("src", "InfraControl.Api", "Program.cs");

        source.Should().Contain("app.UseForwardedHeaders()");
        source.Should().Contain("ForwardedHeaders.XForwardedFor");
        source.Should().Contain("ForwardedHeaders.XForwardedProto");
    }

    [Fact]
    public void Api_Should_Not_Redirect_To_Https()
    {
        // O TLS termina no nginx (ADR-022): a API só escuta HTTP dentro da rede
        // Docker, então não existe porta HTTPS para a qual redirecionar.
        var source = Read("src", "InfraControl.Api", "Program.cs");

        source.Should().NotContain("app.UseHttpsRedirection");
    }

    [Fact]
    public void Controllers_Should_Use_The_V1_Route_Prefix()
    {
        // O nginx encaminha /api inteiro, então um controller fora do
        // versionamento passa despercebido em runtime e só aparece na hora de
        // consumir o endpoint. Todo route tem de ser api/v1/*.
        var controllersRoot = Path.Combine(FindRepositoryRoot(), "src", "InfraControl.Api", "Controllers");

        var routes = Directory
            .EnumerateFiles(controllersRoot, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .SelectMany(source => source.Split('\n'))
            .Where(line => line.Contains("[Route(\"", StringComparison.Ordinal))
            .Select(line => line.Trim())
            .ToList();

        routes.Should().NotBeEmpty("a API tem de ter controllers com [Route]");

        var withoutV1 = routes
            .Where(route => !route.Contains("api/v1/", StringComparison.Ordinal))
            .ToList();

        withoutV1.Should().BeEmpty(
            "todo [Route] deve ser versionado (api/v1/...); fora de versionamento: " +
            string.Join(", ", withoutV1));
    }

    // ── nginx ──────────────────────────────────────────────────────────────
    [Fact]
    public void Nginx_Should_Proxy_The_Api_Health_Checks()
    {
        // A API serve /health e /health/* na raiz; a SPA fica em /status.
        // Sem os locations, GET https://host/health cai no try_files e devolve
        // o index.html em vez do JSON de saúde.
        var source = Read("docker", "nginx.conf");

        source.Should().Contain("location = /health");
        source.Should().Contain("location /health/");
        // ADR-044 (41b9d7e): o proxy passa a usar o upstream `api_backend`
        // (load balancing entre réplicas) cujo alvo continua api:8080.
        source.Should().Contain("server api:8080");
    }

    [Fact]
    public void Nginx_Should_Proxy_The_Api_Prefix_With_And_Without_Trailing_Slash()
    {
        // "location /api/" não casa "/api": a requisição cai no SPA e devolve
        // HTML onde o cliente espera JSON.
        var source = Read("docker", "nginx.conf");

        source.Should().Contain("location /api {");
        source.Should().NotContain("location /api/ {");
    }

    [Fact]
    public void Nginx_Should_Forward_The_Client_Host_With_Its_Port()
    {
        // O nginx repassa `Host $host`, e `$host` descarta a porta do cliente
        // (localhost:8445 → localhost). Sem X-Forwarded-Host explícito, o
        // Request.Host da API perde :8445 e todos os links gerados com
        // Request.Scheme/Request.Host (forgot-password, convites) saem quebrados
        // em qualquer porta fora da padrão. Os 4 locations que falam com a API
        // têm de mandar o Host crudo ($http_host) — e a API tem de consumi-lo.
        var nginx = Read("docker", "nginx.conf");
        var source = Read("src", "InfraControl.Api", "Program.cs");

        nginx.Split('\n').Count(line => line.Contains("proxy_set_header X-Forwarded-Host $http_host;", StringComparison.Ordinal))
            .Should().BeGreaterThanOrEqualTo(4, "health (= e /), /api e /hubs/ precisam repassar a porta");

        source.Should().Contain("ForwardedHeaders.XForwardedHost");
        source.Should().Contain("app.UseForwardedHeaders()");
    }

    [Fact]
    public void Nginx_Should_Keep_The_Port_On_The_Http_To_Https_Redirect()
    {
        // Duas armadilhas: `$host` descarta a porta (com HTTP_PORT fora de 80 o
        // redirect ia para https://<host>/ e o cookie Secure nunca seria
        // gravado) e `$http_host` mantém a porta ERRADA (a HTTP de origem, não
        // a HTTPS de destino — 8085→8085 em vez de 8445). A porta correta vem
        // de fora por envsubst (NGINX_HTTPS_PORT).
        var source = Read("docker", "nginx.conf");

        source.Should().Contain("return 301 https://$host$https_port_suffix$request_uri;");
        source.Should().Contain("${NGINX_HTTPS_PORT}");
        source.Should().NotContain("return 301 https://$host$request_uri;");
        source.Should().NotContain("return 301 https://$http_host");

        // O template só é processado se o Dockerfile/COPY estiverem certos —
        // senão o container boota com "${NGINX_HTTPS_PORT}" literal e o nginx
        // recusa a config.
        var dockerfile = Read("docker", "Dockerfile");
        var compose = Read("docker-compose.yml");

        dockerfile.Should().Contain("NGINX_ENVSUBST_OUTPUT_DIR=/etc/nginx");
        dockerfile.Should().Contain("/etc/nginx/templates/nginx.conf.template");
        compose.Should().Contain("NGINX_HTTPS_PORT:");
    }

    [Fact]
    public void Compose_Should_Forward_The_Opt_In_Admin_Bootstrap()
    {
        // BootstrapAdmin__* está documentado em .env.example e em docs/05, mas
        // só chega na API se o compose repassar a env. Sem isso o primeiro
        // deploy nasce sem nenhum usuário Admin e toda rota com policy
        // Administer (convites, usuários) responde 403 — invisível para build
        // e para teste unitário.
        var compose = Read("docker-compose.yml");
        var envExample = Read(".env.example");

        compose.Should().Contain("BootstrapAdmin__Enabled:");
        compose.Should().Contain("BootstrapAdmin__Email:");
        compose.Should().Contain("BootstrapAdmin__DisplayName:");
        compose.Should().Contain("BootstrapAdmin__Password:");
        envExample.Should().Contain("BootstrapAdmin__Enabled=");
    }

    // ── frontend ───────────────────────────────────────────────────────────
    [Fact]
    public void Vite_Should_Proxy_The_Health_Checks_In_Development()
    {
        // Dev tem de enxergar a API como produção: mesma rota, mesmo alvo.
        var source = Read("frontend", "InfraControl.Web", "vite.config.ts");

        source.Should().Contain("\"/health\"");
        source.Should().Contain("\"/api\"");
        source.Should().Contain("\"/hubs\"");
    }

    [Fact]
    public void Spa_Should_Not_Reuse_The_Api_Health_Route()
    {
        // Colisão de caminho: se a página voltar para /health, ela disputa a
        // rota com a API no nginx e no proxy do Vite.
        var router = Read("frontend", "InfraControl.Web", "src", "app", "router", "index.tsx");
        var menu = Read("frontend", "InfraControl.Web", "src", "data", "dashboardData.ts");

        router.Should().NotContain("path=\"/health\"");
        menu.Should().NotContain("route: \"/health\"");
        router.Should().Contain("path=\"/status\"");
        menu.Should().Contain("route: \"/status\"");
    }

    [Fact]
    public void Build_Should_Receive_The_Seq_Url_For_The_Logs_Page()
    {
        // A LogsPage lê VITE_LOGS_URL no build; sem repassar a env, o botão de
        // logs aponta para a porta errada em qualquer host que não seja o dev.
        var dockerfile = Read("docker", "Dockerfile");
        var compose = Read("docker-compose.yml");
        var envTypes = Read("frontend", "InfraControl.Web", "src", "vite-env.d.ts");

        dockerfile.Should().MatchRegex(@"ARG VITE_LOGS_URL=");
        compose.Should().Contain("VITE_LOGS_URL:");
        envTypes.Should().Contain("VITE_LOGS_URL");
        envTypes.Should().Contain("VITE_API_BASE_URL");
    }
}
