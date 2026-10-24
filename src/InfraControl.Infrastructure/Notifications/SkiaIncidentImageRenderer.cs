using System.Globalization;
using InfraControl.Application.Notifications.WhatsApp;
using SkiaSharp;

namespace InfraControl.Infrastructure.Notifications;

/// <summary>Renderiza o cartão de alerta no padrão visual aprovado em ADR-059.</summary>
public sealed class SkiaIncidentImageRenderer : IIncidentImageRenderer
{
    public const int Width = 1024;
    public const int Height = 768;

    private const float Padding = 48;
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
        DrawHeader(canvas, data, accent);
        DrawSummary(canvas, data);
        DrawSeries(canvas, data, accent);

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
        using var badgeFont = new SKFont(SKTypeface.Default, 16);
        using var dateFont = new SKFont(SKTypeface.Default, 17);
        using var labelFont = new SKFont(SKTypeface.Default, 14);
        using var deviceFont = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), 24);
        using var titleFont = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), 31);
        using var badgePaint = new SKPaint { Color = accent, IsAntialias = true };
        using var foregroundPaint = new SKPaint { Color = Foreground, IsAntialias = true };
        using var mutedPaint = new SKPaint { Color = Muted, IsAntialias = true };
        using var badgeTextPaint = new SKPaint { Color = Background, IsAntialias = true };

        var badgeLabel = data.Kind switch
        {
            ChartCardKind.Incident => "INCIDENTE",
            ChartCardKind.Warning => "AVISO",
            ChartCardKind.Resolution => "RESOLVIDO",
            _ => string.Empty
        };
        var badgeWidth = badgeFont.MeasureText(badgeLabel) + 28;
        var badge = new SKRect(Padding, 32, Padding + badgeWidth, 70);
        canvas.DrawRoundRect(badge, 9, 9, badgePaint);
        canvas.DrawText(badgeLabel, badge.Left + 14, 57, badgeFont, badgeTextPaint);

        var stamp = FormatStamp(data.EventAtUtc);
        canvas.DrawText(stamp, Width - Padding - dateFont.MeasureText(stamp), 57,
            dateFont, mutedPaint);

        canvas.DrawText("DISPOSITIVO", Padding, 120, labelFont, mutedPaint);
        canvas.DrawText(data.DeviceLabel ?? "Dispositivo", Padding, 155,
            deviceFont, foregroundPaint);

        var titleLines = WrapText(data.Title, titleFont, foregroundPaint,
            Width - (Padding * 2), maxLines: 2);
        for (var index = 0; index < titleLines.Count; index++)
            canvas.DrawText(titleLines[index], Padding, 207 + (index * 38),
                titleFont, foregroundPaint);
    }

    private static void DrawSummary(SKCanvas canvas, IncidentChartData data)
    {
        using var labelFont = new SKFont(SKTypeface.Default, 15);
        using var valueFont = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), 24);
        using var labelPaint = new SKPaint { Color = Muted, IsAntialias = true };
        using var valuePaint = new SKPaint { Color = Foreground, IsAntialias = true };

        var columnWidth = (Width - (Padding * 2)) / 3;
        var stats = new[]
        {
            ("ATUAL", data.CurrentValue),
            ("MÍNIMO", data.MinValue),
            ("MÁXIMO", data.MaxValue)
        };

        for (var index = 0; index < stats.Length; index++)
        {
            var x = Padding + (columnWidth * index);
            canvas.DrawText(stats[index].Item1, x, 292, labelFont, labelPaint);
            canvas.DrawText(
                stats[index].Item2 is { } value ? FormatValue(value, data.Unit) : "—",
                x, 326, valueFont, valuePaint);
        }

    }

    private static void DrawSeries(SKCanvas canvas, IncidentChartData data, SKColor accent)
    {
        var area = new SKRect(112, 380, Width - Padding, 658);
        using var gridPaint = new SKPaint
        {
            Color = Grid, IsAntialias = true, StrokeWidth = 1
        };
        using var axisPaint = new SKPaint
        {
            Color = Muted, IsAntialias = true, StrokeWidth = 1
        };
        using var limitPaint = new SKPaint
        {
            Color = Muted, IsAntialias = true, StrokeWidth = 2,
            PathEffect = SKPathEffect.CreateDash([8, 7], 0)
        };
        using var linePaint = new SKPaint
        {
            Color = accent, IsAntialias = true, StrokeWidth = 3,
            Style = SKPaintStyle.Stroke, StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round
        };
        using var pointPaint = new SKPaint { Color = accent, IsAntialias = true };
        using var labelFont = new SKFont(SKTypeface.Default, 13);
        using var labelPaint = new SKPaint { Color = Muted, IsAntialias = true };

        if (data.Series.Count == 0)
        {
            const string hint = "Sem dados históricos disponíveis";
            var width = labelFont.MeasureText(hint);
            canvas.DrawText(hint, area.MidX - (width / 2), area.MidY,
                labelFont, labelPaint);
            return;
        }

        var minTime = data.Series.Min(point => point.AtUtc);
        var maxTime = data.Series.Max(point => point.AtUtc);
        var minValue = data.Series.Min(point => point.Value);
        var maxValue = data.Series.Max(point => point.Value);
        if (data.LimitValue is { } threshold)
        {
            minValue = Math.Min(minValue, threshold);
            maxValue = Math.Max(maxValue, threshold);
        }

        var range = maxValue - minValue;
        var padding = range > 0
            ? range * 0.08
            : Math.Max(Math.Abs(minValue) * 0.05, 1);
        minValue -= padding;
        maxValue += padding;

        for (var index = 0; index <= 4; index++)
        {
            var fraction = index / 4f;
            var y = area.Top + (area.Height * fraction);
            var value = maxValue - ((maxValue - minValue) * fraction);
            canvas.DrawLine(area.Left, y, area.Right, y, gridPaint);

            var label = FormatValue(value, data.Unit);
            canvas.DrawText(label, area.Left - 12 - labelFont.MeasureText(label),
                y + 5, labelFont, labelPaint);
        }

        for (var index = 0; index <= 4; index++)
        {
            var fraction = index / 4f;
            var x = area.Left + (area.Width * fraction);
            canvas.DrawLine(x, area.Top, x, area.Bottom, gridPaint);

            var time = minTime + TimeSpan.FromTicks(
                (long)((maxTime - minTime).Ticks * fraction));
            var label = time.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
            var labelX = Math.Clamp(x - (labelFont.MeasureText(label) / 2),
                area.Left, area.Right - labelFont.MeasureText(label));
            canvas.DrawText(label, labelX, 690, labelFont, labelPaint);
        }

        if (data.LimitValue is { } limit)
        {
            var limitY = YFor(limit, minValue, maxValue, area);
            canvas.DrawLine(area.Left, limitY, area.Right, limitY, limitPaint);
            canvas.DrawLine(area.Left, 727, area.Left + 32, 727, limitPaint);
            canvas.DrawText($"LIMITE {FormatValue(limit, data.Unit)}",
                area.Left + 42, 732, labelFont, labelPaint);
        }

        using var path = new SKPath();
        var span = (maxTime - minTime).TotalSeconds;
        for (var index = 0; index < data.Series.Count; index++)
        {
            var point = data.Series[index];
            var x = area.Left + (span <= 0
                ? area.Width / 2
                : area.Width * (float)((point.AtUtc - minTime).TotalSeconds / span));
            var y = YFor(point.Value, minValue, maxValue, area);
            if (index == 0)
                path.MoveTo(x, y);
            else
                path.LineTo(x, y);
            canvas.DrawCircle(x, y, 4, pointPaint);
        }

        canvas.DrawPath(path, linePaint);
        canvas.DrawLine(area.Left, area.Bottom, area.Right, area.Bottom, axisPaint);
    }

    private static List<string> WrapText(
        string text, SKFont font, SKPaint paint, float maxWidth, int maxLines)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (font.MeasureText(candidate, paint) <= maxWidth || string.IsNullOrEmpty(current))
            {
                current = candidate;
                continue;
            }

            lines.Add(current);
            current = word;
            if (lines.Count == maxLines - 1)
                break;
        }

        if (lines.Count < maxLines && !string.IsNullOrEmpty(current))
        {
            while (font.MeasureText(current, paint) > maxWidth && current.Length > 1)
                current = current[..^1];
            lines.Add(current);
        }

        if (lines.Count == 0)
            lines.Add(string.Empty);
        return lines;
    }

    private static float YFor(double value, double minValue, double maxValue, SKRect area)
        => area.Bottom - (float)(((value - minValue) / (maxValue - minValue)) * area.Height);

    private static string FormatValue(double value, string unit)
    {
        var formatted = value.ToString("0.##", CultureInfo.InvariantCulture);
        if (string.Equals(unit, "uptime", StringComparison.OrdinalIgnoreCase)
            && value >= 0
            && value <= TimeSpan.MaxValue.TotalSeconds)
        {
            var duration = TimeSpan.FromSeconds(value);
            return $"{(long)duration.TotalDays}d {duration.Hours:00}h {duration.Minutes:00}m";
        }

        return string.IsNullOrWhiteSpace(unit)
            ? formatted
            : unit is "%" or "°C" ? $"{formatted}{unit}" : $"{formatted} {unit}";
    }

    private static string FormatStamp(DateTime utc)
        => utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}
