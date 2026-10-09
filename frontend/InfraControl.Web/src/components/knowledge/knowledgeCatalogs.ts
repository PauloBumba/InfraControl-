/**
 * F24-T001/K1 — catálogo dos 14 tipos de Knowledge da spec docs/32 §5,
 * espelhado num arquivo único (mesmo padrão de `taskCatalogs.ts`): a
 * chave é o NOME do enum `KnowledgeArticleType` (viaja como string no
 * DTO — o MVC não registra JsonStringEnumConverter) e o valor é o
 * rótulo pt-BR exato da spec. Nada de rótulo espalhado pela UI.
 */
export const KNOWLEDGE_TYPE_LABELS: Record<string, string> = {
  Procedure: "Procedimento",
  Manual: "Manual",
  Guide: "Guia",
  Faq: "FAQ",
  KnownSolution: "Solução conhecida",
  Configuration: "Configuração",
  TechnicalDocumentation: "Documentação técnica",
  BestPractices: "Boas práticas",
  Policy: "Política",
  InternalStandard: "Norma interna",
  Troubleshooting: "Troubleshooting",
  SystemKnowledge: "Conhecimento de sistema",
  InfrastructureKnowledge: "Conhecimento de infraestrutura",
  OperationalKnowledge: "Conhecimento operacional",
};

/** Opções do select na ordem da spec. */
export const KNOWLEDGE_TYPE_OPTIONS: { value: string; label: string }[] =
  Object.entries(KNOWLEDGE_TYPE_LABELS).map(([value, label]) => ({
    value,
    label,
  }));

/** Rótulo do tipo; sem tipo (ou valor desconhecido de versão velha) → null. */
export function knowledgeTypeLabel(type: string | null | undefined): string | null {
  if (!type) return null;
  return KNOWLEDGE_TYPE_LABELS[type] ?? type;
}
