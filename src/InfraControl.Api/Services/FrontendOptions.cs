namespace InfraControl.Api.Services;

/// <summary>
/// Origem do frontend para os links que saem por e-mail (convite F1-T017,
/// reset — pendência da página /reset-password). Decisão do usuário em
/// 01/10/2026: a origem vem de config, nunca do cliente (origin vindo do
/// request de um anônimo seria vetor de phishing apontando token para outro
/// site).
///
/// - vazio (padrão em produção): fallback para a origem da própria request —
///   com o nginx same-origin (SPA em <c>/</c> e API em <c>/api</c>) essa origem
///   é a do frontend;
/// - preenchido (dev): a API vista pelo proxy do vite é <c>localhost:7237</c>,
///   que não serve o SPA — a config aponta para <c>http://localhost:3000</c>.
/// </summary>
public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>Origem (scheme+host[+porta], sem barra final) do dashboard.</summary>
    public string? BaseUrl { get; set; }
}
