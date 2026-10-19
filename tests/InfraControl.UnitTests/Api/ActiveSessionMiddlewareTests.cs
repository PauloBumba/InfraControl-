using System.Security.Claims;
using FluentAssertions;
using InfraControl.Api.Middleware;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Identity.Sessions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Api;

/// <summary>
/// F1-T016(b) / ADR-018: o cookie é auto-contido e por isso não tinha
/// autoridade nenhuma — revogar sessão não tirava ninguém de lugar. O
/// middleware é o que devolve essa autoridade, então a política é testada aqui
/// com um leitor falso (é a decisão que importa: quem passa, quem é recusado,
/// quem paga consulta ao banco).
///
/// O leitor de verdade é testado em <c>ActiveSessionReaderTests</c> (Sqlite,
/// com os estados vencidos fabricados por SQL) — a consulta é o outro lado.
/// </summary>
public sealed class ActiveSessionMiddlewareTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SessionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class StubReader(Func<Guid, Guid, ActiveSessionCheck> respond)
        : IActiveSessionReader
    {
        public int Calls { get; private set; }

        public Task<ActiveSessionCheck> CheckAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(respond(sessionId, userId));
        }
    }

    private static ClaimsPrincipal Principal(Guid? userId, Guid? sessionId)
    {
        var claims = new List<Claim>();
        if (userId is not null) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        if (sessionId is not null) claims.Add(new Claim(ActiveSessionMiddleware.SessionIdClaim, sessionId.Value.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie", ClaimTypes.Name, ClaimTypes.Role));
    }

    /// <summary>
    /// O middleware assina o cookie fora (SignOutAsync) ao recusar. Isso resolve
    /// IAuthenticationService do RequestServices — sem um provider aqui o teste
    /// estouraria por falta de container, não por política.
    /// </summary>
    private sealed class StubAuthenticator : IAuthenticationService
    {
        public int SignOuts { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOuts++;
            return Task.CompletedTask;
        }
    }

    private static DefaultHttpContext Request(
        ClaimsPrincipal principal,
        IActiveSessionReader? reader = null,
        ICacheService? cache = null,
        StubAuthenticator? authenticator = null)
    {
        var context = new DefaultHttpContext { User = principal };
        context.Response.Body = new MemoryStream();

        // O middleware resolve IActiveSessionReader e ICacheService POR REQUEST
        // (escopados): injetá-los no construtor do singleton os capturaria no
        // root provider e o host com ValidateScopes não sobe — bug real achado
        // pelo e2e de pipeline completo. Os stubs vão no RequestServices, como
        // os serviços de verdade ficam no host.
        context.RequestServices = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authenticator ?? new StubAuthenticator())
            .AddSingleton<IActiveSessionReader>(reader ?? new StubReader((_, _) => ActiveSessionCheck.Active))
            .AddSingleton<ICacheService>(cache ?? new FakeCacheService())
            .AddLogging()
            .BuildServiceProvider();
        return context;
    }

    private static ActiveSessionMiddleware Middleware(
        TimeSpan? checkInterval = null,
        Action? onNext = null)
        => new(
            _ => { onNext?.Invoke(); return Task.CompletedTask; },
            Options.Create(new SessionLifetimeOptions
            {
                SlidingLifetime = TimeSpan.FromDays(30),
                AbsoluteLifetime = TimeSpan.FromHours(12),
                CheckInterval = checkInterval ?? TimeSpan.FromSeconds(30)
            }),
            NullLogger<ActiveSessionMiddleware>.Instance);

    private static ClaimsPrincipal ActiveUser(Guid? sessionId = null) => Principal(UserId, sessionId ?? SessionId);

    [Fact]
    public async Task Active_Session_Should_Pass()
    {
        var reached = false;
        var context = Request(ActiveUser(), new StubReader((_, _) => ActiveSessionCheck.Active));

        await Middleware(onNext: () => reached = true).InvokeAsync(context);

        reached.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData(ActiveSessionStatus.NotFound)]
    [InlineData(ActiveSessionStatus.Revoked)]
    [InlineData(ActiveSessionStatus.SlidingExpired)]
    [InlineData(ActiveSessionStatus.AbsoluteExpired)]
    [InlineData(ActiveSessionStatus.WrongUser)]
    public async Task Dead_Session_Should_Be_401(ActiveSessionStatus status)
    {
        var reached = false;
        var authenticator = new StubAuthenticator();
        var context = Request(ActiveUser(), new StubReader((_, _) => new ActiveSessionCheck(status)),
            authenticator: authenticator);

        await Middleware(onNext: () => reached = true).InvokeAsync(context);

        reached.Should().BeFalse("a sessão morreu antes do endpoint");
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        authenticator.SignOuts.Should().Be(1, "o cookie morto é apagado, senão o browser reenvia e o usuário preso em 401");
    }

    [Fact]
    public async Task Rejection_Should_Not_Say_Why()
    {
        // "sessão revogada" vs "não encontrei a sessão" vs "de outra pessoa" é
        // informação sobre a sessão para quem tem o cookie
        var messages = new List<string>();

        foreach (var status in new[]
                 {
                     ActiveSessionStatus.NotFound, ActiveSessionStatus.Revoked,
                     ActiveSessionStatus.SlidingExpired, ActiveSessionStatus.AbsoluteExpired,
                     ActiveSessionStatus.WrongUser
                 })
        {
            var context = Request(ActiveUser(), new StubReader((_, _) => new ActiveSessionCheck(status)));
            await Middleware().InvokeAsync(context);

            context.Response.Body.Position = 0;
            messages.Add(await new StreamReader(context.Response.Body).ReadToEndAsync());
        }

        messages.Distinct().Should().HaveCount(1, "a resposta é a mesma nos cinco casos");
    }

    private static ClaimsPrincipal MalformedSidPrincipal() => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
         new Claim(ActiveSessionMiddleware.SessionIdClaim, "não-é-guid")],
        "cookie", ClaimTypes.Name, ClaimTypes.Role));

    [Fact]
    public async Task Cookie_Without_Session_Claim_Should_Be_401_Without_Hitting_The_Database()
    {
        // cookie emitido antes da ADR-018: fail-closed, e sem consulta —
        // não há id para consultar
        var reader = new StubReader((_, _) => ActiveSessionCheck.Active);
        var context = Request(Principal(UserId, sessionId: null), reader);

        await Middleware().InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        reader.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Malformed_Session_Claim_Should_Be_401_Without_Hitting_The_Database()
    {
        var reader = new StubReader((_, _) => ActiveSessionCheck.Active);
        var context = Request(MalformedSidPrincipal(), reader);

        await Middleware().InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized, "sid ilegível é fail-closed");
        reader.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Anonymous_Should_Pass_Without_Hitting_The_Database()
    {
        var reader = new StubReader((_, _) => new ActiveSessionCheck(ActiveSessionStatus.Revoked));
        var reached = false;

        await Middleware(onNext: () => reached = true)
            .InvokeAsync(Request(new ClaimsPrincipal(new ClaimsIdentity()), reader));

        reached.Should().BeTrue();
        reader.Calls.Should().Be(0, "anônimo não tem sessão para conferir");
    }

    [Fact]
    public async Task Second_Request_In_The_Check_Interval_Should_Not_Hit_The_Database()
    {
        // o cache é o motivo de a verificação ser barata: sem ele seria uma
        // consulta ao banco por request em toda a API
        var reader = new StubReader((_, _) => ActiveSessionCheck.Active);
        var cache = new FakeCacheService();
        var middleware = Middleware();

        await middleware.InvokeAsync(Request(ActiveUser(), reader, cache));
        await middleware.InvokeAsync(Request(ActiveUser(), reader, cache));
        await middleware.InvokeAsync(Request(ActiveUser(), reader, cache));

        reader.Calls.Should().Be(1, "três requests dentro da janela, uma consulta");
    }

    [Fact]
    public async Task Rejected_Session_Should_Also_Be_Cached()
    {
        // sem isto, cookie morto vira uma consulta por request enquanto o
        // usuário navega na tela de erro
        var reader = new StubReader((_, _) => new ActiveSessionCheck(ActiveSessionStatus.Revoked));
        var cache = new FakeCacheService();
        var middleware = Middleware();

        await middleware.InvokeAsync(Request(ActiveUser(), reader, cache));
        await middleware.InvokeAsync(Request(ActiveUser(), reader, cache));

        reader.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Check_Interval_Expiry_Should_Recheck()
    {
        var reader = new StubReader((_, _) => ActiveSessionCheck.Active);
        var cache = new FakeCacheService();
        var middleware = Middleware(checkInterval: TimeSpan.FromMilliseconds(1));

        await middleware.InvokeAsync(Request(ActiveUser(), reader, cache));
        await Task.Delay(30);
        await middleware.InvokeAsync(Request(ActiveUser(), reader, cache));

        reader.Calls.Should().Be(2, "passado o intervalo, a sessão é conferida de novo — é aí que a revogação passa a valer");
    }

    [Fact]
    public async Task Revoked_Check_In_The_Shared_Cache_Should_Reject_Without_The_Reader()
    {
        // ADR-020: é ASSIM que a revogação escrita por outra instância vale na
        // hora — quem revoga grava o check "Revoked" no cache compartilhado e o
        // middleware lê o cache antes de qualquer consulta ao banco. Sem isto a
        // janela de 30s da ADR-018 continuaria de pé entre instâncias.
        var reader = new StubReader((_, _) => ActiveSessionCheck.Active);
        var cache = new FakeCacheService();
        await cache.SetAsync(ActiveSessionCache.Key(SessionId), new ActiveSessionCheck(ActiveSessionStatus.Revoked));
        var context = Request(ActiveUser(), reader, cache);

        await Middleware().InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        reader.Calls.Should().Be(0, "o estado revogado já estava no cache compartilhado");
    }

    [Fact]
    public async Task Session_Of_Another_User_Should_Be_Refused_By_The_Reader()
    {
        // o sid vem do cookie: o leitor precisa da dona para conferir
        var attacker = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var seen = Guid.Empty;
        var reader = new StubReader((_, userId) =>
        {
            seen = userId;
            return new ActiveSessionCheck(ActiveSessionStatus.Active);
        });

        await Middleware().InvokeAsync(Request(Principal(attacker, SessionId), reader));

        seen.Should().Be(attacker, "a dona precisa travelsse até a consulta");
    }

    [Fact]
    public void Options_Should_Reject_A_Ceiling_Longer_Than_The_Sliding_Lifetime()
    {
        var options = new SessionLifetimeOptions
        {
            SlidingLifetime = TimeSpan.FromHours(1),
            AbsoluteLifetime = TimeSpan.FromHours(2)
        };

        options.IsValid.Should().BeFalse("teto maior que deslizante é configuração sem sentido");
    }

    [Fact]
    public void Default_Options_Should_Be_Sane()
    {
        var options = new SessionLifetimeOptions();

        options.IsValid.Should().BeTrue();
        options.SlidingLifetime.Should().Be(TimeSpan.FromDays(30));
        options.AbsoluteLifetime.Should().Be(TimeSpan.FromHours(12));
        options.AbsoluteLifetime.Should().BeLessThan(options.SlidingLifetime, "é o teto que dá sentido ao deslizante");
    }

    /// <summary>
    /// O middleware passou a ler o ICacheService (Redis primário, fallback em
    /// memória — ADR-010/ADR-020), não o IMemoryCache local. O fake tem TTL de
    /// verdade porque o teste do CheckInterval depende do expiry, não de um
    /// dicionário eterno.
    /// </summary>
    private sealed class FakeCacheService : ICacheService
    {
        private sealed record Entry(object? Value, DateTime? ExpiresAtUtc);
        private readonly Dictionary<string, Entry> _store = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (!_store.TryGetValue(key, out var entry))
                return Task.FromResult(default(T));

            if (entry.ExpiresAtUtc is not null && entry.ExpiresAtUtc <= DateTime.UtcNow)
            {
                _store.Remove(key);
                return Task.FromResult(default(T));
            }

            return Task.FromResult(entry.Value is T typed ? typed : default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            _store[key] = new Entry(value, expiration is null ? null : DateTime.UtcNow.Add(expiration.Value));
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (!_store.TryGetValue(key, out var entry))
                return Task.FromResult(false);

            if (entry.ExpiresAtUtc is not null && entry.ExpiresAtUtc <= DateTime.UtcNow)
            {
                _store.Remove(key);
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("o middleware não usa lock distribuído.");
        public Task TagAsync(string tag, string key, TimeSpan? duration, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
