/**
 * F24-T001/K1 — catálogo dos 14 tipos de Knowledge da spec docs/32 §5,
 * espelhado num arquivo único (mesmo padrão de `taskCatalogs.ts`): a
 * chave é o NOME do enum `KnowledgeArticleType` (viaja como string no
 * DTO — o MVC não registra JsonStringEnumConverter) e o valor é o
 * rótulo pt-BR exato da spec. Nada de rótulo espalhado pela UI.
 */
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

/**
 * F24-K2 (ADR-034, docs/32 §7) — matriz de 16 pares de transição, um
 * espelho EXATO do `KnowledgeArticle.IsValidTransition` no domínio. O
 * seletor de status da UI só oferece o que esta função devolve; o backend
 * continua sendo a autoridade (409 fora da matriz). Se a matriz do
 * domínio mudar, esta tem que mudar junto — os testes travam os 16 pares.
 */
const NEXT_STATUSES: Record<number, number[]> = {
  // Cadeia da §7: rascunho → revisão → aprovação → publicação.
  [ARTICLE_STATUS_DRAFT]: [ARTICLE_STATUS_IN_REVIEW],
  [ARTICLE_STATUS_IN_REVIEW]: [
    ARTICLE_STATUS_APPROVED,
    ARTICLE_STATUS_UPDATED,
    ARTICLE_STATUS_REJECTED,
    ARTICLE_STATUS_DRAFT,
  ],
  [ARTICLE_STATUS_APPROVED]: [ARTICLE_STATUS_PUBLISHED, ARTICLE_STATUS_DRAFT],
  // Revisão do publicado + especiais.
  [ARTICLE_STATUS_PUBLISHED]: [ARTICLE_STATUS_IN_REVIEW, ARTICLE_STATUS_ARCHIVED, ARTICLE_STATUS_OBSOLETE],
  [ARTICLE_STATUS_UPDATED]: [ARTICLE_STATUS_IN_REVIEW, ARTICLE_STATUS_ARCHIVED, ARTICLE_STATUS_OBSOLETE],
  // Estados de saída restauram/retrabalham no rascunho.
  [ARTICLE_STATUS_ARCHIVED]: [ARTICLE_STATUS_DRAFT],
  [ARTICLE_STATUS_OBSOLETE]: [ARTICLE_STATUS_DRAFT],
  [ARTICLE_STATUS_REJECTED]: [ARTICLE_STATUS_DRAFT],
};

/**
 * Transições válidas a partir de um status. Status desconhecido (valor
 * velho/defensivo) devolve vazio — a UI não oferece nada em vez de oferecer
 * algo que o backend vai recusar.
 */
export function knowledgeNextStatuses(from: number): number[] {
  return NEXT_STATUSES[from] ?? [];
}

/**
 * F24-K2 — rótulo/cor dos 8 status (docs/32 §7, rótulos verbatim). Antes
 * vivia embutido na KnowledgePage; sai da página para o catálogo porque o
 * seletor de transição e as 3 visões (tabela/grid/quadro) precisam do mesmo
 * lugar único de verdade — mesmo precedente de `taskCatalogs.ts`.
 */
export const articleStatusConfig: Record<number, { label: string; bg: string; color: string }> = {
  [ARTICLE_STATUS_DRAFT]: { label: "Rascunho", bg: "#fef3c7", color: "#d97706" },
  [ARTICLE_STATUS_IN_REVIEW]: { label: "Em revisão", bg: "#ffedd5", color: "#ea580c" },
  [ARTICLE_STATUS_APPROVED]: { label: "Aprovada", bg: "#ccfbf1", color: "#0d9488" },
  [ARTICLE_STATUS_PUBLISHED]: { label: "Publicada", bg: "#dcfce7", color: "#16a34a" },
  [ARTICLE_STATUS_UPDATED]: { label: "Atualizada", bg: "#e0f2fe", color: "#0284c7" },
  [ARTICLE_STATUS_ARCHIVED]: { label: "Arquivada", bg: "#f1f5f9", color: "#475569" },
  [ARTICLE_STATUS_OBSOLETE]: { label: "Obsoleta", bg: "#e2e8f0", color: "#64748b" },
  [ARTICLE_STATUS_REJECTED]: { label: "Rejeitada", bg: "#fee2e2", color: "#dc2626" },
};

/** Rótulo defensivo: status sem config cai no número cru (nunca quebra). */
export function articleStatusLabel(status: number): string {
  return articleStatusConfig[status]?.label ?? String(status);
}

/**
 * F24-T008 — categorias visuais no topo da página. O `KnowledgeCategoryDto`
 * só tem id/nome/descrição (sem cor — campo novo exigiria migration/ADR),
 * então a UI deriva uma cor ESTÁVEL do nome: mesmo nome = mesma cor em
 * qualquer render/sessão, o que basta para reconhecer a categoria de longe.
 */
const CATEGORY_ACCENTS = [
  { bg: "#eff6ff", color: "#1d4ed8", border: "#bfdbfe" },
  { bg: "#f0fdf4", color: "#15803d", border: "#bbf7d0" },
  { bg: "#fef3c7", color: "#b45309", border: "#fde68a" },
  { bg: "#fdf2f8", color: "#be185d", border: "#fbcfe8" },
  { bg: "#f5f3ff", color: "#6d28d9", border: "#ddd6fe" },
  { bg: "#ecfeff", color: "#0e7490", border: "#a5f3fc" },
] as const;

export interface KnowledgeCategoryAccent {
  bg: string;
  color: string;
  border: string;
}

/** Hash determinístico simples (djb2-like) → palette fixa. */
export function knowledgeCategoryAccent(name: string): KnowledgeCategoryAccent {
  let hash = 5381;
  for (let index = 0; index < name.length; index += 1) {
    hash = ((hash * 33) ^ name.charCodeAt(index)) >>> 0;
  }
  return (
    CATEGORY_ACCENTS[hash % CATEGORY_ACCENTS.length] ?? {
      bg: "#eff6ff",
      color: "#1d4ed8",
      border: "#bfdbfe",
    }
  );
}
