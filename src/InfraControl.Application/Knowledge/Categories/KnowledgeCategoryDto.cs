namespace InfraControl.Application.Knowledge.Categories;

public sealed record KnowledgeCategoryDto(
    Guid Id,
    string Name,
    string? Description);
