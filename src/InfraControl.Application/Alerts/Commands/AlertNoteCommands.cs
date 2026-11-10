using InfraControl.Application.Alerts.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Alerts.Commands;

// F50-T003 — anotação do operador no alerta. O alerta nasce do sistema
// (pós-check / Zabbix) e não tem CRUD público: este commando é a ÚNICA
// mutação humana permitida nele (PUT de substituição — texto grava,
// null/branco limpa).

/// <summary>
/// Anotação do operador — Operate; `IAuditableRequest` porque mexe num
/// dado de monitoramento (o AuditBehavior grava antes/depois sem payload).
/// </summary>
public sealed record SetAlertOperatorNoteCommand(
    Guid AlertId,
    string? Note) : BaseCommand<AlertDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;

    public string AuditResource => "Alert";
}

public sealed class SetAlertOperatorNoteCommandHandler(
    IAlertRepository repository)
    : IRequestHandler<SetAlertOperatorNoteCommand, AlertDto>
{
    public async Task<AlertDto> Handle(
        SetAlertOperatorNoteCommand request,
        CancellationToken cancellationToken)
    {
        var alert = await repository.GetByIdAsync(request.AlertId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Alerta '{request.AlertId}' não encontrado.");

        // Domínio faz trim, limpa em branco e guarda o máximo (400 pt-BR).
        alert.SetOperatorNote(request.Note);
        await repository.SaveChangesAsync(cancellationToken);

        return new AlertDto(
            alert.Id,
            alert.DeviceId,
            DeviceName: null, // a anotação não depende do join do device
            alert.Kind?.ToString(),
            alert.Severity.ToString(),
            alert.Status.ToString(),
            alert.Source.ToString(),
            alert.Message,
            alert.CreateDateTime,
            alert.ResolvedAt,
            alert.OperatorNote);
    }
}
