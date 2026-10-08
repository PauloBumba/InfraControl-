namespace InfraControl.Application.Knowledge.Articles;

/// <summary>
/// Origem do artigo no payload (F24-T004/K4, spec docs/32 §9) —
/// referência por tipo + id (o §9 desenha "Task #0042"; resolver
/// título/nome do alvo é backlog quando existir página de detalhe).
/// `Type` é o nome do enum em string (mesmo critério do `Type` do
/// artigo — decisão K1): "Task" | "Pdi" | "Incident".
/// </summary>
public sealed record KnowledgeArticleOriginDto(string Type, Guid Id);
