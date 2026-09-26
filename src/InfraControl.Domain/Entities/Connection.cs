using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Conexão entre duas interfaces (docs/03 — Interface N:N Interface via
/// Connection; F3-T005 — decisões do usuário: par DIRIGIDO, ou seja
/// (a,b) ≠ (b,a); única restrição é self-loop; mesmo device é permitido).
/// Origem/destino: FKs Restrict + índice único do par na migração.
/// </summary>
public sealed class Connection : AggregateRoot
{
    public Guid SourceInterfaceId { get; private set; }
    public Guid TargetInterfaceId { get; private set; }

    private Connection() { }

    public static Connection Create(Guid sourceInterfaceId, Guid targetInterfaceId)
    {
        if (sourceInterfaceId == Guid.Empty)
            throw new ArgumentException(
                "O id da interface de origem é obrigatório.", nameof(sourceInterfaceId));

        if (targetInterfaceId == Guid.Empty)
            throw new ArgumentException(
                "O id da interface de destino é obrigatório.", nameof(targetInterfaceId));

        if (sourceInterfaceId == targetInterfaceId)
            throw new ArgumentException(
                "A conexão não pode ligar a interface a ela mesma.",
                nameof(targetInterfaceId));

        return new Connection
        {
            SourceInterfaceId = sourceInterfaceId,
            TargetInterfaceId = targetInterfaceId
        };
    }
}
