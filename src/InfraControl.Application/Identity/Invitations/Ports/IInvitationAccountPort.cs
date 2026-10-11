using InfraControl.Application.Identity.Authentication;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Identity.Invitations.Ports;

public sealed record InvitationAccountInfo(Guid UserId, bool IsActive);

/// <summary>
/// Porta de provisionamento de conta para o fluxo de convites.
/// O adaptador em Infrastructure é responsável por criar a conta INATIVA
/// (domain + Identity sem senha) e, na etapa de ativação, definir a senha
/// e habilitar o agregado de domínio.
/// </summary>
public interface IInvitationAccountPort
{
    Task<InvitationAccountInfo?> GetAccountInfoAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Garante que uma conta exista para o email, criando-a INATIVA quando não existir.
    /// Se a conta já existir, apenas sincroniza as roles do convite sem alterar o estado de ativação.
    /// </summary>
    Task<Guid> ProvisionInactiveAccountAsync(
        string email,
        IReadOnlyCollection<UserRole> roles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Define a senha da conta e a habilita (User.Enable).
    /// Falha quando a conta informada não existe.
    /// </summary>
    Task<OperationResult> SetPasswordAndActivateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);
}
