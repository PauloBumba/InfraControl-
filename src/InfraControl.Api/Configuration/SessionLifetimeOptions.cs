namespace InfraControl.Api.Configuration;

/// <summary>
/// F1-T016(b) / ADR-018: prazos de sessão em configuração, não em constante.
/// <para><see cref="SlidingLifetime"/> é esticado a cada refresh; <see cref="AbsoluteLifetime"/>
/// é o teto e nunca anda. Teto maior que deslizante é configuração sem sentido e
/// é recusado na validação.</para>
/// </summary>
public sealed class SessionLifetimeOptions
{
    public const string SectionName = "Session";

    /// <summary>Prazo deslizante. Também vira o <c>ExpireTimeSpan</c> do cookie.</summary>
    public TimeSpan SlidingLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Teto da sessão, contado do login. Não é esticado pelo refresh.</summary>
    public TimeSpan AbsoluteLifetime { get; set; } = TimeSpan.FromHours(12);

    /// <summary>Por quanto tempo o estado "sessão ativa" fica em cache.</summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    public bool IsValid =>
        SlidingLifetime > TimeSpan.Zero
        && AbsoluteLifetime > TimeSpan.Zero
        && CheckInterval > TimeSpan.Zero
        && AbsoluteLifetime <= SlidingLifetime;
}
