using System.Globalization;
using InfraControl.Application.Notifications.WhatsApp;
using SkiaSharp;

namespace InfraControl.Infrastructure.Notifications;

/// <summary>
/// F33-T005/ADR-059 decisão 7: cartão PNG do evento (fundo escuro,
/// identidade uniforme, linha da série + limite tracejado) — referência
/// visual do card da plataforma. Stateless; theme fixo (sem dependência
/// de culture/relógio além do timestamp do próprio dado).
/// </summary>
public sealed class SkiaIncidentImageRenderer : IIncidentImageRenderer
{
    public const int Width = 800;
    public const int Height = 450;

    private static readonly SKColor Background = new(0x0F, 0x14, 0x19);
    private static readonly SKColor Foreground = new(0xF0, 0xF3, 0xF6);
    private static readonly SKColor Muted = new(0x8B, 0x95, 0xA1);
    private static readonly SKColor Grid = new(0x2A, 0x33, 0x3D);
    private static readonly SKColor Incident = new(0xE5, 0x48, 0x4D);
    private static readonly SKColor Warning = new(0xF5, 0xA5, 0x24);
    private static readonly SKColor Resolution = new(0x30, 0xA4, 0x6C);

    public byte[] RenderPng(IncidentChartData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var bitmap = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(Background);

        var accent = AccentFor(data.Kind);
        using (var bar = new SKPaint { Color = accent, IsAntialias = true })
            canvas.DrawRect(new SKRect(0, 0, 8, Height), bar);

        DrawHeader(canvas, data, accent);
        DrawSeries(canvas, data, accent);
        DrawFooter(canvas, data);

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static SKColor AccentFor(ChartCardKind kind) => kind switch
    {
        ChartCardKind.Incident => Incident,
        ChartCardKind.Warning => Warning,
        ChartCardKind.Resolution => Resolution,
        _ => Muted
    };

    private static void DrawHeader(SKCanvas canvas, IncidentChartData data, SKColor accent)
    {
        using var titleFont = new SKFont(
            SKTypeface.FromFamilyName(null, SKFontStyle.Bold), 24);
        using var metaFont = new SKFont(SKTypeface.Default, 15);
        using var titlePaint = new SKPaint { Color = Foreground, IsAntialias = true };
        using var metaPaint = new SKPaint { Color = Muted, IsAntialias = true };
        using var kindPaint = new SKPaint { Color = accent, IsAntialias = true };

        canvas.DrawText(data.Title, 28, 48, titleFont, titlePaint);

        var kindLabel = data.Kind switch
        {
            ChartCardKind.Incident => "INCIDENTE",
            ChartCardKind.Warning => "AVISO",
            ChartCardKind.Resolution => "RESOLUÇÃO",
            _ => string.Empty
        };
        canvas.DrawText(kindLabel, 28, 76, metaFont, kindPaint);

        var metaParts = new List<string> { data.SeverityLabel };
        if (!string.IsNullOrWhiteSpace(data.DeviceLabel))
            metaParts.Insert(0, data.DeviceLabel!);
        if (data.CurrentValue is { } current)
            metaParts.Add($"atual {Format(current)}");
        if (data.MinValue is { } min && data.MaxValue is { } max)
            metaParts.Add($"mín {Format(min)} / máx {Format(max)}");
        canvas.DrawText(string.Join(" · ", metaParts), 28, 102, metaFont, metaPaint);
    }

    private static void DrawSeries(SKCanvas canvas, IncidentChartData data, SKColor accent)
    {
        var area = new SKRect(48, 130, Width - 28, Height - 56);

        using var gridPaint = new SKPaint { Color = Grid, IsAntialias = true, StrokeWidth = 1 };
        using var limitPaint = new SKPaint
        {
            Color = Muted, IsAntialias = true, StrokeWidth = 1.5f,
            PathEffect = SKPathEffect.CreateDash([6, 6], 0)
        };
        using var linePaint = new SKPaint
        {
            Color = accent, IsAntialias = true, StrokeWidth = 2.5f,
            Style = SKPaintStyle.Stroke, StrokeJoin = SKStrokeJoin.Round
        };

        for (var i = 0; i <= 4; i++)
        {
            var y = area.Top + (area.Height * i / 4);
            canvas.DrawLine(area.Left, y, area.Right, y, gridPaint);
        }

        if (data.Series.Count == 0)
        {
            using var emptyFont = new SKFont(SKTypeface.Default, 16);
            using var emptyPaint = new SKPaint { Color = Muted, IsAntialias = true };
            const string hint = "Sem dados históricos disponíveis";
            var width = emptyFont.MeasureText(hint);
            canvas.DrawText(
                hint,
                area.MidX - (width / 2),
                area.MidY,
                emptyFont, emptyPaint);
            return;
        }

        var minTime = data.Series.Min(point => point.AtUtc);
        var maxTime = data.Series.Max(point => point.AtUtc);
        var minValue = data.Series.Min(point => point.Value);
        var maxValue = data.Series.Max(point => point.Value);
        if (maxValue - minValue < double.Epsilon)
        {
            minValue -= 1;
            maxValue += 1;
        }

        if (data.LimitValue is { } limit && limit >= minValue && limit <= maxValue)
        {
            var limitY = YFor(limit, minValue, maxValue, area);
            canvas.DrawLine(area.Left, limitY, area.Right, limitY, limitPaint);
        }

        using var path = new SKPath();
        var span = (maxTime - minTime).TotalSeconds;
        for (var i = 0; i < data.Series.Count; i++)
        {
            var point = data.Series[i];
            var x = area.Left
                + (span <= 0
                    ? area.Width / 2
                    : area.Width * (float)((point.AtUtc - minTime).TotalSeconds / span));
            var y = YFor(point.Value, minValue, maxValue, area);
            if (i == 0)
                path.MoveTo(x, y);
            else
                path.LineTo(x, y);
        }

        canvas.DrawPath(path, linePaint);
    }

    private static float YFor(
        double value, double minValue, double maxValue, SKRect area)
        => area.Bottom
           - (float)(((value - minValue) / (maxValue - minValue)) * area.Height);

    private static void DrawFooter(SKCanvas canvas, IncidentChartData data)
    {
        using var font = new SKFont(SKTypeface.Default, 12);
        using var paint = new SKPaint { Color = Muted, IsAntialias = true };
        canvas.DrawText(
            $"InfraControl · evento {FormatStamp(data.EventAtUtc)}",
            28, Height - 18, font, paint);
    }

    private static string Format(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatStamp(DateTime utc)
        => utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}
