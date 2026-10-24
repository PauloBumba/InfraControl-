using FluentAssertions;
using InfraControl.Application.Notifications.WhatsApp;
using InfraControl.Infrastructure.Notifications;
using SkiaSharp;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F33-T005: o renderer SkiaSharp transforma <see cref="IncidentChartData"/>
/// em PNG válido de tamanho fixo. Nunca lança — o dispatcher já trata a
/// falha de render com fallback para texto.
/// </summary>
public sealed class SkiaIncidentImageRendererTests
{
    private readonly SkiaIncidentImageRenderer _renderer = new();

    private static byte[] Png(ChartCardKind kind)
    {
        var data = new IncidentChartData(
            kind,
            Title: "Alerta Critical criado",
            DeviceLabel: "sw-01",
            SeverityLabel: "Critical",
            EventAtUtc: new DateTime(2026, 10, 23, 14, 5, 0, DateTimeKind.Utc),
            Series:
            [
                new ChartSeriesPoint(new DateTime(2026, 10, 23, 12, 5, 0, DateTimeKind.Utc), 92),
                new ChartSeriesPoint(new DateTime(2026, 10, 23, 13, 45, 0, DateTimeKind.Utc), 98),
                new ChartSeriesPoint(new DateTime(2026, 10, 23, 14, 5, 0, DateTimeKind.Utc), 99),
            ],
            LimitValue: 95,
            Unit: "%");

        return new SkiaIncidentImageRenderer().RenderPng(data);
    }

    [Fact]
    public void RenderPng_ShouldEmitPngMagicBytes()
    {
        var bytes = Png(ChartCardKind.Incident);

        bytes.Should().StartWith([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    }

    [Theory]
    [InlineData(ChartCardKind.Incident)]
    [InlineData(ChartCardKind.Warning)]
    [InlineData(ChartCardKind.Resolution)]
    public void RenderPng_ShouldDecodeToFixedCanvas(ChartCardKind kind)
    {
        var bytes = Png(kind);

        using var bitmap = SKBitmap.Decode(bytes);
        bitmap.Should().NotBeNull("o PNG precisa ser decodificável");
        bitmap!.Width.Should().Be(1024);
        bitmap.Height.Should().Be(768);
    }

    [Fact]
    public void RenderPng_WithEmptySeries_ShouldStillEmitPng()
    {
        var data = new IncidentChartData(
            ChartCardKind.Warning,
            Title: "Aviso sem série",
            DeviceLabel: null,
            SeverityLabel: "Warning",
            EventAtUtc: DateTime.UtcNow,
            Series: [],
            LimitValue: null);

        var bytes = _renderer.RenderPng(data);

        bytes.Should().StartWith([0x89, 0x50, 0x4E, 0x47]);
        using var bitmap = SKBitmap.Decode(bytes);
        bitmap.Width.Should().Be(SkiaIncidentImageRenderer.Width);
    }

    [Fact]
    public void RenderPng_DifferentKinds_ShouldDifferInBytes()
    {
        var incident = Png(ChartCardKind.Incident);
        var resolution = Png(ChartCardKind.Resolution);

        incident.Should().NotEqual(resolution,
            "cada tipo de cartão tem cor de acento própria");
    }

    [Theory]
    [InlineData(ChartCardKind.Incident, 0xE5, 0x48, 0x4D)]
    [InlineData(ChartCardKind.Warning, 0xF5, 0xA5, 0x24)]
    [InlineData(ChartCardKind.Resolution, 0x30, 0xA4, 0x6C)]
    public void RenderPng_ShouldUseReferenceStatusColor(
        ChartCardKind kind, byte red, byte green, byte blue)
    {
        using var bitmap = SKBitmap.Decode(Png(kind));

        bitmap.GetPixel(70, 45).Should().Be(new SKColor(red, green, blue));
    }

    [Fact]
    public void RenderPng_WithSinglePointAndNoLimit_ShouldEmitDecodableImage()
    {
        var data = new IncidentChartData(
            ChartCardKind.Resolution,
            Title: "Host reiniciado",
            DeviceLabel: "vCenter",
            SeverityLabel: "OK",
            EventAtUtc: new DateTime(2026, 10, 23, 14, 5, 0, DateTimeKind.Utc),
            Series: [new ChartSeriesPoint(
                new DateTime(2026, 10, 23, 14, 5, 0, DateTimeKind.Utc), 8035200)],
            Unit: "uptime");

        using var bitmap = SKBitmap.Decode(_renderer.RenderPng(data));

        bitmap.Should().NotBeNull();
        bitmap!.Width.Should().Be(SkiaIncidentImageRenderer.Width);
    }
}
