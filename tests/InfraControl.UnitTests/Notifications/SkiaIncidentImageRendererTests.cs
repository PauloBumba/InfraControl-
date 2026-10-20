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
            EventAtUtc: DateTime.UtcNow,
            Series:
            [
                new ChartSeriesPoint(DateTime.UtcNow.AddMinutes(-30), 92),
                new ChartSeriesPoint(DateTime.UtcNow.AddMinutes(-10), 98),
                new ChartSeriesPoint(DateTime.UtcNow, 99),
            ],
            LimitValue: 95);

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
        bitmap.Width.Should().Be(SkiaIncidentImageRenderer.Width);
        bitmap.Height.Should().Be(SkiaIncidentImageRenderer.Height);
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
}
