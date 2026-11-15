namespace InfraControl.Domain.Enums;

/// <summary>
/// Frequência do relatório agendado por e-mail (Network Explorer F4+ /
/// feature de relatório geral): diário ou semanal (dia da semana fixo).
/// Persistido como string.
/// </summary>
public enum ReportFrequency
{
    Daily = 0,
    Weekly = 1
}
