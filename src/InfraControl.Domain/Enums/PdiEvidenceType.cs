namespace InfraControl.Domain.Enums;

/// <summary>
/// Tipos fixos de evidência (docs/30 §12): o mecanismo é genérico, mas
/// a classificação é fechada para permitir indicadores (§21) — lookup
/// configurável continua fora do escopo (Fase 8).
/// </summary>
public enum PdiEvidenceType
{
    /// <summary>Certificado (conclusão de curso, treinamento…).</summary>
    Certificate = 1,

    /// <summary>Documento (relatório, ata, declaração…).</summary>
    Document = 2,

    /// <summary>Imagem (foto, captura de tela…).</summary>
    Image = 3,

    /// <summary>Arquivo genérico.</summary>
    File = 4,

    /// <summary>Atividade realizada (registro de execução).</summary>
    CompletedActivity = 5,

    /// <summary>Avaliação (resultado de avaliação formal).</summary>
    Assessment = 6,

    /// <summary>Feedback (retorno de terceiros registrado como evidência).</summary>
    Feedback = 7,

    /// <summary>Resultado (métrica, indicador atingido).</summary>
    Result = 8,

    /// <summary>Outro (fora das categorias acima).</summary>
    Other = 9
}

/// <summary>Rotulos pt-BR do <see cref="PdiEvidenceType"/> (timeline e UX).</summary>
public static class PdiEvidenceTypeLabels
{
    public static string ToDisplayLabel(this PdiEvidenceType type)
        => type switch
        {
            PdiEvidenceType.Certificate => "certificado",
            PdiEvidenceType.Document => "documento",
            PdiEvidenceType.Image => "imagem",
            PdiEvidenceType.File => "arquivo",
            PdiEvidenceType.CompletedActivity => "atividade realizada",
            PdiEvidenceType.Assessment => "avaliação",
            PdiEvidenceType.Feedback => "feedback",
            PdiEvidenceType.Result => "resultado",
            PdiEvidenceType.Other => "outro",
            _ => "evidência"
        };
}
