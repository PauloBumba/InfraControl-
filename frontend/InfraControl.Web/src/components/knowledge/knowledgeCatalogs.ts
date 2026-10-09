<<<<<<< HEAD
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
=======
import {
  ARTICLE_STATUS_ARCHIVED,
  ARTICLE_STATUS_APPROVED,
  ARTICLE_STATUS_DRAFT,
  ARTICLE_STATUS_IN_REVIEW,
  ARTICLE_STATUS_OBSOLETE,
  ARTICLE_STATUS_PUBLISHED,
  ARTICLE_STATUS_REJECTED,
  ARTICLE_STATUS_UPDATED,
} from "@/services/api/knowledge";

/**
 * F24-T001/K1 — catálogo único dos 14 tipos de Knowledge (docs/32 §5),
 * espelhando o padrão de `components/tasks/taskCatalogs.ts`: o valor viaja
 * igual ao canônico do domínio (pt-BR) — o backend valida por conjunto
 * fechado (KnowledgeArticle.KnownTypes) e o case-insensitive é normalizado
 * no servidor.
 */
export const KNOWLEDGE_TYPES: readonly string[] = [
  "Procedimento",
  "Manual",
  "Guia",
  "FAQ",
  "Solução conhecida",
  "Configuração",
  "Documentação técnica",
  "Boas práticas",
  "Política",
  "Norma interna",
  "Troubleshooting",
  "Conhecimento de sistema",
  "Conhecimento de infraestrutura",
  "Conhecimento operacional",
];

export const MAX_TAGS = 10;
export const TAG_MAX_LENGTH = 30;

/** Artigo legado (type null, anterior ao K1) — rótulo de placeholder. */
export function typeLabel(type: string | null): string {
  return type ?? "Sem tipo";
}

export const typeOptions: { value: string; label: string }[] =
  KNOWLEDGE_TYPES.map((type) => ({ value: type, label: type }));

/**
 * F24-T002/K2 (ADR-039 §1): os 8 estados do workflow de docs/32 §7 —
 * rótulos pt-BR espelhando KnowledgeArticleStatusExtensions.ToPtBrLabel
 * (o status viaja como número no JSON).
 */
export const articleStatusConfig: Record<
  number,
  { label: string; bg: string; color: string }
> = {
  [ARTICLE_STATUS_DRAFT]: { label: "Rascunho", bg: "#fef3c7", color: "#d97706" },
  [ARTICLE_STATUS_IN_REVIEW]: { label: "Em revisão", bg: "#dbeafe", color: "#2563eb" },
  [ARTICLE_STATUS_APPROVED]: { label: "Aprovada", bg: "#ede9fe", color: "#7c3aed" },
  [ARTICLE_STATUS_PUBLISHED]: { label: "Publicada", bg: "#dcfce7", color: "#16a34a" },
  [ARTICLE_STATUS_UPDATED]: { label: "Atualizada", bg: "#ccfbf1", color: "#0d9488" },
  [ARTICLE_STATUS_ARCHIVED]: { label: "Arquivada", bg: "#f1f5f9", color: "#475569" },
  [ARTICLE_STATUS_OBSOLETE]: { label: "Obsoleta", bg: "#ffedd5", color: "#ea580c" },
  [ARTICLE_STATUS_REJECTED]: { label: "Rejeitada", bg: "#ffe4e6", color: "#e11d48" },
};
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
