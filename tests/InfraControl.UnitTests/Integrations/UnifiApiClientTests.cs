using System.Net;
using System.Text;
using FluentAssertions;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Infrastructure.Integrations.Unifi;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F20-T001/ADR-028 §2: cliente REST do UniFi Controller — login
/// POST /api/auth/login com cookie de sessão unifises, re-autenticação
/// automática em 401 (uma repetição) e listagem de dispositivos
/// (somente cadastro — sem telemetria persistida, regra 8).
/// </summary>
public sealed class UnifiApiClientTests
{

    // ── F26 Fase 3: sites e versão ──────────────────────────────────

    [Fact]
    public async Task ListSitesAsync_ShouldParseSelfSites()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-1; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/api/self/sites"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"short_id":"abc123","name":"default","desc":"Site principal","attr_hidden":false},
                  {"short_id":"xyz789","name":"super","desc":"","attr_hidden":true}
                ]}
                """));

        var client = CreateClient(handler);

        var sites = await client.ListSitesAsync();

        sites.Should().HaveCount(2);
        sites[0].ShortId.Should().Be("abc123");
        sites[0].Name.Should().Be("default");
        sites[0].Hidden.Should().BeFalse();
        sites[1].ShortId.Should().Be("xyz789");
        sites[1].Hidden.Should().BeTrue();
        handler.Requests[1].RequestUri!.ToString().Should().Contain("/api/self/sites");
    }

    [Fact]
    public async Task GetSystemVersionAsync_ShouldReadVersionFromSysinfo()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-1; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/stat/sysinfo"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"uptime":123,"version":"8.4.62","data":[]}
                """));

        var client = CreateClient(handler);

        var version = await client.GetSystemVersionAsync();

        version.Should().Be("8.4.62");
    }

    [Fact]
    public async Task ListDevicesAsync_ShouldParseSiteStateAndFirmware()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-1; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/stat/device"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"mac":"aa:bb:cc:dd:ee:01","name":"ap-01","model":"U7PG2","type":"uap",
                   "ip":"10.0.0.5","site_id":"abc123","state":"ONLINE","version":"6.6.55"}
                ]}
                """));

        var client = CreateClient(handler);

        var devices = await client.ListDevicesAsync();

        devices[0].SiteId.Should().Be("abc123");
        devices[0].State.Should().Be("ONLINE");
        devices[0].Firmware.Should().Be("6.6.55");
    }


    // ── F26 Fase 4: clientes ao vivo e redes ────────────────────────

    [Fact]
    public async Task ListConnectedClientsAsync_ShouldParseStatSta()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-1; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/stat/sta"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"mac":"f4:92:bf:00:11:22","ip":"172.16.10.55","hostname":"iphone",
                   "ap_mac":"74:ac:b9:11:22:33","essid":"Corp-WiFi","signal":-52,
                   "radio":"ng","is_wired":false,"last_seen":1759900000},
                  {"mac":"f4:92:bf:00:44:55","ip":"172.16.10.56","hostname":"pc-023",
                   "sw_mac":"74:ac:b9:44:55:66","is_wired":true,"last_seen":1759900100}
                ]}
                """));

        var client = CreateClient(handler);

        var clients = await client.ListConnectedClientsAsync();

        clients.Should().HaveCount(2);
        clients[0].Mac.Should().Be("f4:92:bf:00:11:22");
        clients[0].ApMac.Should().Be("74:ac:b9:11:22:33");
        clients[0].Essid.Should().Be("Corp-WiFi");
        clients[0].Signal.Should().Be(-52);
        clients[0].IsWired.Should().BeFalse();
        // S1b: rádio bruto do controller ("ng" = 2.4 GHz) passa adiante;
        // cliente com-fio não tem rádio (null honesto, nunca 0/"").
        clients[0].Radio.Should().Be("ng");
        clients[1].IsWired.Should().BeTrue();
        clients[1].SwitchMac.Should().Be("74:ac:b9:44:55:66");
        clients[1].Radio.Should().BeNull();
    }

    [Fact]
    public async Task ListNetworksAsync_ShouldParseNetworkConf()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-1; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/rest/networkconf"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"_id":"n1","name":"Corp","purpose":"corporate","vlan":10,"enabled":true,"site_id":"abc123"},
                  {"_id":"n2","name":"IoT","purpose":"vlan-only","vlan":30,"enabled":false,"site_id":"abc123"}
                ]}
                """));

        var client = CreateClient(handler);

        var networks = await client.ListNetworksAsync();

        networks.Should().HaveCount(2);
        networks[0].VlanId.Should().Be(10);
        networks[0].Enabled.Should().BeTrue();
        networks[1].Purpose.Should().Be("vlan-only");
        networks[1].Enabled.Should().BeFalse();
    }


    [Fact]
    public async Task ListSwitchPortsAsync_ShouldParsePortTableOfTheDevice()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-1; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/stat/device"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"mac":"74:ac:b9:00:00:01","name":"sw-01","model":"USW-24-POE","type":"usw","ip":"10.0.0.2",
                   "port_table":[
                     {"port_idx":1,"name":"Port 1","up":true,"speed":"1000","full_duplex":true,
                      "port_vlan":10,"poe_enable":true,"poe_power":"RUNNING","media":"1000BASE-T",
                      "port_conf_enabled":true},
                     {"port_idx":2,"name":"Port 2","up":false,"speed":"","port_vlan":10,"poe_enable":false,
                      "port_conf_enabled":false}
                   ]},
                  {"mac":"74:ac:b9:00:00:02","name":"ap-01","model":"U7PG2","type":"uap","ip":"10.0.0.3","port_table":[]}
                ]}
                """));

        var client = CreateClient(handler);

        var ports = await client.ListSwitchPortsAsync("74:AC:B9:00:00:01");

        ports.Should().HaveCount(2);
        ports[0].PortIndex.Should().Be(1);
        ports[0].LinkUp.Should().BeTrue();
        ports[0].Speed.Should().Be("1000");
        ports[0].FullDuplex.Should().BeTrue();
        ports[0].PortVlan.Should().Be(10);
        ports[0].PoeStatus.Should().Be("RUNNING");
        // ADR-059 §3 (H6): estado ADMINISTRATIVO vem do controller — é ele
        // que dirige o botão Habilitar/Desabilitar, não o link do cabo.
        ports[0].Enabled.Should().BeTrue();
        ports[1].LinkUp.Should().BeFalse();
        ports[1].Enabled.Should().BeFalse();
        // F30-T001 S4a: a Port 2 não traz "full_duplex" no JSON — ausência é
        // null, nunca false (que viraria meio-duplex inventado na persistência).
        ports[1].FullDuplex.Should().BeNull();
        ports[1].Speed.Should().BeNull("controller reportou \"\"");
    }

    [Fact]
    public async Task ListSwitchPortsAsync_MacDesconhecido_LancaKeyNotFound()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-1; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/stat/device"),
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"},"data":[]}"""));

        var client = CreateClient(handler);

        var act = () => client.ListSwitchPortsAsync("74:ac:b9:00:00:99");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }


    [Fact]
    public async Task GetTopologyAsync_PayloadReal_DoControlador_BuildsNodesAndUplink()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-1; Path=/"));
        // payload capturado do controlador real (2026-10-09) — 1x UAP + uplink
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/stat/device"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"name":"AC Pro","type":"uap","model":"U7PG2","mac":"d0:21:f9:3c:59:50",
                   "state":1,
                   "uplink":{"uplink_mac":"5a:04:4f:bf:7c:f0","port_idx":1,"type":"wire"},
                   "lldp_table":null,"downlink_table":null,"uplink_table":[],
                   "port_table":[{"port_idx":1,"name":"Port 1","op_mode":"switch","enable":true}]}
                ]}
                """));

        var client = CreateClient(handler);

        var topo = await client.GetTopologyAsync();

        topo.Nodes.Should().HaveCount(2);
        topo.Nodes.Single(n => n.Mac == "d021f93c5950").Kind.Should().Be("ap");
        var unknown = topo.Nodes.Single(n => !n.IsKnownDevice);
        unknown.Mac.Should().Be("5a044fbf7cf0");
        unknown.Kind.Should().Be("external");

        var link = topo.Links.Should().ContainSingle().Subject;
        link.SourceMac.Should().Be("d021f93c5950");
        link.TargetMac.Should().Be("5a044fbf7cf0");
        link.SourcePort.Should().Be(1);
        link.LinkType.Should().Be("wire");
        link.Confidence.Should().Be("unknown");
        link.Source.Should().Be("controller");
    }

    [Fact]
    public async Task GetTopologyAsync_SwitchApELldp_MarcaConfiancaCorreta()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-1; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/stat/device"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"name":"gw","type":"ugw","model":"UDM","mac":"aa:bb:cc:00:00:01","state":1,"port_table":[]},
                  {"name":"sw-01","type":"usw","model":"USW-24-POE","mac":"aa:bb:cc:00:00:02","state":1,
                   "uplink":{"uplink_mac":"aa:bb:cc:00:00:01","port_idx":8,"type":"wire"},
                   "lldp_table":[{"port_idx":3,"chassis_id":"aa:bb:cc:00:00:03","sys_name":"sw-02"}],
                   "port_table":[]},
                  {"name":"ap-01","type":"uap","model":"U7PG2","mac":"aa:bb:cc:00:00:03","state":1,
                   "uplink":{"uplink_mac":"aa:bb:cc:00:00:02","port_idx":5,"type":"wire"},
                   "port_table":[]}
                ]}
                """));

        var client = CreateClient(handler);

        var topo = await client.GetTopologyAsync();

        topo.Nodes.Should().HaveCount(3);
        topo.Nodes.Single(n => n.Mac == "aabbcc000001").Kind.Should().Be("gateway");
        topo.Nodes.Single(n => n.Mac == "aabbcc000002").Kind.Should().Be("switch");

        var swUplink = topo.Links.Single(l => l.SourceMac == "aabbcc000002" && l.Source == "controller");
        swUplink.Confidence.Should().Be("confirmed");
        swUplink.TargetMac.Should().Be("aabbcc000001");

        var apUplink = topo.Links.Single(l => l.SourceMac == "aabbcc000003" && l.Source == "controller");
        apUplink.Confidence.Should().Be("confirmed");
        apUplink.SourcePort.Should().Be(5);

        var lldp = topo.Links.Single(l => l.Source == "lldp");
        lldp.Confidence.Should().Be("inferred");
        lldp.TargetName.Should().Be("sw-02");
    }

    // ── Login e sessão ──────────────────────────────────────────────

    [Fact]
    public async Task ListDevicesAsync_ShouldLoginAndCallStatDeviceWithSessionCookie()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-123; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/stat/device"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"mac":"aa:bb:cc:dd:ee:01","name":"gw-01","model":"UDM-Pro","type":"udm","ip":"192.168.1.1"},
                  {"mac":"aa:bb:cc:dd:ee:02","name":"sw-01","model":"USW-24-POE","type":"usw","ip":"192.168.1.2"}
                ]}
                """));

        var client = CreateClient(handler);

        var devices = await client.ListDevicesAsync();

        devices.Should().HaveCount(2);
        devices[0].Mac.Should().Be("aa:bb:cc:dd:ee:01");
        devices[0].Name.Should().Be("gw-01");
        devices[0].Model.Should().Be("UDM-Pro");
        devices[0].Type.Should().Be("udm");
        devices[0].Ip.Should().Be("192.168.1.1");

        handler.Requests.Should().HaveCount(2, "login + stat/device");
        handler.Requests[1].Headers.GetValues("Cookie")
            .Single().Should().Contain("unifises=sess-123");
        handler.Requests[1].RequestUri!.ToString()
            .Should().Contain("/proxy/network/api/s/default/stat/device");
    }

    [Fact]
    public async Task ListDevicesAsync_OnUnauthorized_ShouldReauthenticateOnceAndRetry()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-1; Path=/"),
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-2; Path=/"));
        var statCalls = 0;
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/stat/device"), _ =>
        {
            statCalls++;
            return statCalls == 1
                ? Response(HttpStatusCode.Unauthorized)
                : Response(HttpStatusCode.OK,
                    """{"meta":{"rc":"ok"},"data":[{"mac":"aa:bb:cc:dd:ee:03","name":"ap-01","model":"U6","type":"uap","ip":"192.168.1.3"}]}""");
        });

        var client = CreateClient(handler);

        var devices = await client.ListDevicesAsync();

        devices.Should().HaveCount(1);
        statCalls.Should().Be(2, "o 401 deve custar exatamente uma repetição");
        // login inicial + re-login do 401
        handler.Requests.Count(r => r.RequestUri!.AbsolutePath == "/api/auth/login")
            .Should().Be(2);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldThrowPtBr()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.Unauthorized));

        var client = CreateClient(handler);

        var send = () => client.ListDevicesAsync();

        (await send.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*Credenciais*UniFi*");
    }

    // ── Fase 1 (F26-T001): controlador standalone (docs/35 §3) ─────

    [Fact]
    public async Task ListDevicesAsync_OnStandaloneController_ShouldFallbackToApiLoginAndCallRootPath()
    {
        var handler = new ScriptedHandler();
        // standalone: /api/auth/login responde 401 mesmo com credenciais válidas
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.Unauthorized));
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-standalone; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/s/default/stat/device",
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"mac":"aa:bb:cc:dd:ee:09","name":"ap-lab","model":"U7PG2","type":"uap","ip":"192.168.1.10"}
                ]}
                """));

        var client = CreateClient(handler);

        var devices = await client.ListDevicesAsync();

        devices.Should().HaveCount(1);
        devices[0].Model.Should().Be("U7PG2");
        handler.Requests.Should().HaveCount(3, "auth/login + api/login + stat/device");
        handler.Requests[2].RequestUri!.AbsolutePath.Should().Be("/api/s/default/stat/device",
            "standalone não usa o prefixo /proxy/network");
        handler.Requests[2].Headers.GetValues("Cookie")
            .Single().Should().Contain("unifises=sess-standalone");
    }

    [Fact]
    public async Task ListDevicesAsync_OnStandalone_ShouldReauthenticateViaApiLoginOn401()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.Unauthorized));
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=s1; Path=/"),
                 _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=s2; Path=/"));
        var statCalls = 0;
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/s/default/stat/device", _ =>
        {
            statCalls++;
            return statCalls == 1
                ? Response(HttpStatusCode.Unauthorized)
                : Response(HttpStatusCode.OK,
                    """{"meta":{"rc":"ok"},"data":[{"mac":"aa:bb:cc:dd:ee:10","name":"sw-lab","model":"USW-8","type":"usw","ip":"192.168.1.11"}]}""");
        });

        var client = CreateClient(handler);

        var devices = await client.ListDevicesAsync();

        devices.Should().HaveCount(1);
        statCalls.Should().Be(2);
        handler.Requests.Count(r => r.RequestUri!.AbsolutePath == "/api/login")
            .Should().Be(2, "o estilo standalone detectado é reusado na re-autenticação");
        handler.Requests.Should().NotContain(r =>
            r.RequestUri!.AbsolutePath.StartsWith("/proxy/network"),
            "nunca usar o prefixo UniFi OS em controlador standalone");
    }

    [Fact]
    public async Task Login_WhenBothLoginEndpointsReject_ShouldThrowPtBrCredentials()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.Unauthorized));
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/login",
            _ => Response(HttpStatusCode.Unauthorized));

        var client = CreateClient(handler);

        var send = () => client.ListDevicesAsync();

        (await send.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*Credenciais*UniFi*");
    }

    [Fact]
    public async Task Login_WhenNoCompatibleLoginEndpointExists_ShouldThrowInvalidOperationPtBr()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath is "/api/auth/login" or "/api/login",
            _ => Response(HttpStatusCode.NotFound));

        var client = CreateClient(handler);

        var send = () => client.ListDevicesAsync();

        (await send.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*API de login*compatível*");
    }

    [Fact]
    public async Task ListDevicesAsync_WhenNotConfigured_ShouldThrowPtBr()
    {
        var handler = new ScriptedHandler();
        var client = CreateClient(handler, baseUrl: string.Empty);

        var send = () => client.ListDevicesAsync();

        (await send.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Integração UniFi não configurada*");
        handler.Requests.Should().BeEmpty("sem configuração nada é chamado");
    }

    // ── Health (ADR-028 §5) ─────────────────────────────────────────

    [Fact]
    public async Task IsHealthyAsync_WhenSelfEndpointOk_ShouldReturnTrue()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=s; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/self",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}"""));

        var client = CreateClient(handler);

        (await client.IsHealthyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task IsHealthyAsync_WhenControllerDown_ShouldReturnFalse()
    {
        var handler = new ScriptedHandler();
        handler.When(r => true, _ => throw new HttpRequestException("controller fora do ar"));

        var client = CreateClient(handler);

        (await client.IsHealthyAsync()).Should().BeFalse();
    }

    // ── F18-T007 (Insights): tráfego e DPI ──────────────────────────

    [Fact]
    public async Task ListClientTrafficAsync_ShouldCallStatStaAndParseCounters()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-9; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/stat/sta"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"mac":"aa:bb:cc:dd:ee:20","hostname":"notebook-paulo","ip":"192.168.1.50",
                   "rx_bytes":1048576,"tx_bytes":524288,"last_seen":1759900000},
                  {"mac":"aa:bb:cc:dd:ee:21","hostname":"","ip":"192.168.1.51",
                   "rx_bytes":0,"tx_bytes":0}
                ]}
                """));

        var client = CreateClient(handler);

        var traffic = await client.ListClientTrafficAsync();

        traffic.Should().HaveCount(2);
        traffic[0].Hostname.Should().Be("notebook-paulo");
        traffic[0].Ip.Should().Be("192.168.1.50");
        traffic[0].RxBytes.Should().Be(1048576);
        traffic[0].TxBytes.Should().Be(524288);
        traffic[0].TotalBytes.Should().Be(1048576 + 524288);
        traffic[0].LastSeen.Should().Be(
            DateTimeOffset.FromUnixTimeSeconds(1759900000).UtcDateTime,
            "last_seen vem em epochs seconds");
        traffic[1].LastSeen.Should().BeNull("sem last_seen no Controller = null");

        handler.Requests.Should().HaveCount(2, "login + stat/sta");
        handler.Requests[1].RequestUri!.ToString()
            .Should().Contain("/proxy/network/api/s/default/stat/sta");
    }

    [Fact]
    public async Task ListDpiStatsAsync_ShouldParseStatAppWithCategoryFallbacks()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-9; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/stat/app"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"app":"Netflix","cat":"streaming","value":9000},
                  {"app":"Microsoft Teams","category":"collaboration","s":600,"r":400},
                  {"app":"","cat":"ignored","value":12345}
                ]}
                """));

        var client = CreateClient(handler);

        var apps = await client.ListDpiStatsAsync();

        apps.Should().HaveCount(2, "linha sem nome de aplicação é descartada");
        apps[0].App.Should().Be("Netflix");
        apps[0].Category.Should().Be("streaming");
        apps[0].TotalBytes.Should().Be(9000);
        apps[1].Category.Should().Be("collaboration", "fallback de 'category' quando não há 'cat'");
        apps[1].TotalBytes.Should().Be(1000, "sem 'value' os bytes somam s + r");

        handler.Requests[1].RequestUri!.ToString()
            .Should().Contain("/proxy/network/api/s/default/stat/app");
    }

    // ── F18-T006 (WiFi): redes SSID e telemetria de AP ──────────────

    [Fact]
    public async Task ListSsidsAsync_ShouldCallRestSsidconfAndParseConfig()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-9; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/rest/ssidconf"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"_id":"a","name":"Casa-5G","security":"wpapsk","enabled":true,
                   "vlan":40,"is_guest":false,"no2ghz":true,"no5ghz":false},
                  {"_id":"b","name":"Convidados","security":"none","enabled":false,"is_guest":true},
                  {"_id":"c","name":"","security":"wpapsk","enabled":true}
                ]}
                """));

        var client = CreateClient(handler);

        var ssids = await client.ListSsidsAsync();

        ssids.Should().HaveCount(2, "rede sem nome é descartada");
        ssids[0].Name.Should().Be("Casa-5G");
        ssids[0].Security.Should().Be("wpapsk", "segurança vem crua do Controller");
        ssids[0].Enabled.Should().BeTrue();
        ssids[0].Vlan.Should().Be(40);
        ssids[0].Bands.Should().Be(
            "5 GHz", "no2ghz=true desliga 2.4 GHz e no5ghz=false mantém 5 GHz");
        ssids[0].IsGuest.Should().BeFalse();
        ssids[1].Name.Should().Be("Convidados");
        ssids[1].Vlan.Should().Be(0, "sem vlan = 0 (sem VLAN)");
        ssids[1].Bands.Should().BeEmpty(
            "sem os flags de banda o Controller não informou — nada é suposto");
        ssids[1].IsGuest.Should().BeTrue();

        handler.Requests.Should().HaveCount(2, "login + ssidconf");
        handler.Requests[1].RequestUri!.ToString()
            .Should().Contain("/proxy/network/api/s/default/rest/ssidconf");
    }

    [Fact]
    public async Task ListAccessPointsAsync_ShouldParseTelemetryFromStatDevice()
    {
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-9; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/stat/device"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"mac":"f0:9f:c2:11:22:33","name":"AP-Sala","model":"U7PG2","ip":"192.168.1.11",
                   "type":"uap","state":1,"uptime":86400,
                   "system-stats":{"cpu":"7.5","mem":"41"},
                   "rx_bytes":9000000,"tx_bytes":4500000,
                   "vap_table":[{"num_sta":4},{"num_sta":2}],
                   "radio_table":[
                     {"name":"radio0","band":"2g","channel":6,"width":20,"tx_power":17,"utilization":12},
                     {"name":"radio1","band":"5g","channel":44,"width":80,"tx_power":23,"utilization":35}
                   ]},
                  {"mac":"f0:9f:c2:44:55:66","name":"AP-Fora","model":"U6Lite","ip":"192.168.1.12",
                   "type":"uap","state":0,"rx_bytes":0,"tx_bytes":0}
                ]}
                """));

        var client = CreateClient(handler);

        var aps = await client.ListAccessPointsAsync();

        aps.Should().HaveCount(2);
        aps[0].Connected.Should().BeTrue("state=1 = conectado");
        aps[0].UptimeSeconds.Should().Be(86400);
        aps[0].CpuPercent.Should().Be(8, "'7.5' em string arredonda para longe de zero");
        aps[0].MemPercent.Should().Be(41);
        aps[0].RxBytes.Should().Be(9000000);
        aps[0].TxBytes.Should().Be(4500000);
        aps[0].Clients.Should().Be(6, "clientes = soma dos num_sta da vap_table");
        aps[0].Radios.Should().HaveCount(2);
        aps[0].Radios[1].Band.Should().Be("5g");
        aps[0].Radios[1].Channel.Should().Be(44);
        aps[0].Radios[1].Width.Should().Be(80);
        aps[0].Radios[1].TxPower.Should().Be(23);
        aps[0].Radios[1].Utilization.Should().Be(35);
        aps[1].Connected.Should().BeFalse("state=0 = desconectado");
        aps[1].CpuPercent.Should().Be(0, "sem system-stats = 0, nunca inventado");
        aps[1].Clients.Should().Be(0, "sem vap_table = 0");
        aps[1].Radios.Should().BeEmpty("sem radio_table = lista vazia");

        handler.Requests.Should().HaveCount(2, "login + stat/device");
        handler.Requests[1].RequestUri!.ToString()
            .Should().Contain("/proxy/network/api/s/default/stat/device");
    }

    // ── Infra ───────────────────────────────────────────────────────

    private static UnifiApiClient CreateClient(
        HttpMessageHandler handler, string baseUrl = "https://unifi.exemplo.test")
    {
        var factory = new SingleClientFactory(handler);
        var options = Options.Create(new UnifiIntegrationOptions
        {
            BaseUrl = baseUrl,
            Username = "admin",
            Password = "senha-dev"
        });

        return new UnifiApiClient(
            factory, options, NullLogger<UnifiApiClient>.Instance);
    }

    private static HttpResponseMessage Response(
        HttpStatusCode statusCode, string? json = null, string? setCookie = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (json is not null)
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
        if (setCookie is not null)
            response.Headers.TryAddWithoutValidation("Set-Cookie", setCookie);
        return response;
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(handler, disposeHandler: false);
    }

    [Fact]
    public async Task ListSitesAsync_ShouldFallbackToName_WhenControllerOmitsShortId()
    {
        // UniFi Network 10.4.57 (unifi.infovda.com.br) não devolve short_id
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-1; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/api/self/sites"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"_id":"580fa89533f7079a0fed3851","name":"ta262mxg","desc":"HSDS"}
                ]}
                """));

        var client = CreateClient(handler);

        var sites = await client.ListSitesAsync();

        sites.Should().HaveCount(1);
        sites[0].ShortId.Should().Be("ta262mxg", "sem short_id o sync não persiste nenhum site");
        sites[0].Name.Should().Be("ta262mxg");
        sites[0].Description.Should().Be("HSDS");
    }

    [Fact]
    public async Task ListNetworksAsync_ShouldParseNetworkConf_WhenVlanIsString()
    {
        // controlador real devolve "vlan":"21" (string) — TryGetInt32 lança em String
        var handler = new ScriptedHandler();
        handler.When(r => r.RequestUri!.AbsolutePath == "/api/auth/login",
            _ => Response(HttpStatusCode.OK, """{"meta":{"rc":"ok"}}""", "unifises=sess-1; Path=/"));
        handler.When(r => r.RequestUri!.AbsolutePath.Contains("/rest/networkconf"),
            _ => Response(HttpStatusCode.OK, """
                {"meta":{"rc":"ok"},"data":[
                  {"_id":"n1","name":"Telefonia","purpose":"vlan-only","vlan":"21","enabled":true}
                ]}
                """));

        var client = CreateClient(handler);

        var networks = await client.ListNetworksAsync();

        networks.Should().HaveCount(1);
        networks[0].VlanId.Should().Be(21);
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly List<(Func<HttpRequestMessage, bool> Match,
            Func<HttpRequestMessage, HttpResponseMessage> Respond)> _routes = [];

        public List<HttpRequestMessage> Requests { get; } = [];

        public void When(
            Func<HttpRequestMessage, bool> match,
            Func<HttpRequestMessage, HttpResponseMessage> respond)
            => _routes.Add((match, respond));

        public void When(
            Func<HttpRequestMessage, bool> match,
            params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
        {
            var index = 0;
            _routes.Add((match, request =>
                responses[Math.Min(index++, responses.Length - 1)](request)));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            foreach (var (match, respond) in _routes)
            {
                if (match(request))
                    return Task.FromResult(respond(request));
            }

            return Task.FromResult(Response(HttpStatusCode.NotFound));
        }
    }
}
