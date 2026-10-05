using System.ComponentModel.DataAnnotations;

namespace InfraControl.Api.ViewModels.Admin;

public sealed record SetUserStatusViewModel(bool Active);

public sealed record UpdateUserViewModel
{
    [Required(ErrorMessage = "O nome de exibição é obrigatório.")]
    public string DisplayName { get; init; } = string.Empty;

    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "O e-mail deve ser válido.")]
    public string Email { get; init; } = string.Empty;
}
