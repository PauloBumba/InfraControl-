import { api } from "./client";

/**
 * F17-T004 — conexão da KnowledgePage ao backend (contrato real de
 * `GET/POST /api/v1/knowledge/articles` + categorias em
 * `api/v1/knowledge/categories`). Espelha `KnowledgeArticleDto` e
 * `KnowledgeCategoryDto`.
 *
 * Atenção de contrato: `status` é exposto como **enum .NET** no DTO e o
 * pipeline MVC NÃO registra `JsonStringEnumConverter` (só o outbox tem) —
 * o JSON traz o valor NUMÉRICO (0/1/2), não "Draft". Os rótulos pt-BR
 * ficam na página; aqui fixamos as constantes.
 */
export interface KnowledgeArticle {
  id: string;
  title: string;
  content: string;
  categoryId: string | null;
  /** 0=Draft, 1=Published, 2=Archived (KnowledgeArticleStatus). */
  status: number;
  /** F24-T001/K1 — nome do enum (KnowledgeArticleType); null = sem tipo. */
  type: string | null;
  /** F24-T001/K1 — identificação (spec docs/32 §6): descrição, tags e
   * pessoas; nomes já resolvidos pelo backend (leitura sem /admin/users). */
  description: string | null;
  tags: string[];
  authorId: string | null;
  authorName: string | null;
  responsibleId: string | null;
  responsibleName: string | null;
  /** F24-T003/K3 — versão corrente ("1.0" na criação); histórico em GET {id}/versions. */
  version: string;
  /**
   * F24-T004/K4 — origens (docs/32 §9): tipo + id, vindo em lote na
   * busca e no detalhe. Type = "Task" | "Pdi" | "Incident".
   */
  origins: { type: string; id: string }[];
  /**
   * F24-T005/K5 — governança (docs/32 §6/§12): agenda de revisão
   * periódica. `pendingReview` = "Knowledge necessita de revisão".
   */
  nextReviewAt: string | null;
  reviewIntervalDays: number | null;
  reviewResponsibleId: string | null;
  reviewResponsibleName: string | null;
  pendingReview: boolean;
  /** Vínculos F10-T004 — só populados no GET/{id}; a busca devolve []. */
  deviceIds: string[];
  incidentIds: string[];
  serviceIds: string[];
  /** F24-T001/K1 — null = artigo legado (criado antes do K1). */
  type: string | null;
  description: string | null;
  authorId: string | null;
  responsibleId: string | null;
  /** Tags normalizadas (lowercase) — substituição total no PUT. */
  tags: string[];
  /** Resolvidos nas queries Search/Get (join em DomainUsers). */
  authorName?: string | null;
  responsibleName?: string | null;
}

export interface KnowledgeCategory {
  id: string;
  name: string;
  description: string | null;
}

/** `PagedResult<T>` — o campo `data` do envelope. */
export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// F24-T002/K2 (ADR-034, spec docs/32 §7): 8 status. Os 3 primeiros
// números são o contrato legado do F10 (estáveis); os novos entram
// 3–7 — o MVC serializa enum como número (sem JsonStringEnumConverter).
export const ARTICLE_STATUS_DRAFT = 0;
export const ARTICLE_STATUS_PUBLISHED = 1;
export const ARTICLE_STATUS_ARCHIVED = 2;
export const ARTICLE_STATUS_IN_REVIEW = 3;
export const ARTICLE_STATUS_APPROVED = 4;
export const ARTICLE_STATUS_UPDATED = 5;
export const ARTICLE_STATUS_OBSOLETE = 6;
export const ARTICLE_STATUS_REJECTED = 7;

export interface ArticleFilters {
  term?: string;
  page?: number;
  pageSize?: number;
  /** F24-T005/K5 — só artigos com revisão vencida (docs/32 §12). */
  pendingReview?: boolean;
  /**
   * F24-T007/K7 — busca avançada (docs/32 §13): filtros estruturados
   * AND sobre campos do modelo. `type` = nome do enum; `tag` casa com a
   * normalização do domínio (trim+lowercase). "Sistema"/"setor" da spec
   * não existem no modelo e não têm filtro.
   */
  categoryId?: string;
  authorId?: string;
  type?: string;
  tag?: string;
}

/** Busca com `term` (substring no repositório); pageSize clampado 1..100. */
export async function listArticles(
  filters: ArticleFilters = {},
): Promise<Paged<KnowledgeArticle>> {
  const params = new URLSearchParams();
  if (filters.term) params.set("term", filters.term);
  if (filters.page) params.set("page", String(filters.page));
  if (filters.pageSize) params.set("pageSize", String(filters.pageSize));
  if (filters.pendingReview) params.set("pendingReview", "true");
  if (filters.categoryId) params.set("categoryId", filters.categoryId);
  if (filters.authorId) params.set("authorId", filters.authorId);
  if (filters.type) params.set("type", filters.type);
  if (filters.tag) params.set("tag", filters.tag);

  const query = params.toString();
  const data = await api.get<Paged<KnowledgeArticle>>(
    `/knowledge/articles${query ? `?${query}` : ""}`,
  );
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
}

/**
 * Categorias para o chip do card e o select da criação. A busca não tem
 * "todos" — trazemos a primeira página no teto (100) e resolvemos
 * `categoryId` → nome localmente (o DTO de artigo não traz o nome).
 */
export async function listCategories(): Promise<Paged<KnowledgeCategory>> {
  const data = await api.get<Paged<KnowledgeCategory>>(
    "/knowledge/categories?page=1&pageSize=100",
  );
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: 100 };
}

/**
 * Criação de artigo (Operate + antiforgery). As validações `[Required]`/
 * `[MaxLength]` do ViewModel já nascem em pt-BR e chegam no `errors` do
 * envelope. F24-T001/K1: identificação opcional (tipo/descrição/tags/
 * responsável); autor é o usuário corrente, definido no servidor.
 */
export function createArticle(input: {
  title: string;
  content: string;
  categoryId?: string;
  /** Nome do enum KnowledgeArticleType (catálogo em knowledgeCatalogs). */
  type?: string;
  description?: string;
  tags?: string[];
  responsibleId?: string;
}): Promise<KnowledgeArticle | null> {
  return api.post<KnowledgeArticle>("/knowledge/articles", {
    title: input.title,
    content: input.content,
    categoryId: input.categoryId || undefined,
    type: input.type || undefined,
    description: input.description || undefined,
    tags: input.tags?.length ? input.tags : undefined,
    responsibleId: input.responsibleId || undefined,
  });
}

/** Atualiza artigo (PUT /knowledge/articles/{id}; substituição total de tags). */
export async function updateArticle(
  id: string,
  input: {
    title: string;
    content: string;
    categoryId?: string;
    type: string;
    description?: string;
    tags?: string[];
    responsibleId?: string;
  },
): Promise<null> {
  return api.put<null>(`/knowledge/articles/${id}`, {
    title: input.title,
    content: input.content,
    categoryId: input.categoryId || null,
    type: input.type,
    description: input.description || null,
    tags: input.tags ?? [],
    responsibleId: input.responsibleId || null,
  });
}

/** Exclui artigo (DELETE /knowledge/articles/{id}; ciclo Draft→Published→Archived). */
export async function deleteArticle(id: string): Promise<null> {
  return api.delete<null>(`/knowledge/articles/${id}`);
}

/**
 * Transição de status (POST /knowledge/articles/{id}/status). O handler
 * valida a transição e devolve 400 pt-BR com as opções válidas.
 */
export async function changeArticleStatus(
  id: string,
  status: "Draft" | "Published" | "Archived",
): Promise<null> {
  return api.post<null>(`/knowledge/articles/${id}/status`, { status });
}

/** Cria categoria (Operate). */
export function createCategory(input: {
  name: string;
  description?: string;
}): Promise<KnowledgeCategory | null> {
  return api.post<KnowledgeCategory>("/knowledge/categories", {
    name: input.name,
    description: input.description || null,
  });
}

/** Atualiza categoria (PUT /knowledge/categories/{id}). */
export async function updateCategory(
  id: string,
  input: { name: string; description?: string },
): Promise<null> {
  return api.put<null>(`/knowledge/categories/${id}`, {
    name: input.name,
    description: input.description || null,
  });
}

/** Exclui categoria (DELETE /knowledge/categories/{id}; 409 com artigos). */
export async function deleteCategory(id: string): Promise<null> {
  return api.delete<null>(`/knowledge/categories/${id}`);
}
