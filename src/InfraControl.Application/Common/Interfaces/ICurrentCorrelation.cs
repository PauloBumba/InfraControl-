namespace InfraControl.Application.Common.Interfaces;

/// <summary>
/// ADR-021: o correlation id da requisição que provocou a escrita, para o
/// rastro não morrer na fronteira do outbox.
///
/// Existe como interface e não como uma leitura direta do Serilog porque o
/// Domain e a Infrastructure não devem depender de Serilog para decidir o que
/// guardar. Quem implementa no host HTTP é
/// <c>InfraControl.Api.Middleware.HttpContextCorrelation</c>; nos workers não há
/// requisição, e a implementação devolve <c>null</c>.
/// </summary>
public interface ICurrentCorrelation
{
    /// <summary>Id da requisição em andamento, ou <c>null</c> fora de uma requisição.</summary>
    string? Id { get; }
}

/// <summary>
/// Processo sem requisição (workers): não há correlation id em andamento.
/// </summary>
public sealed class NullCurrentCorrelation : ICurrentCorrelation
{
    public string? Id => null;
}
