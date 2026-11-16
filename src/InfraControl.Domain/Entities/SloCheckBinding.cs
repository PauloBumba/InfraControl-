using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Vínculo N:N Slo ↔ MonitoringCheck (ADR-068): o SLO mede a
/// disponibilidade (% de MonitoringResult com sucesso) dos checks
/// vinculados na sua janela. Chave composta (SloId, MonitoringCheckId);
/// FKs Restrict nos dois lados — excluir SLO ou check vinculado é
/// bloqueado (pré-check 409 + backstop do banco).
/// </summary>
public sealed class SloCheckBinding : Entity
{
    public Guid SloId { get; private set; }
    public Guid MonitoringCheckId { get; private set; }

    private SloCheckBinding() { }

    public static SloCheckBinding Create(Guid sloId, Guid monitoringCheckId)
    {
        if (sloId == Guid.Empty)
            throw new ArgumentException("O id do SLO é obrigatório.", nameof(sloId));
        if (monitoringCheckId == Guid.Empty)
            throw new ArgumentException(
                "O id do check de monitoramento é obrigatório.", nameof(monitoringCheckId));

        return new SloCheckBinding
        {
            SloId = sloId,
            MonitoringCheckId = monitoringCheckId
        };
    }
}
