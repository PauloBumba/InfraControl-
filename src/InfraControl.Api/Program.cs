using InfraControl.Application;
using InfraControl.Application.Identity;
using InfraControl.Application.Identity.Roles.Commands;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Api.Authorization;
using InfraControl.Api.Middleware;
using InfraControl.Application.Identity.Sessions;
using InfraControl.Api.Security;
using InfraControl.Api.RateLimiting;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Infrastructure.Services;
using InfraControl.Infrastructure.Auth;
using InfraControl.Api.Hubs;
using InfraControl.Api.Services;
using InfraControl.Api.Workers;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.SignalR;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "InfraControl.Api")
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// ── Infrastructure ───────────────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationMediatR();
builder.Services.AddSingleton(builder.Configuration
    .GetSection(BootstrapAdminOptions.SectionName)
    .Get<BootstrapAdminOptions>() ?? new BootstrapAdminOptions());
builder.Services.AddScoped<IAdminBootstrapper, AdminBootstrapper>();

// ── Controllers ──────────────────────────────────────────────────────────
// F0-T023: todo corpo de erro — inclusive ModelState do binding — usa o
// envelope ApiResponse do BuildingBlocks (nunca ProblemDetails nem { error }).
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .SelectMany(entry => entry.Value?.Errors ?? [])
            .Select(error => error.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .DefaultIfEmpty("Dados inválidos enviados na requisição.")
            .ToArray();

        return new BadRequestObjectResult(ApiResponse<object>.Fail(errors));
    };
});
// ── Notificações (F9-T004: canal SignalR) ────────────────────────────────
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, SignalRUserIdProvider>();
builder.Services.AddSingleton<ISignalRNotificationDispatcher, SignalRNotificationDispatcher>();
builder.Services.AddHostedService<NotificationSignalRWorker>();
builder.Services.AddHostedService<NotificationEmailWorker>();
builder.Services.AddHostedService<NotificationTelegramWorker>();
builder.Services.AddHostedService<NotificationWhatsAppWorker>();
builder.Services.AddHostedService<NotificationWebhookWorker>();
builder.Services.AddHostedService<NotificationSlackWorker>();
// F9-T006: o admin escolhe os canais do fanout e os destinos por canal
// (Notifications:Channels) — a MESMA seção lida pelo NotificationEventConsumer
// no host dos Workers; defaults vazios = comportamento Email+SignalR.
builder.Services
    .AddOptions<NotificationChannelsOptions>()
    .Bind(builder.Configuration.GetSection(NotificationChannelsOptions.SectionName));

// F16-T001/ADR-023: token do webhook de entrada do Zabbix (Integrations:
// Zabbix) — vazio = integração desligada (404 no endpoint), não valida no
// boot de propósito (o desligado é o default).
builder.Services
    .AddOptions<ZabbixIntegrationOptions>()
    .Bind(builder.Configuration.GetSection(ZabbixIntegrationOptions.SectionName));

// F0-T023: o GlobalExceptionHandler é o único ponto de erro da API, mas o
// middleware do ASP.NET (UseExceptionHandler sem path/delegate) só sobe se o
// IProblemDetailsService existir — AddExceptionHandler<T> NÃO o registra.
// Sem esta linha o pipeline falha em tempo de build ("ExceptionHandlingPath or
// ExceptionHandler must be set") e a API não sobe em NENHUM ambiente.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
        options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    })
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

// F1-T014: o fluxo do dashboard é Google Identity Services → id_token →
// POST /api/v1/auth/google, com a validação da credencial no servidor
// (GoogleJsonWebSignature: assinatura/JWKS, aud, iss, exp, email_verified).
// Nenhum handler de authorization code do Google é registrado de propósito:
// a API não troca código por token, portanto não existe Client Secret em
// lugar nenhum do sistema — a única configuração é o ClientId público,
// usado também como `aud`. Menos superfície de segredo, menos dependência.
// Autorização HTTP: 3 níveis canônicos (Operate/Administer) sobre a role,
// fonte única da verdade (ADR-016). Leitura é [Authorize] simples. A mesma
// hierarquia é aplicada na camada MediatR por IAuthorizedRequest.RequiredRoles.
builder.Services.AddAuthorization(options =>
    options.AddRolePolicies());

// F1-T015/ADR-017: double-submit de CSRF. A configuração fica em
// AntiforgeryCookieSetup para que os testes validem exatamente a mesma
// configuração que roda (cookie XSRF-TOKEN legível + header X-XSRF-TOKEN).
builder.Services.AddAntiforgery(AntiforgeryCookieSetup.Configure);

// F1-T016(b)/ADR-018 + F1-T016b/ADR-020: prazos de sessão em configuração,
// inclusive o corte por role (RoleAbsoluteLifetimes: cap só encurta e precisa
// nomear role conhecida). A validação acontece na resolução do IOptions,
// abaixo, porque configuração sem sentido é melhor derrubando o boot do que
// aparecendo em produção.
builder.Services.AddMemoryCache();
builder.Services
    .AddOptions<SessionLifetimeOptions>()
    .Bind(builder.Configuration.GetSection(SessionLifetimeOptions.SectionName))
    .Validate(options => options.IsValid,
        "Session inválida: AbsoluteLifetime e CheckInterval > 0, AbsoluteLifetime <= SlidingLifetime, RoleAbsoluteLifetimes com valores > 0 e roles conhecidas (Admin, Operator, Viewer).")
    .ValidateOnStart();
builder.Services.Configure<CookiePolicyOptions>(options =>
    options.CheckConsentNeeded = _ => false);

// ── Rate limiting HTTP (ADR-011 / docs/08) ───────────────────────────────
// Camada HTTP do ASP.NET Core Rate Limiter com estado distribuído no Redis
// (GlobalLimiter) e janela em memória como fallback. A camada MediatR
// (RateLimitingBehavior) continua como segunda barreira.
builder.Services.AddHttpRateLimiting(builder.Configuration);

// ── OpenAPI ──────────────────────────────────────────────────────────────
builder.Services.AddOpenApi();

// ── Health Checks ────────────────────────────────────────────────────────
var postgresConn = builder.Configuration.GetConnectionString("DefaultConnection");
var redisConn = builder.Configuration.GetConnectionString("Redis");

builder.Services.AddHealthChecks()
    .AddNpgSql(postgresConn!, name: "postgresql", tags: ["db", "core"])
    .AddRedis(redisConn!, name: "redis", tags: ["cache"]);

// ── OpenTelemetry ────────────────────────────────────────────────────────
var otelServiceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "InfraControl.Api";
var otelEndpoint = builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(otelServiceName)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("InfraControl.*")
        .AddOtlpExporter(opts => opts.Endpoint = new Uri(otelEndpoint)))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter("InfraControl.*")
        .AddOtlpExporter(opts => opts.Endpoint = new Uri(otelEndpoint)));

// ── Build ────────────────────────────────────────────────────────────────
var app = builder.Build();

// Bootstrap Admin é opt-in e lê a senha apenas da configuração/ambiente seguro.
if (builder.Configuration.GetValue<bool>($"{BootstrapAdminOptions.SectionName}:Enabled"))
{
    using var bootstrapScope = app.Services.CreateScope();
    var bootstrapper = bootstrapScope.ServiceProvider.GetRequiredService<IAdminBootstrapper>();
    await bootstrapper.RunAsync();
}

// ── Pipeline ─────────────────────────────────────────────────────────────
// F0-T023: exceções não tratadas → envelope ApiResponse (deve ser o primeiro).
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();

// F1-T015/ADR-017: escrita autenticada por cookie exige token de antiforgery.
// Fica no pipeline (e não como atributo de controller) para que um endpoint
// novo nasça protegido por padrão. Depende de UseAuthentication: precisa saber
// se a request está autenticada para decidir se valida.
// ADR-021: PRIMEIRO middleware do pipeline, antes do Serilog request logging e
// antes de qualquer tratamento de erro. Se a requisição que estourou não tem
// correlation id na linha, o log de rastro não serve para nada.
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseMiddleware<AntiforgeryCookieMiddleware>();

// F1-T016(b)/ADR-018: a sessão que o cookie afirma é conferida contra o banco
// a cada request autenticado, com cache curto. Também depois de
// UseAuthentication e antes de UseAuthorization, para a sessão morrer antes de
// qualquer decisão de endpoint.
app.UseMiddleware<ActiveSessionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

// Health Checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("core")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/startup", new HealthCheckOptions
{
    Predicate = _ => true
});

app.MapControllers();

// Hub de notificações em tempo real (F9-T004).
app.MapHub<NotificationsHub>("/hubs/notifications");

app.Run();

// Exposto para os testes e2e (WebApplicationFactory<Program> roda o host
// real — pipeline, policies, antiforgery e GlobalExceptionHandler intactos).
public partial class Program;
