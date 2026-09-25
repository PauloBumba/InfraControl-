using InfraControl.Application.Common.Interfaces;

namespace InfraControl.Api.RateLimiting;

/// <summary>Regra por categoria: janela fixa com no máximo PermitLimit requisições.</summary>
public sealed record HttpRateLimitRule(int PermitLimit, int WindowSeconds);

/// <summary>
/// ADR-011 / docs/08 — camada HTTP do rate limiting. Seção "RateLimit" da
/// configuração; políticas variam por operação e criticidade (categoria).
/// </summary>
public sealed class HttpRateLimitOptions
{
    public const string SectionName = "RateLimit";

    public bool Enabled { get; set; } = true;

    public Dictionary<string, HttpRateLimitRule> Rules { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(RateLimitCategory.Default)] = new HttpRateLimitRule(120, 60),
        [nameof(RateLimitCategory.Login)] = new HttpRateLimitRule(5, 60),
        [nameof(RateLimitCategory.PasswordReset)] = new HttpRateLimitRule(3, 600),
        [nameof(RateLimitCategory.Webhook)] = new HttpRateLimitRule(120, 60),
        [nameof(RateLimitCategory.Ingestion)] = new HttpRateLimitRule(1000, 60)
    };

    public HttpRateLimitRule GetRule(RateLimitCategory category)
    {
        if (Rules.TryGetValue(category.ToString(), out var rule))
            return rule;

        return Rules[nameof(RateLimitCategory.Default)];
    }
}
