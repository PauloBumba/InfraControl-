using InfraControl.Domain.Common;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

public sealed class Invitation : AggregateRoot
{
    private readonly List<InvitationRole> _roles = [];

    public string Email { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public InvitationStatus Status { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public IReadOnlyCollection<UserRole> Roles => _roles.Select(role => role.Role).ToArray();
    // A lista real (não AsReadOnly): o EF precisa escrever na coleção de
    // navegação (fixup de add/remove de InvitationRole). Escrito apenas pelo
    // próprio domínio + fixup; leitores externos só enxergam a interface.
    public IReadOnlyCollection<InvitationRole> RoleAssignments => _roles;

    private Invitation() { }

    private Invitation(Guid id, string email, string tokenHash, DateTime expiresAt, Guid createdBy, IEnumerable<UserRole> roles)
        : base(id)
    {
        Email = NormalizeEmail(email);
        TokenHash = RequireTokenHash(tokenHash);
        ExpiresAt = RequireFutureExpiry(expiresAt);
        CreatedBy = createdBy == Guid.Empty
            ? throw new ArgumentException("O id do convidante é obrigatório.", nameof(createdBy))
            : createdBy;
        Status = InvitationStatus.Pending;

        var distinctRoles = roles.Distinct().ToArray();
        if (distinctRoles.Length == 0)
            throw new ArgumentException("Ao menos uma função é obrigatória.", nameof(roles));

        foreach (var role in distinctRoles)
            _roles.Add(new InvitationRole(Id, role));

        RaiseDomainEvent(new InvitationCreatedDomainEvent(Id, Email));
    }

    public static Invitation Create(string email, string tokenHash, DateTime expiresAt, Guid createdBy, IEnumerable<UserRole> roles)
        => new(Guid.NewGuid(), email, tokenHash, expiresAt, createdBy, roles);

    public bool IsExpired(DateTime utcNow) => Status == InvitationStatus.Pending && ExpiresAt <= utcNow;

    public void MarkExpired()
    {
        if (Status != InvitationStatus.Pending) return;
        Status = InvitationStatus.Expired;
        Touch();
    }

    public void Accept(string tokenHash, DateTime utcNow)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException($"Apenas convites pendentes podem ser aceitos (atual: {Status}).");

        if (ExpiresAt <= utcNow)
        {
            MarkExpired();
            throw new InvalidOperationException("O convite expirou.");
        }

        if (!MatchesHash(tokenHash))
            throw new UnauthorizedAccessException("O token do convite é inválido.");

        Status = InvitationStatus.Accepted;
        DecidedAt = utcNow;
        Touch();
        RaiseDomainEvent(new InvitationAcceptedDomainEvent(Id, Email));
    }

    public void Reject(string tokenHash, DateTime utcNow)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException($"Apenas convites pendentes podem ser recusados (atual: {Status}).");

        if (ExpiresAt <= utcNow)
        {
            MarkExpired();
            throw new InvalidOperationException("O convite expirou.");
        }

        if (!MatchesHash(tokenHash))
            throw new UnauthorizedAccessException("O token do convite é inválido.");

        Status = InvitationStatus.Rejected;
        DecidedAt = utcNow;
        Touch();
        RaiseDomainEvent(new InvitationRejectedDomainEvent(Id, Email));
    }

    /// <summary>
    /// Valida que a ativação (definição de senha) pode ocorrer: convite já aceito,
    /// dentro da janela de expiração e com token correspondente. Não altera estado.
    /// </summary>
    public void EnsureActivatable(string tokenHash, DateTime utcNow)
    {
        if (Status != InvitationStatus.Accepted)
            throw new InvalidOperationException($"Apenas convites aceitos podem ser ativados (atual: {Status}).");

        if (ExpiresAt <= utcNow)
            throw new InvalidOperationException("O convite expirou.");

        if (!MatchesHash(tokenHash))
            throw new UnauthorizedAccessException("O token do convite é inválido.");
    }

    /// <summary>
    /// Marca o convite como ativado (conta provisionada e senha definida).
    /// Single-use: só aceita a transição Accepted → Activated.
    /// </summary>
    public void MarkActivated()
    {
        if (Status != InvitationStatus.Accepted)
            throw new InvalidOperationException($"Apenas convites aceitos podem ser ativados (atual: {Status}).");

        Status = InvitationStatus.Activated;
        Touch();
        RaiseDomainEvent(new InvitationActivatedDomainEvent(Id, Email));
    }

    /// <summary>
    /// ADR-030 §1 — PUT do convite pendente: substitui as funções
    /// (somente Pending — 409; vazio → 400; deduplica).
    /// </summary>
    public void SetRoles(IEnumerable<UserRole> roles)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException("Convite não está pendente.");

        var distinct = roles.Distinct().ToArray();
        if (distinct.Length == 0)
            throw new ArgumentException(
                "Ao menos uma função é obrigatória.", nameof(roles));

        _roles.Clear();
        foreach (var role in distinct)
            _roles.Add(new InvitationRole(Id, role));

        RaiseDomainEvent(new InvitationUpdatedDomainEvent(Id, Status));
    }

    public void Cancel()
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException($"Apenas convites pendentes podem ser cancelados (atual: {Status}).");

        Status = InvitationStatus.Cancelled;
        DecidedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        Touch();

        RaiseDomainEvent(new InvitationUpdatedDomainEvent(Id, Status));
    }

    /// <summary>
    /// Aceite de convite em tempo real dentro da plataforma (usuário já autenticado).
    /// Não valida token pois o email já foi validado pelo contexto do usuário logado.
    /// </summary>
    public void AcceptInternal(DateTime utcNow)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException($"Apenas convites pendentes podem ser aceitos (atual: {Status}).");

        if (ExpiresAt <= utcNow)
        {
            MarkExpired();
            throw new InvalidOperationException("O convite expirou.");
        }

        Status = InvitationStatus.Accepted;
        DecidedAt = utcNow;
        Touch();
        RaiseDomainEvent(new InvitationAcceptedDomainEvent(Id, Email));
    }

    /// <summary>
    /// Recusa de convite em tempo real dentro da plataforma (usuário já autenticado).
    /// Não valida token pois o email já foi validado pelo contexto do usuário logado.
    /// </summary>
    public void RejectInternal(DateTime utcNow)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException($"Apenas convites pendentes podem ser recusados (atual: {Status}).");

        if (ExpiresAt <= utcNow)
        {
            MarkExpired();
            throw new InvalidOperationException("O convite expirou.");
        }

        Status = InvitationStatus.Rejected;
        DecidedAt = utcNow;
        Touch();
        RaiseDomainEvent(new InvitationRejectedDomainEvent(Id, Email));
    }

    private bool MatchesHash(string tokenHash)
    {
        var expected = System.Text.Encoding.UTF8.GetBytes(TokenHash);
        var actual = System.Text.Encoding.UTF8.GetBytes(tokenHash ?? string.Empty);
        return expected.Length == actual.Length
            && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("Um e-mail válido é obrigatório.", nameof(email));

        return email.Trim().ToLowerInvariant();
    }

    private static string RequireTokenHash(string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("O hash do token é obrigatório.", nameof(tokenHash));

        return tokenHash;
    }

    private static DateTime RequireFutureExpiry(DateTime expiresAt)
    {
        if (expiresAt <= AppTimeProvider.Current.GetUtcNow().UtcDateTime)
            throw new ArgumentException("A expiração deve estar no futuro.", nameof(expiresAt));

        return expiresAt;
    }
}
