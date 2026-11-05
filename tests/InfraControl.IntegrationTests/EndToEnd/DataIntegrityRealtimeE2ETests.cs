using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using InfraControl.Api.Hubs;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Domain.Events;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using InfraControl.Workers.Consumers.Notifications;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace InfraControl.IntegrationTests.EndToEnd;

/// <summary>
/// F46-T004 — integridade front↔banco e tempo real SignalR:
/// (1) escrita pela API → evento gravado no outbox na MESMA transação (sem
/// perda); (2) fila de deliveries → worker do canal → hub → cliente SignalR
/// autenticado recebe o push (evento→hub→UI, última milha real).
/// </summary>
public sealed class DataIntegrityRealtimeE2ETests(
    E2EHost host, ITestOutputHelper output) : IClassFixture<E2EHost>
{
    private const int RealtimeTimeoutSeconds = 20;

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
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        return cookies!.Single(c => c.StartsWith("XSRF-TOKEN="))
            .Split("XSRF-TOKEN=")[1].Split(";")[0];
    }

    // ── (1) Integridade: escrita → outbox na mesma transação ───────────────

    [Fact]
    public async Task IncidentCreate_ShouldPersistOutboxEvent_WithoutLoss()
    {
        var client = await LoginAsync(E2EHost.OperatorEmail);
        var xsrf = await XsrfAsync(client);
        var title = $"T004 outbox {Guid.NewGuid():N}";

        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/incidents")
        {
            Content = JsonContent.Create(new { title, severity = "high" })
        };
        create.Headers.Add("X-XSRF-TOKEN", xsrf);
        var response = await client.SendAsync(create);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            await response.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var incidentId = json.RootElement.GetProperty("data")
            .GetProperty("id").GetGuid();

        using var scope = host.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // A escrita E o evento estão no banco (mesma transação de commit).
        (await db.Incidents.FindAsync(incidentId)).Should().NotBeNull(
            "o incidente deve persistir");
        var outbox = await db.OutboxMessages
            .Where(m => m.EventType == nameof(IncidentCreatedDomainEvent))
            .OrderByDescending(m => m.OccurredAt)
            .FirstAsync();
        outbox.Payload.Should().Contain(incidentId.ToString(),
            "o payload do evento deve carregar o id do incidente (sem perda)");
        outbox.ProcessedAt.Should().BeNull(
            "o dispatcher (worker separado) é quem marca; a API só grava");
        outbox.CorrelationId.Should().NotBeNullOrWhiteSpace(
            "correlação da requisição acompanha o evento (ADR-021)");
    }

    // ── (2) Tempo real: delivery → worker → hub → cliente SignalR ──────────

    /// <summary>
    /// Handler que finge ser o navegador: guarda os cookies do login e os
    /// reenvia — o SignalR Client não tem CookieContainer do TestServer.
    /// </summary>
    private sealed class SessionCookieHandler(HttpMessageHandler inner)
        : DelegatingHandler(inner)
    {
        public CookieContainer Cookies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var header = Cookies.GetCookieHeader(uri);
            if (!string.IsNullOrEmpty(header))
                request.Headers.TryAddWithoutValidation("Cookie", header);

            var response = await base.SendAsync(request, cancellationToken);

            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                // Um header por cookie: juntar com "; " corromperia o parser.
                foreach (var cookie in setCookies)
                    Cookies.SetCookies(uri, cookie);
            }

            return response;
        }
    }

    [Fact]
    public async Task QueuedDelivery_ShouldReachConnectedSignalRClient()
    {
        // Usuário logado = destinatário do push.
        Guid userId;
        using (var scope = host.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var domainUser = await db.DomainUsers
                .SingleAsync(u => u.Email == E2EHost.AdminEmail);
            userId = domainUser.Id;
        }

        // Pipeline de cookies compartilhado entre HttpClient e SignalR Client.
        var session = new SessionCookieHandler(host.Factory.Server.CreateHandler());
        using var loginClient = new HttpClient(session)
        {
            BaseAddress = new Uri("https://localhost")
        };
        var login = await loginClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = E2EHost.AdminEmail, password = E2EHost.AdminPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK,
            await login.Content.ReadAsStringAsync());

        // O XSRF-TOKEN nasce no /auth/me (como o front faz no boot) — o
        // negotiate do SignalR é um POST autenticado e passa pelo
        // AntiforgeryCookieMiddleware (ADR-017, mesmo caminho do front).
        var me = await loginClient.GetAsync("/api/v1/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var siteUri = new Uri("https://localhost");
        var xsrf = session.Cookies.GetCookies(siteUri)["XSRF-TOKEN"]?.Value;
        xsrf.Should().NotBeNullOrWhiteSpace(
            "o /auth/me deve emitir o cookie XSRF-TOKEN");

        var received = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri("https://localhost/hubs/notifications"), options =>
            {
                // O MESMO handler de sessão: o hub exige o cookie de autenticação.
                options.HttpMessageHandlerFactory = _ => session;
                // Header do negotiate: idêntico ao createHubConnection do front.
                options.Headers!["X-XSRF-TOKEN"] = xsrf!;
            })
            .Build();

        connection.On<JsonElement>("notification", payload =>
        {
            // Outros testes da classe podem deixar deliveries pendentes no
            // mesmo host — só a "nossa" finaliza o teste.
            if (payload.GetProperty("title").GetString() == "F46-T004 realtime")
                received.TrySetResult(payload);
        });

        await connection.StartAsync();
        connection.State.Should().Be(HubConnectionState.Connected);

        try
        {
            // O consumer (mesmo do RabbitMQ) enfileira a delivery pendente.
            using (var scope = host.Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var consumer = new NotificationEventConsumer(
                    db,
                    new InboxStore(db),
                    Options.Create(new NotificationChannelsOptions()),
                    NullLogger<NotificationEventConsumer>.Instance);

                var domainEvent = new UserCreatedDomainEvent(
                    userId, E2EHost.AdminEmail);
                await consumer.HandleAsync(
                    domainEvent.EventId, domainEvent.EventType,
                    "F46-T004 realtime", "Push end-to-end pelo hub.",
                    recipientUserId: userId, recipientRoles: [],
                    CancellationToken.None);
            }

            // NotificationSignalRWorker (hosted service real do host) faz o
            // poll (2s) e despacha para o grupo user:{userId} do hub.
            var completed = await Task.WhenAny(
                received.Task,
                Task.Delay(TimeSpan.FromSeconds(RealtimeTimeoutSeconds)));
            completed.Should().Be(received.Task,
                $"o cliente deveria receber o push em até {RealtimeTimeoutSeconds}s");

            var payload = await received.Task;
            output.WriteLine(
                $"[signalr] recebido: {payload.GetRawText()}");
            payload.GetProperty("title").GetString()
                .Should().Be("F46-T004 realtime");
            payload.GetProperty("eventType").GetString()
                .Should().Be(nameof(UserCreatedDomainEvent));
            payload.GetProperty("deliveryId").GetGuid()
                .Should().NotBeEmpty();
        }
        finally
        {
            await connection.DisposeAsync();
        }

        // Integridade: a delivery foi marcada como enviada pelo worker.
        using (var verifyScope = host.Factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var delivery = await (
                from d in db.NotificationDeliveries
                join n in db.Notifications on d.NotificationId equals n.Id
                where d.UserId == userId && n.Title == "F46-T004 realtime"
                orderby d.CreateDateTime descending
                select d).FirstOrDefaultAsync();
            delivery.Should().NotBeNull(
                "a delivery criada pelo consumer deve existir");
            delivery!.Status.Should().Be(DeliveryStatus.Sent,
                "após o push ao hub, o worker marca a delivery como enviada");
        }
    }

    /// <summary>
    /// Deduplicação (inbox): reentregar o MESMO domain event não cria segunda
    /// notification/delivery — sem duplicação de efeito no consumidor.
    /// </summary>
    [Fact]
    public async Task Consumer_ShouldDeduplicateRedeliveredEvent()
    {
        Guid userId;
        using (var scope = host.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userId = (await db.DomainUsers
                .SingleAsync(u => u.Email == E2EHost.AdminEmail)).Id;
        }

        using var dedupScope = host.Factory.Services.CreateScope();
        var db2 = dedupScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var consumer = new NotificationEventConsumer(
            db2,
            new InboxStore(db2),
            Options.Create(new NotificationChannelsOptions()),
            NullLogger<NotificationEventConsumer>.Instance);

        var domainEvent = new UserCreatedDomainEvent(
            userId, "dedup@t004.test");

        await consumer.HandleAsync(
            domainEvent.EventId, domainEvent.EventType,
            "Dedup T004", "Só deve contar uma vez.",
            recipientUserId: userId, recipientRoles: [],
            CancellationToken.None);
        // Reentrega (retry de broker / reprocessamento manual).
        await consumer.HandleAsync(
            domainEvent.EventId, domainEvent.EventType,
            "Dedup T004", "Só deve contar uma vez.",
            recipientUserId: userId, recipientRoles: [],
            CancellationToken.None);

        var deliveries = await db2.Notifications
            .Where(n => n.Title == "Dedup T004")
            .CountAsync();
        deliveries.Should().Be(1,
            "o inbox (ProcessedMessages) deduplica a reentrega do mesmo EventId");

        var processed = await db2.ProcessedMessages
            .Where(p => p.EventId == domainEvent.EventId)
            .CountAsync();
        processed.Should().Be(1, "o registro de inbox deve ser único por evento");

        // Cleanup: não deixar delivery pendente para o worker do host despachar
        // durante o teste SignalR (mesmo usuário destinatário).
        var notifications = await db2.Notifications
            .Where(n => n.Title == "Dedup T004")
            .ToListAsync();
        var notificationIds = notifications.Select(n => n.Id).ToList();
        var pendingDeliveries = await db2.NotificationDeliveries
            .Where(d => notificationIds.Contains(d.NotificationId))
            .ToListAsync();
        db2.NotificationDeliveries.RemoveRange(pendingDeliveries);
        db2.Notifications.RemoveRange(notifications);
        await db2.SaveChangesAsync();
    }
}
