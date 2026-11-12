using InfraControl.Application.Alerts.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Alerts.Commands;

// F52-T003 — baixa justificada do alerta (padrão ServiceNow): o operador
// resolve manualmente informando código + justificativa. A resolução
// automática pós-check continua chamando Alert.Resolve() sem argumentos
// (code/note ficam nulos).

/// <summary>
/// Baixa manual do alerta — Operate; `IAuditableRequest` (molde
/// <see cref="SetAlertOperatorNoteCommand"/>): mexe num dado de
/// monitoramento e o AuditBehavior grava a mutação.
/// </summary>
public sealed record ResolveAlertCommand(
    Guid AlertId,
    string Code,
    string Note) : BaseCommand<AlertDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;

    public string AuditResource => "Alert";
}

public sealed class ResolveAlertCommandHandler(IAlertRepository repository)
    : IRequestHandler<ResolveAlertCommand, AlertDto>
{
    public async Task<AlertDto> Handle(
        ResolveAlertCommand request,
        CancellationToken cancellationToken)
    {
        var alert = await repository.GetByIdAsync(request.AlertId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Alerta '{request.AlertId}' não encontrado.");

        // ResolutionCodes.Parse → 400 pt-BR "Código de baixa inválido: …";
        // domínio valida justificativa obrigatória e máximo (≤500).
        alert.Resolve(ResolutionCodes.Parse(request.Code), request.Note);
        await repository.SaveChangesAsync(cancellationToken);

        return new AlertDto(
            alert.Id,
            alert.DeviceId,
            DeviceName: null,
            alert.Kind?.ToString(),
            alert.Severity.ToString(),
            alert.Status.ToString(),
            alert.Source.ToString(),
            alert.Message,
            alert.CreateDateTime,
            alert.ResolvedAt,
            alert.OperatorNote,
            alert.ResolutionCode?.ToString(),
            alert.ResolutionNote);
    }
}
