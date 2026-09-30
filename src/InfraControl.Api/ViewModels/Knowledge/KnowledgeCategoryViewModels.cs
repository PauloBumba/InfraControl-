using System.ComponentModel.DataAnnotations;

namespace InfraControl.Api.ViewModels.Knowledge;

public sealed class CreateKnowledgeCategoryViewModel
{
    [Required(ErrorMessage = "O nome da categoria é obrigatório.")]
    [MaxLength(120, ErrorMessage = "O nome da categoria pode ter no máximo 120 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "A descrição da categoria pode ter no máximo 500 caracteres.")]
    public string? Description { get; set; }
}

public sealed class UpdateKnowledgeCategoryViewModel
{
    [Required(ErrorMessage = "O nome da categoria é obrigatório.")]
    [MaxLength(120, ErrorMessage = "O nome da categoria pode ter no máximo 120 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "A descrição da categoria pode ter no máximo 500 caracteres.")]
    public string? Description { get; set; }
}
