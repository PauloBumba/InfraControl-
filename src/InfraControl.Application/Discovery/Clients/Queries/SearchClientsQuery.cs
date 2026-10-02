using InfraControl.Application.Discovery.Results.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Discovery.Clients.Queries;

// F18-T008: `GET /api/v1/clients` — clientes da rede sem depender do
// "último run de discovery" (decisão do F17-T015). Fonte = agregação de
// ARP/DHCP de TODAS as execuções; dedupe/agregação acontecem AQUI (Values é
// JSON serializado numa coluna de texto — não há dedupe por MAC portável
// entre Sqlite e Postgres), então a porta só devolve os rows candidatos.
// Contrato paginado estável: clamp 1..100, ordenação por LastSeenAt DESC.

public sealed record SearchClientsQuery(
    string? Term = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ClientDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class SearchClientsQueryHandler(IDiscoveryResultRepository repository)
    : IRequestHandler<SearchClientsQuery, PagedResult<ClientDto>>
{
    /// <summary>
    /// Kinds com potencial de cliente. Neighbor (LLDP/CDP), Host (ICMP) e
    /// MacTableEntry não descrevem clientes de rede — não entram na lista.
    /// </summary>
    private static readonly DiscoveryResultKind[] ClientKinds =
    [
        DiscoveryResultKind.ArpEntry,
        DiscoveryResultKind.DhcpLease,
    ];

    public async Task<PagedResult<ClientDto>> Handle(
        SearchClientsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var rows = await repository.ListByKindsAsync(ClientKinds, cancellationToken);

        var clients = Group(rows)
            .Select(Merge)
            .Where(client =>
                !string.IsNullOrWhiteSpace(client.Ip) ||
                !string.IsNullOrWhiteSpace(client.Mac))
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.Term))
        {
            var term = request.Term.Trim();
            clients = clients.Where(client =>
                Contains(client.Ip, term) ||
                Contains(client.Mac, term) ||
                Contains(client.Hostname, term)).ToList();
        }

        var ordered = clients
            .OrderByDescending(client => client.LastSeenAt)
            .ThenBy(client => client.Ip)
            .ThenBy(client => client.Mac)
            .ToList();

        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<ClientDto>(items, ordered.Count, page, pageSize);
    }

    /// <summary>
    /// Chave de dedupe: MAC normalizado (maiusculas, sem espaço nas pontas)
    /// quando existe; IP normalizado como fallback (linhas antigas de ARP sem
    /// MAC). LINQ em memória — ver doc-comment do handler.
    /// </summary>
    private static IEnumerable<IGrouping<string, Domain.Entities.DiscoveryResult>> Group(
        IEnumerable<Domain.Entities.DiscoveryResult> rows) =>
        rows
            .Where(row =>
                !string.IsNullOrWhiteSpace(Value(row, "ip")) ||
                !string.IsNullOrWhiteSpace(Value(row, "mac")))
            .GroupBy(row =>
            {
                var mac = Value(row, "mac");
                return !string.IsNullOrWhiteSpace(mac)
                    ? $"mac:{mac.Trim().ToUpperInvariant()}"
                    : $"ip:{Value(row, "ip")!.Trim()}";
            },
            StringComparer.OrdinalIgnoreCase);

    private static ClientDto Merge(IGrouping<string, Domain.Entities.DiscoveryResult> group)
    {
        // Mais recente primeiro: o kind e o lastSeenAt são da linha nova;
        // campos vazios nela são completados pelas linhas antigas do grupo.
        var newestFirst = group
            .OrderByDescending(row => row.CreatedAt)
            .ThenByDescending(row => row.Id)
            .ToList();

        return new ClientDto(
            FirstNonEmpty(newestFirst.Select(row => Value(row, "ip"))),
            FirstNonEmpty(newestFirst.Select(row => Value(row, "mac"))),
            FirstNonEmpty(newestFirst.Select(row => Value(row, "hostname"))),
            FirstNonEmpty(newestFirst.Select(row => Value(row, "vlan"))),
            FirstNonEmpty(newestFirst.Select(row => Value(row, "local_if"))),
            newestFirst[0].Kind.ToString(),
            newestFirst[0].CreatedAt);
    }

    private static string? Value(Domain.Entities.DiscoveryResult row, string key) =>
        row.Values is not null && row.Values.TryGetValue(key, out var value)
            ? value
            : null;

    private static string? FirstNonEmpty(IEnumerable<string?> values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static bool Contains(string? field, string term) =>
        field?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
}
