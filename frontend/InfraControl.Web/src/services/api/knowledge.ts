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
  /** Vínculos F10-T004 — só populados no GET/{id}; a busca devolve []. */
  deviceIds: string[];
  incidentIds: string[];
  serviceIds: string[];
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

export const ARTICLE_STATUS_DRAFT = 0;
export const ARTICLE_STATUS_PUBLISHED = 1;
export const ARTICLE_STATUS_ARCHIVED = 2;

export interface ArticleFilters {
  term?: string;
  page?: number;
  pageSize?: number;
}

/** Busca com `term` (substring no repositório); pageSize clampado 1..100. */
export async function listArticles(
  filters: ArticleFilters = {},
): Promise<Paged<KnowledgeArticle>> {
  const params = new URLSearchParams();
  if (filters.term) params.set("term", filters.term);
  if (filters.page) params.set("page", String(filters.page));
  if (filters.pageSize) params.set("pageSize", String(filters.pageSize));

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
 * envelope.
 */
export function createArticle(input: {
  title: string;
  content: string;
  categoryId?: string;
}): Promise<KnowledgeArticle | null> {
  return api.post<KnowledgeArticle>("/knowledge/articles", {
    title: input.title,
    content: input.content,
    categoryId: input.categoryId || undefined,
  });
}
