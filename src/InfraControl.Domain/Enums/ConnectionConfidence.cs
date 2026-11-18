namespace InfraControl.Domain.Enums;

/// <summary>
/// Confiança da Connection (F53-T001, spec 11.7): Confirmed = evidência
/// que sustenta o link (configuração manual ou protocolo LLDP/CDP);
/// Estimated = reservado para inferências futuras; Unknown = sem
/// informação suficiente para confirmar — apresentar como desconhecida,
/// nunca inventar ligação.
/// </summary>
public enum ConnectionConfidence
{
    Unknown = 0,
    Confirmed = 1,
    Estimated = 2
}
