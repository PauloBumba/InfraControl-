namespace InfraControl.Domain.Access;

/// <summary>
/// Catálogo canônico de módulos do sistema (ADR-066): única fonte que
/// traduz rota HTTP (<c>/api/v1/{segmento}</c>) e namespace MediatR
/// (<c>InfraControl.Application.{Segmento}</c>) para a mesma chave de
/// módulo gerenciada pelo admin em <c>AccessPolicyRule</c>.
/// Módulo não catalogado → null → fallback para a checagem legada de roles.
/// </summary>
public static class AccessModuleCatalog
{
    /// <summary>Módulo: chave canônica, rótulo pt-BR, segmentos de rota e raízes de namespace.</summary>
    public sealed record Module(
        string Key,
        string Label,
        string[] RouteSegments,
        string[] NamespaceRoots);

    // static readonly (não const): NetArchTest HaveDependencyOn varre constantes
    // de campo e casaria "InfraControl.Application." como dependência de assembly
    // (ProjectStructureTests.Domain_Should_Not_Depend_On_Application).
    private static readonly string AppPrefix = "InfraControl.Application.";

    public static readonly IReadOnlyList<Module> Modules =
    [
        new("account", "Minha conta", ["account"], ["Account"]),
        new("admin", "Administração", ["admin"], ["Identity.Administration"]),
        new("ai", "Inteligência Artificial", ["ai", "automation"], ["AI"]),
        new("auth", "Autenticação", ["auth"], ["Identity.Authentication", "Identity.Sessions"]),
        new("checklists", "Checklists", ["checklists"], ["Checklists"]),
        new("company-settings", "Config. institucional", ["company-settings"], ["Company"]),
        new("discovery", "Descoberta", ["discovery", "clients"], ["Discovery"]),
        new("incidents", "Incidentes", ["incidents"], ["Incidents"]),
        new("integrations", "Integrações", ["integrations"], ["Integrations"]),
        new("inventory", "Inventário", ["inventory"], ["Inventory", "Devices", "RemoteAccess"]),
        new("invitations", "Convites", ["invitations"], ["Identity.Invitations"]),
        new("knowledge", "Conhecimento", ["knowledge"], ["Knowledge"]),
        new("monitoring", "Monitoramento", ["monitoring"], ["Monitoring", "Alerts"]),
        new("network", "Rede", ["network"], ["Network"]),
        new("notifications", "Notificações", ["notifications"], ["Notifications"]),
        new("operations", "Operação", ["operations"], ["Operations"]),
        new("pdi", "PDI", ["pdi"], ["Pdi"]),
        new("projects", "Projetos", ["projects"], ["Projects"]),
        new("reports", "Relatórios", ["reports"], ["Reports"]),
        new("sla", "SLA/SLO", ["sla"], ["Sla"]),
        new("tasks", "Tarefas", ["tasks"], ["Tasks"]),
        new("uploads", "Uploads", ["uploads"], ["Uploads"]),
    ];

    private static readonly Dictionary<string, string> RouteAlias = BuildAlias(
        Modules.SelectMany(m => m.RouteSegments.Select(s => (s, m.Key))));

    private static readonly Dictionary<string, string> NamespaceAlias = BuildAlias(
        Modules.SelectMany(m => m.NamespaceRoots.Select(s => (s, m.Key))));

    /// <summary>Resolve o módulo pelo segmento de rota após <c>/api/v1/</c>.</summary>
    public static string? FromRouteSegment(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return null;

        var normalized = segment.Trim().ToLowerInvariant();
        return RouteAlias.TryGetValue(normalized, out var key) ? key : null;
    }

    /// <summary>
    /// Resolve o módulo pelo namespace do tipo do request MediatR
    /// (<c>InfraControl.Application.{Modulo}[.{Sub}]</c>). <c>Identity</c> é
    /// subdividido (Administration→admin, Authentication/Sessions→auth,
    /// Invitations→invitations). Desconhecido → null (fallback legado).
    /// </summary>
    public static string? FromRequestNamespace(string? ns)
    {
        if (string.IsNullOrWhiteSpace(ns) ||
            !ns.StartsWith(AppPrefix, StringComparison.Ordinal))
            return null;

        var rest = ns[AppPrefix.Length..];
        var firstDot = rest.IndexOf('.');
        var root = firstDot < 0 ? rest : rest[..firstDot];

        if (root.Equals("Identity", StringComparison.OrdinalIgnoreCase))
        {
            if (firstDot < 0)
                return "admin";

            var sub = rest[(firstDot + 1)..];
            var subDot = sub.IndexOf('.');
            var subRoot = (subDot < 0 ? sub : sub[..subDot])
                .ToLowerInvariant();
            return subRoot switch
            {
                "authentication" or "sessions" => "auth",
                "invitations" => "invitations",
                _ => "admin",
            };
        }

        if (root.Equals("Common", StringComparison.OrdinalIgnoreCase))
            return null;

        var normalized = root.ToLowerInvariant();
        return NamespaceAlias.TryGetValue(normalized, out var key) ? key : null;
    }

    /// <summary>Rótulo pt-BR do módulo; desconhecido → a própria chave.</summary>
    public static string LabelOf(string moduleKey) =>
        Modules.FirstOrDefault(m =>
            m.Key.Equals(moduleKey, StringComparison.OrdinalIgnoreCase))?.Label
        ?? moduleKey;

    private static Dictionary<string, string> BuildAlias(
        IEnumerable<(string Segment, string Key)> pairs)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (segment, key) in pairs)
            map[segment.ToLowerInvariant()] = key;
        return map;
    }
}
