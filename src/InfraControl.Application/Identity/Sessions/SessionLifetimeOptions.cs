using InfraControl.Domain.Enums;

namespace InfraControl.Application.Identity.Sessions;

/// <summary>
/// F1-T016(b)/ADR-018 + F1-T016b/ADR-020: prazos de sessão em configuração,
/// não em constante — e desde o ADR-020 o teto também é cortável por role.
/// <para><see cref="SlidingLifetime"/> é esticado a cada refresh;
/// <see cref="AbsoluteLifetime"/> é o teto e nunca anda. Cada entrada de
/// <see cref="RoleAbsoluteLifetimes"/> encurta esse teto para quem tem a role
/// (cap só corta, nunca amplia). Teto maior que deslizante, cap não positivo
/// ou role desconhecida é configuração sem sentido e é recusado na
/// validação — falhar no boot é melhor do que falhar em produção.</para>
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

    /// <summary>
    /// F1-T016b/ADR-020: teto por role (ex.: <c>Admin</c> → 08:00:00). No login
    /// o teto efetivo é o menor entre este cap e o <see cref="AbsoluteLifetime"/>.
    /// Vale para o próximo login: sessão existente não é reescrita.
    /// </summary>
    public Dictionary<string, TimeSpan> RoleAbsoluteLifetimes { get; set; } = new();

    /// <summary>
    /// Cap da role com igualdade de caixa — a configuração pode vir como
    /// <c>admin</c> ou <c>Admin</c>. Role sem cap devolve falso.
    /// </summary>
    public bool TryGetRoleCap(string role, out TimeSpan absoluteLifetime)
    {
        foreach (var (key, value) in RoleAbsoluteLifetimes)
        {
            if (string.Equals(key, role, StringComparison.OrdinalIgnoreCase))
            {
                absoluteLifetime = value;
                return true;
            }
        }

        absoluteLifetime = default;
        return false;
    }

    public bool IsValid =>
        SlidingLifetime > TimeSpan.Zero
        && AbsoluteLifetime > TimeSpan.Zero
        && CheckInterval > TimeSpan.Zero
        && AbsoluteLifetime <= SlidingLifetime
        && RoleAbsoluteLifetimes.All(cap => cap.Value > TimeSpan.Zero)
        && RoleAbsoluteLifetimes.All(cap => Enum.TryParse<UserRole>(cap.Key, ignoreCase: true, out _));
}
