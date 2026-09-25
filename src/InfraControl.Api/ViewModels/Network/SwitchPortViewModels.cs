namespace InfraControl.Api.ViewModels.Network;

public sealed record CreateSwitchPortViewModel(string Name, string? Description = null);

/// <summary>PUT completo: linkUp null significa "voltar a não observado" (F4 repreenche).</summary>
public sealed record UpdateSwitchPortViewModel(string Name, string? Description, bool? LinkUp);
