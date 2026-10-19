namespace InfraControl.Api.ViewModels.Admin;

// ADR-061 §1 (H1): o contrato JSON de enum viaja como STRING — os VMs
// recebem os nomes ("Admin"/"Operator"/"Viewer") e o controller parseia
// para UserRole (molde do Search de status na própria classe). Antes os
// VMs eram enum tipado sem conversor: o STJ esperava número e o PUT do
// front (roles: string[]) caía em 400 em 100% das tentativas.
public sealed record CreateInvitationViewModel(
    string Email,
    IReadOnlyCollection<string> Roles,
    int ExpiresInDays = 7);

/// <summary>PUT do convite pendente (ADR-030 §1).</summary>
public sealed record UpdateInvitationViewModel(IReadOnlyCollection<string> Roles);

