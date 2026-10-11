using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Identity.Invitations.Commands;
using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.Application.Projects.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Projects.Commands;

public sealed record CreateProjectInvitationCommand(
    Guid ProjectId,
    string Email,
    ProjectMemberRole Role,
    string? BaseUrl = null,
    int ExpiresInDays = 7) : BaseCommand<ProjectInvitationResult>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles => [];
    public string AuditResource => "ProjectInvitation";
}

public sealed record ProjectInvitationResult(
    Guid InvitationId,
    bool AccountExists,
    bool RequiresActivation);

public sealed class CreateProjectInvitationCommandHandler(
    IProjectRepository projectRepository,
    IInvitationRepository invitationRepository,
    IInvitationAccountPort accountPort,
    IIdentityEmailSender emailSender,
    ICurrentUserContext currentUser,
    ILogger<CreateProjectInvitationCommandHandler> logger)
    : IRequestHandler<CreateProjectInvitationCommand, ProjectInvitationResult>
{
    public async Task<ProjectInvitationResult> Handle(
        CreateProjectInvitationCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } inviterId)
            throw new UnauthorizedAccessException("Usuário autenticado não identificado para enviar convite.");

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            throw new ArgumentException("Um e-mail válido é obrigatório.", nameof(request.Email));
        if (!Enum.IsDefined(request.Role))
            throw new ArgumentOutOfRangeException(nameof(request.Role), "Papel de equipe inválido.");

        var project = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException($"Projeto '{request.ProjectId}' não encontrado.");

        var isSystemAdmin = currentUser.Roles.Contains(UserRole.Admin.ToString(), StringComparer.OrdinalIgnoreCase);
        if (!isSystemAdmin && project.CreatedById != inviterId)
            throw new UnauthorizedAccessException("Somente o criador do projeto ou um Administrador pode convidar membros.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var account = await accountPort.GetAccountInfoAsync(normalizedEmail, cancellationToken);

        if (account is not null && project.Members.Any(member => member.UserId == account.UserId))
            throw new InvalidOperationException("Este usuário já faz parte da equipe do projeto.");

        if (await invitationRepository.GetPendingByEmailAndProjectAsync(
                normalizedEmail, project.Id, cancellationToken) is not null)
            throw new InvalidOperationException(
                $"Já existe um convite pendente para '{normalizedEmail}' neste projeto.");

        var expiresInDays = Math.Clamp(request.ExpiresInDays, 1, 30);
        var token = InvitationToken.Generate();
        var utcNow = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        var invitation = Invitation.CreateForProject(
            normalizedEmail,
            InvitationToken.Hash(token),
            utcNow.AddDays(expiresInDays),
            inviterId,
            project.Id,
            project.Code,
            project.Title,
            request.Role);

        await invitationRepository.AddAsync(invitation, cancellationToken);
        await invitationRepository.SaveChangesAsync(cancellationToken);

        var baseUrl = request.BaseUrl?.TrimEnd('/') ?? string.Empty;
        var invitationUrl = account is { IsActive: true }
            ? $"{baseUrl}/invitations"
            : $"{baseUrl}/invitations/{invitation.Id}?token={token}";
        await emailSender.SendInvitationAsync(normalizedEmail, invitationUrl, cancellationToken);

        logger.LogInformation(
            "Project invitation {InvitationId} created for {ProjectId} and {Email}.",
            invitation.Id,
            project.Id,
            normalizedEmail);

        return new ProjectInvitationResult(
            invitation.Id,
            account is not null,
            account is not { IsActive: true });
    }
}
