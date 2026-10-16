using System.Net;
using FluentAssertions;
using InfraControl.Infrastructure.Monitoring;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Monitoring;

/// <summary>
/// F30-T005a: OnvifSoapClient — SOAP 1.2/WS-Addressing sobre POST em
/// /onvif/device_service (docs/46 §C, sem NuGet: XDocument nativo). Seam
/// testável via HttpMessageHandler injectable — nenhuma rede real.
/// </summary>
public sealed class OnvifSoapClientTests
{
    private const string DeviceServicePath = "/onvif/device_service";

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static HttpResponseMessage Soap(string innerBody, HttpStatusCode status = HttpStatusCode.OK)
        => new(status)
        {
            // Toda resposta SOAP passa pelo envelope (prefixos declarados).
            Content = new StringContent(
                Envelope(innerBody), System.Text.Encoding.UTF8, "application/soap+xml")
        };

    private static string Envelope(string innerBody)
        => "<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\" "
            + "xmlns:tds=\"http://www.onvif.org/ver10/device/wsdl\">"
            + $"<s:Body>{innerBody}</s:Body></s:Envelope>";

    private static OnvifSoapClient Client(StubHandler handler, string? user = null, string? pass = null)
        => new(Options.Create(new OnvifOptions
        {
            Username = user ?? "",
            Password = pass ?? ""
        }), handler);

    // ── GetStorage ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetStorage_ShouldParseTotalsRetentionAndPostSoapAction()
    {
        var handler = new StubHandler(_ => Soap(
            "<tds:GetStorageResponse>"
            + "<tds:Storage>"
            + "<tds:Total>107374182400</tds:Total>"
            + "<tds:Used>85899345920</tds:Used>"
            + "<tds:Free>21474836480</tds:Free>"
            + "<tds:RetentionDays>45</tds:RetentionDays>"
            + "</tds:Storage></tds:GetStorageResponse>"));

        var info = await Client(handler).GetStorageAsync("192.0.2.50", TimeSpan.FromSeconds(3));

        info.Should().NotBeNull();
        info!.TotalBytes.Should().Be(107_374_182_400);
        info.FreeBytes.Should().Be(21_474_836_480);
        info.RetentionDays.Should().Be(45);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(DeviceServicePath);
        handler.LastRequest.Content!.Headers.ContentType!.MediaType.Should()
            .Be("application/soap+xml");
        handler.LastRequestBody.Should().Contain("GetStorage")
            .And.Contain("http://www.onvif.org/ver10/device/wsdl/GetStorage");
    }

    [Fact]
    public async Task GetStorage_ShouldDeriveUsedFromFreeWhenUsedAbsent()
    {
        var handler = new StubHandler(_ => Soap(
            "<r:GetStorageResponse xmlns:r=\"http://www.onvif.org/ver10/storage/wsdl\">"
            + "<r:Storage><r:Total>5368709120</r:Total><r:Free>2684354560</r:Free>"
            + "</r:Storage></r:GetStorageResponse>"));

        var info = await Client(handler).GetStorageAsync("192.0.2.51", TimeSpan.FromSeconds(3));

        info.Should().NotBeNull();
        info!.TotalBytes.Should().Be(5_368_709_120);
        info.FreeBytes.Should().Be(2_684_354_560);
    }

    [Fact]
    public async Task GetStorage_WithoutUsedAndFree_ShouldReturnNullAsUnsupported()
    {
        var handler = new StubHandler(_ => Soap(
            "<tds:GetStorageResponse><tds:Storage>"
            + "<tds:Total>107374182400</tds:Total>"
            + "</tds:Storage></tds:GetStorageResponse>"));

        var info = await Client(handler).GetStorageAsync("192.0.2.52", TimeSpan.FromSeconds(3));

        info.Should().BeNull("sem Used/Free o check não tem como calcular %");
    }

    [Fact]
    public async Task GetStorage_SoapFault_ShouldReturnNullAsUnsupported()
    {
        var handler = new StubHandler(_ => Soap(
            "<s:Fault><s:Code><s:Value>s:Sender</s:Value></s:Code>"
            + "<s:Reason><s:Text>Action not supported</s:Text></s:Reason></s:Fault>"));

        var info = await Client(handler).GetStorageAsync("192.0.2.53", TimeSpan.FromSeconds(3));

        info.Should().BeNull();
    }

    [Fact]
    public async Task GetStorage_HttpError_ShouldThrow()
    {
        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("boom")
            });
        var client = Client(handler);

        var action = () => client.GetStorageAsync("192.0.2.54", TimeSpan.FromSeconds(3));

        (await action.Should().ThrowAsync<Exception>())
            .WithMessage("*500*");
    }

    [Fact]
    public async Task GetStorage_GarbageXml_ShouldThrow()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            // XML malformado de propósito (HTML truncado) — parse deve falhar.
            Content = new StringContent("<html><broken",
                System.Text.Encoding.UTF8, "text/html")
        });
        var client = Client(handler);

        var action = () => client.GetStorageAsync("192.0.2.55", TimeSpan.FromSeconds(3));

        await action.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetStorage_ShouldBracketIpv6Target()
    {
        var handler = new StubHandler(_ => Soap(
            "<tds:GetStorageResponse><tds:Storage>"
            + "<tds:Total>1048576</tds:Total><tds:Used>524288</tds:Used>"
            + "</tds:Storage></tds:GetStorageResponse>"));

        await Client(handler).GetStorageAsync("2001:db8::10", TimeSpan.FromSeconds(3));

        handler.LastRequest!.RequestUri!.Host.Should().Be("[2001:db8::10]");
        handler.LastRequest.RequestUri.HostNameType.ToString().Should().Be("IPv6");
    }

    // ── GetDeviceInformation ─────────────────────────────────────────────

    [Fact]
    public async Task GetDeviceInformation_ShouldParseManufacturerModelFirmware()
    {
        var handler = new StubHandler(_ => Soap(
            "<tds:GetDeviceInformationResponse>"
            + "<tds:Manufacturer>Dahua</tds:Manufacturer>"
            + "<tds:Model>IPC-HFW2441T</tds:Model>"
            + "<tds:FWVersion>2.800.1000000.0</tds:FWVersion>"
            + "</tds:GetDeviceInformationResponse>"));

        var info = await Client(handler)
            .GetDeviceInformationAsync("192.0.2.56", TimeSpan.FromSeconds(3));

        info.Should().NotBeNull();
        info!.Manufacturer.Should().Be("Dahua");
        info.Model.Should().Be("IPC-HFW2441T");
        info.FirmwareVersion.Should().Be("2.800.1000000.0");

        handler.LastRequestBody.Should().Contain("GetDeviceInformation");
    }

    [Fact]
    public async Task GetDeviceInformation_SoapFault_ShouldReturnNull()
    {
        var handler = new StubHandler(_ => Soap(
            "<s:Fault><s:Reason><s:Text>nope</s:Text></s:Reason></s:Fault>"));

        var info = await Client(handler)
            .GetDeviceInformationAsync("192.0.2.57", TimeSpan.FromSeconds(3));

        info.Should().BeNull();
    }

    // ── GetRecordingStatus (F30-T005b) ───────────────────────────────────

    [Fact]
    public async Task GetRecordingStatus_ShouldParseThreeStatesAndPostAction()
    {
        var handler = new StubHandler(_ => Soap(
            "<tds:GetRecordingStatusResponse>"
            + "<tds:Recording>true</tds:Recording>"
            + "<tds:VideoLoss>false</tds:VideoLoss>"
            + "<tds:Tamper>1</tds:Tamper>"
            + "</tds:GetRecordingStatusResponse>"));

        var status = await Client(handler)
            .GetRecordingStatusAsync("192.0.2.65", TimeSpan.FromSeconds(3));

        status.Should().NotBeNull();
        status!.RecordingActive.Should().BeTrue();
        status.VideoLoss.Should().BeFalse();
        status.Tamper.Should().BeTrue("1 também é true");

        handler.LastRequestBody.Should().Contain("GetRecordingStatus")
            .And.Contain("http://www.onvif.org/ver10/device/wsdl/GetRecordingStatus");
    }

    [Fact]
    public async Task GetRecordingStatus_StateTexts_ShouldMapActiveAndStopped()
    {
        var handler = new StubHandler(_ => Soap(
            "<tds:GetRecordingStatusResponse>"
            + "<tds:RecordingStatus>Active</tds:RecordingStatus>"
            + "</tds:GetRecordingStatusResponse>"));

        var status = await Client(handler)
            .GetRecordingStatusAsync("192.0.2.66", TimeSpan.FromSeconds(3));

        status.Should().NotBeNull();
        status!.RecordingActive.Should().BeTrue(
            "RecordingStatus=Active mapeia para true");
        status.VideoLoss.Should().BeNull("elemento ausente = null, não false");
        status.Tamper.Should().BeNull();
    }

    [Fact]
    public async Task GetRecordingStatus_StoppedState_ShouldMapFalse()
    {
        var handler = new StubHandler(_ => Soap(
            "<tds:GetRecordingStatusResponse>"
            + "<tds:State>Stopped</tds:State>"
            + "</tds:GetRecordingStatusResponse>"));

        var status = await Client(handler)
            .GetRecordingStatusAsync("192.0.2.69", TimeSpan.FromSeconds(3));

        status.Should().NotBeNull();
        status!.RecordingActive.Should().BeFalse(
            "State=Stopped cai nos candidatos de gravação");
        status.VideoLoss.Should().BeNull();
    }

    [Fact]
    public async Task GetRecordingStatus_MissingAllFields_ShouldReturnNull()
    {
        var handler = new StubHandler(_ => Soap(
            "<tds:GetRecordingStatusResponse><tds:SomethingElse>9</tds:SomethingElse>"
            + "</tds:GetRecordingStatusResponse>"));

        var status = await Client(handler)
            .GetRecordingStatusAsync("192.0.2.67", TimeSpan.FromSeconds(3));

        status.Should().BeNull();
    }

    [Fact]
    public async Task GetRecordingStatus_SoapFault_ShouldReturnNull()
    {
        var handler = new StubHandler(_ => Soap(
            "<s:Fault><s:Reason><s:Text>not supported</s:Text></s:Reason></s:Fault>"));

        var status = await Client(handler)
            .GetRecordingStatusAsync("192.0.2.68", TimeSpan.FromSeconds(3));

        status.Should().BeNull();
    }
}
