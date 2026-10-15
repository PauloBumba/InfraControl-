using FluentAssertions;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Integrations.Unifi;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F26 Fase 2.2: config UniFi persistida — senha cifrada (nunca texto
/// puro), fallback de env, teste de conexão que NÃO lança (erro vira
/// resultado) e validação de URL/credenciais.
/// </summary>
public sealed class UniFiSettingsCommandsTests
{
    private sealed class FakeSettingsRepository : IUniFiSettingsRepository
    {
        public UniFiIntegrationSetting? Setting { get; set; }

        public Task<UniFiIntegrationSetting?> GetAsync(CancellationToken cancellationToken)
            => Task.FromResult(Setting);

        public Task SaveAsync(UniFiIntegrationSetting setting, CancellationToken cancellationToken)
        {
            Setting = setting;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProtector : ICredentialProtector
    {
        public string Protect(string plainText) => $"enc({plainText})";
        public string Unprotect(string protectedText) => protectedText["enc(".Length..^1];
    }

    private sealed class FakeEnvAccessor : IUnifiEnvOptionsAccessor
    {
        public UnifiIntegrationOptions Current { get; init; } = new();
    }

    private sealed class FakeClient : IUnifiApiClient
    {
        public Exception? ThrowOnList { get; init; }
        public int DevicesCount { get; init; } = 2;

        public Task<IReadOnlyList<UnifiDeviceDto>> ListDevicesAsync(
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnList is not null)
                throw ThrowOnList;
            IReadOnlyList<UnifiDeviceDto> list = Enumerable
                .Range(0, DevicesCount)
                .Select(i => new UnifiDeviceDto(
                    $"mac{i}", $"name{i}", "U7PG2", "ap", "10.0.0." + i))
                .ToList();
            return Task.FromResult(list);
        }

        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ThrowOnList is null);

        public Task<IReadOnlyList<UnifiSiteDto>> ListSitesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UnifiSiteDto>>([]);

        public Task<string> GetSystemVersionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("8.4.62");

        public Task SetPortConfigAsync(
            string deviceMac, int portIndex, bool? disabled, int? portVlan,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<UnifiConnectedClientDto>> ListConnectedClientsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UnifiConnectedClientDto>>([]);

        public Task<IReadOnlyList<UnifiNetworkDto>> ListNetworksAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UnifiNetworkDto>>([]);

        public Task<IReadOnlyList<UnifiSwitchPortDto>> ListSwitchPortsAsync(
            string deviceMac, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UnifiSwitchPortDto>>([]);

        public Task<UnifiTopologyDto> GetTopologyAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new UnifiTopologyDto([], []));

        public Task<UnifiPortsSummaryDto> GetPortsSummaryAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new UnifiPortsSummaryDto(0, 0));
        public Task<IReadOnlyList<UnifiClientTrafficDto>> ListClientTrafficAsync(
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnList is not null)
                throw ThrowOnList;
            return Task.FromResult<IReadOnlyList<UnifiClientTrafficDto>>([]);
        }

        public Task<IReadOnlyList<UnifiDpiAppDto>> ListDpiStatsAsync(
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnList is not null)
                throw ThrowOnList;
            return Task.FromResult<IReadOnlyList<UnifiDpiAppDto>>([]);
        }

        public Task<IReadOnlyList<UnifiSsidDto>> ListSsidsAsync(
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnList is not null)
                throw ThrowOnList;
            return Task.FromResult<IReadOnlyList<UnifiSsidDto>>([]);
        }

        public Task<IReadOnlyList<UnifiAccessPointDto>> ListAccessPointsAsync(
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnList is not null)
                throw ThrowOnList;
            return Task.FromResult<IReadOnlyList<UnifiAccessPointDto>>([]);
        }
    }

    private sealed class FakeResolver(UnifiIntegrationOptions options) : IUniFiOptionsResolver
    {
        public Task<UnifiIntegrationOptions> ResolveAsync(CancellationToken cancellationToken)
            => Task.FromResult(options);
    }

    private sealed class FakeClientFactory : IUnifiClientFactory
    {
        public FakeClient Client { get; init; } = new();
        public IUnifiApiClient Create(UnifiIntegrationOptions options) => Client;
    }


    [Theory]
    [InlineData("ftp://controlador.local")]
    [InlineData("javascript:alert(1)")]
    [InlineData("controlador.local")]
    public async Task Save_RejeitaUrl_NaoHttp(string baseUrl)
    {
        var handler = new SaveUniFiSettingsCommandHandler(new FakeSettingsRepository(), new FakeProtector());
        var act = () => handler.Handle(Cmd(baseUrl: baseUrl), CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*http(s)*");
    }

    [Fact]
    public async Task Save_RejeitaUrl_ComCredencialEmbutida()
    {
        var handler = new SaveUniFiSettingsCommandHandler(new FakeSettingsRepository(), new FakeProtector());
        var act = () => handler.Handle(
            Cmd(baseUrl: "https://admin:senha@172.21.0.1:8443"), CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*credenciais embutidas*");
    }

    [Fact]
    public void UnifiSyncWorker_ClampInterval_RespeitaLimitesSeguros()
    {
        InfraControl.Workers.UnifiSyncWorker.ClampInterval(TimeSpan.FromSeconds(1))
            .Should().Be(InfraControl.Workers.UnifiSyncWorker.MinInterval);
        InfraControl.Workers.UnifiSyncWorker.ClampInterval(TimeSpan.FromDays(7))
            .Should().Be(InfraControl.Workers.UnifiSyncWorker.MaxInterval);
        InfraControl.Workers.UnifiSyncWorker.ClampInterval(TimeSpan.FromMinutes(30))
            .Should().Be(TimeSpan.FromMinutes(30));
    }

    private static SaveUniFiSettingsCommand Cmd(
        string? password = "segredo",
        string baseUrl = "https://172.21.0.1:8443",
        string username = "paulo_bumba")
        => new(new SaveUniFiSettingsRequest(
            baseUrl, username, password, "default", true));

    [Fact]
    public async Task Save_cifra_a_senha_e_nunca_retorna_texto_puro()
    {
        var repo = new FakeSettingsRepository();
        var handler = new SaveUniFiSettingsCommandHandler(repo, new FakeProtector());

        var dto = await handler.Handle(Cmd(), CancellationToken.None);

        dto.PasswordSet.Should().BeTrue();
        dto.Source.Should().Be("database");
        repo.Setting!.PasswordEncrypted.Should().Be("enc(segredo)");
        typeof(UniFiSettingsDto).GetProperties()
            .Select(p => p.GetValue(dto))
            .Should().NotContain("segredo");
    }

    [Fact]
    public async Task Save_sem_senha_preserva_a_ja_cifrada()
    {
        var repo = new FakeSettingsRepository
        {
            Setting = UniFiIntegrationSetting.Create(
                "https://172.21.0.1:8443", "paulo_bumba", "enc(velho)",
                "default", true)
        };
        var handler = new SaveUniFiSettingsCommandHandler(repo, new FakeProtector());

        var dto = await handler.Handle(Cmd(password: null), CancellationToken.None);

        dto.PasswordSet.Should().BeTrue();
        repo.Setting.PasswordEncrypted.Should().Be("enc(velho)");
    }

    [Fact]
    public async Task Save_url_invalida_lanca_ArgumentException()
    {
        var handler = new SaveUniFiSettingsCommandHandler(
            new FakeSettingsRepository(), new FakeProtector());

        var act = () => handler.Handle(
            Cmd(baseUrl: "não-é-url"), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*absoluta*");
    }

    [Fact]
    public async Task Save_primeira_config_sem_senha_lanca_ArgumentException()
    {
        var handler = new SaveUniFiSettingsCommandHandler(
            new FakeSettingsRepository(), new FakeProtector());

        var act = () => handler.Handle(Cmd(password: ""), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*senha*obrigatória*");
    }

    [Fact]
    public async Task Get_sem_linha_no_banco_usa_env_com_source_environment()
    {
        var env = new FakeEnvAccessor
        {
            Current = new UnifiIntegrationOptions
            {
                BaseUrl = "https://env:8443",
                Username = "env-user",
                Password = "env-pass",
                Site = "default"
            }
        };
        var handler = new GetUniFiSettingsQueryHandler(new FakeSettingsRepository(), env);

        var dto = await handler.Handle(new GetUniFiSettingsQuery(), CancellationToken.None);

        dto.Source.Should().Be("environment");
        dto.BaseUrl.Should().Be("https://env:8443");
        dto.PasswordSet.Should().BeTrue();
    }

    [Fact]
    public async Task Get_com_linha_retorna_source_database_e_password_set()
    {
        var repo = new FakeSettingsRepository
        {
            Setting = UniFiIntegrationSetting.Create(
                "https://db:8443", "db-user", "enc(db-pass)", "outro", false)
        };
        var handler = new GetUniFiSettingsQueryHandler(repo, new FakeEnvAccessor());

        var dto = await handler.Handle(new GetUniFiSettingsQuery(), CancellationToken.None);

        dto.Source.Should().Be("database");
        dto.Username.Should().Be("db-user");
        dto.Site.Should().Be("outro");
        dto.AllowInvalidTlsCertificate.Should().BeFalse();
        dto.PasswordSet.Should().BeTrue();
        dto.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Test_conexao_ok_devolve_quantidade_de_dispositivos()
    {
        var repo = new FakeSettingsRepository
        {
            Setting = UniFiIntegrationSetting.Create(
                "https://db:8443", "user", "enc(pass)", "default", true)
        };
        var handler = new TestUniFiConnectionCommandHandler(
            new FakeResolver(new UnifiIntegrationOptions
            {
                BaseUrl = "https://db:8443", Username = "user", Password = "pass"
            }),
            new FakeClientFactory(),
            NullLogger<TestUniFiConnectionCommandHandler>.Instance);

        var result = await handler.Handle(
            new TestUniFiConnectionCommand(), CancellationToken.None);

        result.Ok.Should().BeTrue();
        result.DevicesCount.Should().Be(2);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task Test_conexao_falha_devolve_erro_identificavel_sem_lancar()
    {
        var repo = new FakeSettingsRepository
        {
            Setting = UniFiIntegrationSetting.Create(
                "https://db:8443", "user", "enc(pass)", "default", true)
        };
        var factory = new FakeClientFactory
        {
            Client = new FakeClient
            {
                ThrowOnList = new InvalidOperationException(
                    "API de login compatível com o controlador não foi encontrada.")
            }
        };
        var handler = new TestUniFiConnectionCommandHandler(
            new FakeResolver(new UnifiIntegrationOptions
            {
                BaseUrl = "https://db:8443", Username = "user", Password = "pass"
            }),
            factory,
            NullLogger<TestUniFiConnectionCommandHandler>.Instance);

        var result = await handler.Handle(
            new TestUniFiConnectionCommand(), CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.DevicesCount.Should().Be(0);
        result.Error.Should().Contain("API de login compatível");
    }

    [Fact]
    public async Task Test_sem_config_completa_devolve_erro_sem_chamar_o_controlador()
    {
        var factory = new Mock<IUnifiClientFactory>();
        var handler = new TestUniFiConnectionCommandHandler(
            new FakeResolver(new UnifiIntegrationOptions()),
            factory.Object,
            NullLogger<TestUniFiConnectionCommandHandler>.Instance);

        var result = await handler.Handle(
            new TestUniFiConnectionCommand(), CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("incompleta");
        factory.Verify(f => f.Create(It.IsAny<UnifiIntegrationOptions>()), Times.Never);
    }

    [Fact]
    public async Task Resolver_usa_banco_quando_existe_e_decifra_a_senha()
    {
        var repo = new FakeSettingsRepository
        {
            Setting = UniFiIntegrationSetting.Create(
                "https://db:8443/", "db-user", "enc(db-pass)", "site-x", true)
        };
        var resolver = new UniFiOptionsResolver(
            repo, new FakeEnvAccessor { Current = new UnifiIntegrationOptions { BaseUrl = "https://env:8443" } },
            new FakeProtector(),
            NullLogger<UniFiOptionsResolver>.Instance);

        var options = await resolver.ResolveAsync(CancellationToken.None);

        options.BaseUrl.Should().Be("https://db:8443");
        options.Username.Should().Be("db-user");
        options.Password.Should().Be("db-pass");
        options.Site.Should().Be("site-x");
        options.AllowInvalidTlsCertificate.Should().BeTrue();
    }

    [Fact]
    public async Task Resolver_sem_linha_no_banco_usa_env()
    {
        var env = new FakeEnvAccessor
        {
            Current = new UnifiIntegrationOptions
            {
                BaseUrl = "https://env:8443", Username = "u", Password = "p"
            }
        };
        var resolver = new UniFiOptionsResolver(
            new FakeSettingsRepository(), env,
            new FakeProtector(),
            NullLogger<UniFiOptionsResolver>.Instance);

        var options = await resolver.ResolveAsync(CancellationToken.None);

        options.BaseUrl.Should().Be("https://env:8443");
    }

    [Fact]
    public async Task Resolver_com_senha_corrompida_retorna_vazio_sem_lancar()
    {
        var repo = new FakeSettingsRepository
        {
            Setting = UniFiIntegrationSetting.Create(
                "https://db:8443", "user", "corrompida", "default", false)
        };
        var throwing = new ThrowingProtector();
        var resolver = new UniFiOptionsResolver(
            repo, new FakeEnvAccessor(), throwing,
            NullLogger<UniFiOptionsResolver>.Instance);

        var options = await resolver.ResolveAsync(CancellationToken.None);

        options.Password.Should().BeEmpty();
        options.Username.Should().Be("user");
    }

    private sealed class ThrowingProtector : ICredentialProtector
    {
        public string Protect(string plainText) => throw new NotSupportedException();
        public string Unprotect(string protectedText) =>
            throw new InvalidOperationException("chave divergente");
    }
}
