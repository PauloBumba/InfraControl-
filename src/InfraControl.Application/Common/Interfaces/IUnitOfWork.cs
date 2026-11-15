namespace InfraControl.Application.Common.Interfaces;

public interface IUnitOfWork : InfraControl.BuildingBlocks.Domain.IUnitOfWork
{
    /// <summary>
    /// SaveChanges tolerante a corrida de unicidade: devolve false quando
    /// outro escritor gravou o mesmo registro primeiro (violação 23505) —
    /// as alterações pendentes são descartadas do tracker (sem isto o
    /// próximo SaveChanges do mesmo escopo as repetiria e falharia de
    /// novo). O chamador deve reler o estado persistido e responder o
    /// outcome idempotente.
    /// </summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default);
}
