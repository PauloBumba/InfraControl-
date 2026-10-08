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

    // F24-T001/K1 — identificação (docs/32 §5–§6). O tipo é validado no
    // handler (400 pt-BR com as opções); vazio = artigo sem tipo.
    public string? Type { get; set; }

    [MaxLength(500, ErrorMessage = "A descrição do artigo pode ter no máximo 500 caracteres.")]
    public string? Description { get; set; }

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

    // F24-T001/K1 — identificação (docs/32 §5–§6).
    public string? Type { get; set; }

    [MaxLength(500, ErrorMessage = "A descrição do artigo pode ter no máximo 500 caracteres.")]
    public string? Description { get; set; }

    public List<string>? Tags { get; set; }

    public Guid? ResponsibleId { get; set; }
}

/// <summary>F10-T003: corpo do POST {id}/status — o valor em si é
/// validado no handler (400 pt-BR com a lista de opções).</summary>
public sealed class ChangeKnowledgeArticleStatusViewModel
{
    [Required(ErrorMessage = "O status do artigo é obrigatório.")]
    public string Status { get; set; } = string.Empty;
}
