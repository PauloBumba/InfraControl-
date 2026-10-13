namespace InfraControl.Application.Projects;

// CRUD-Projetos (2026-10-12) — DTO de evidência técnica (ADR-043 · docs/38
// §3.4). Espelha o `PdiEvidenceDto` sem os nomes de usuário (o repositório
// de projetos não expõe lookup de nome — devolve só os ids; se a UI precisar
// de nomes, é incremento próprio).
public sealed record ProjectEvidenceDto(
    Guid Id,
    Guid ProjectId,
    string Type,
    string Status,
    string? Description,
    string FileName,
    string ContentType,
    long FileSize,
    Guid CreatedBy,
    DateTime CreatedAt,
    Guid? ReviewedBy,
    DateTime? ReviewedAt,
    string? RejectionReason);
