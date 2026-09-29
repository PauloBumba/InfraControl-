using InfraControl.Application.Identity;
using InfraControl.Application.Identity.Roles.Commands;
using InfraControl.Api.Authorization;
using InfraControl.Api.Middleware;
using InfraControl.Api.RateLimiting;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Infrastructure.Services;
using InfraControl.Infrastructure.Auth;
using InfraControl.Api.Hubs;
using InfraControl.Api.Services;
using InfraControl.Api.Workers;
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
builder.Services.AddMediatR(configuration =>
    configuration.RegisterServicesFromAssembly(typeof(InfraControl.Application.Identity.AdminBootstrapper).Assembly));
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
