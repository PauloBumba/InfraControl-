using System.ComponentModel.DataAnnotations;

namespace InfraControl.Api.ViewModels.Knowledge;

public sealed class CreateKnowledgeArticleViewModel
{
    [Required(ErrorMessage = "O título do artigo é obrigatório.")]
    [MaxLength(200, ErrorMessage = "O título do artigo pode ter no máximo 200 caracteres.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "O conteúdo do artigo é obrigatório.")]
    [MaxLength(20000, ErrorMessage = "O conteúdo do artigo pode ter no máximo 20000 caracteres.")]
    public string Content { get; set; } = string.Empty;

    public Guid? CategoryId { get; set; }

<<<<<<< HEAD
    // F24-T001/K1 — identificação (docs/32 §5–§6). O tipo é validado no
    // handler (400 pt-BR com as opções); vazio = artigo sem tipo.
    public string? Type { get; set; }
=======
    // F24-T001/K1 — identificação (docs/32 §5–§6)
    [Required(ErrorMessage = "O tipo do artigo é obrigatório.")]
    [MaxLength(50, ErrorMessage = "O tipo do artigo pode ter no máximo 50 caracteres.")]
    public string Type { get; set; } = string.Empty;
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c

    [MaxLength(500, ErrorMessage = "A descrição do artigo pode ter no máximo 500 caracteres.")]
    public string? Description { get; set; }

<<<<<<< HEAD
=======
    /// <summary>Tags brutas — limite/normalização no domínio (400 pt-BR).</summary>
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
    public List<string>? Tags { get; set; }

    public Guid? ResponsibleId { get; set; }
}

public sealed class UpdateKnowledgeArticleViewModel
{
    [Required(ErrorMessage = "O título do artigo é obrigatório.")]
    [MaxLength(200, ErrorMessage = "O título do artigo pode ter no máximo 200 caracteres.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "O conteúdo do artigo é obrigatório.")]
    [MaxLength(20000, ErrorMessage = "O conteúdo do artigo pode ter no máximo 20000 caracteres.")]
    public string Content { get; set; } = string.Empty;

    public Guid? CategoryId { get; set; }

<<<<<<< HEAD
    // F24-T003/K3 — toda versão registra o motivo (spec docs/32 §8).
    [Required(ErrorMessage = "O motivo da alteração é obrigatório.")]
    [MaxLength(500, ErrorMessage = "O motivo da alteração pode ter no máximo 500 caracteres.")]
    public string Reason { get; set; } = string.Empty;

    // F24-T005/K5 — governança (docs/32 §6/§12): agenda de revisão é
    // configurada à parte (PUT review-schedule), nunca no PUT do
    // artigo (evita apagar a agenda quando o cliente não envia o campo).
    public string? Type { get; set; }
=======
    // F24-T001/K1 — identificação (docs/32 §5–§6)
    [Required(ErrorMessage = "O tipo do artigo é obrigatório.")]
    [MaxLength(50, ErrorMessage = "O tipo do artigo pode ter no máximo 50 caracteres.")]
    public string Type { get; set; } = string.Empty;
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c

    [MaxLength(500, ErrorMessage = "A descrição do artigo pode ter no máximo 500 caracteres.")]
    public string? Description { get; set; }

<<<<<<< HEAD
    public List<string>? Tags { get; set; }

    public Guid? ResponsibleId { get; set; }
}

/// <summary>F24-T005/K5 — agenda de revisão periódica (docs/32 §12).</summary>
public sealed class SetKnowledgeArticleReviewScheduleViewModel
{
    /// <summary>Dias até a próxima revisão (1..3650); null = sem revisão periódica.</summary>
    [Range(1, 3650, ErrorMessage = "O intervalo de revisão deve estar entre 1 e 3650 dias.")]
    public int? IntervalDays { get; set; }

    public Guid? ReviewResponsibleId { get; set; }
=======
    /// <summary>Tags brutas — limite/normalização no domínio (400 pt-BR).</summary>
    public List<string>? Tags { get; set; }

    public Guid? ResponsibleId { get; set; }
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
}

/// <summary>F10-T003: corpo do POST {id}/status — o valor em si é
/// validado no handler (400 pt-BR com a lista de opções).</summary>
public sealed class ChangeKnowledgeArticleStatusViewModel
{
    [Required(ErrorMessage = "O status do artigo é obrigatório.")]
    public string Status { get; set; } = string.Empty;
}
