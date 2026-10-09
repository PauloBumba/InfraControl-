import { useState, type FormEvent } from "react";
import { Column } from "primereact/column";
import { Can } from "@/components/auth/Can";
import { Button } from "@/components/ui/Button";
import { Pagination } from "@/components/ui/Pagination";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { FormField, FormError } from "@/components/ui/FormField";
import { PageHeader } from "@/components/ui/PageHeader";
import { SearchInput } from "@/components/ui/SearchInput";
import { StatusChip } from "@/components/ui/StatusChip";
import { SelectField } from "@/components/ui/SelectField";
import { Dialog, ConfirmDialog } from "@/components/ui/Dialog";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { listUsers, type AdminUser } from "@/services/api/adminUsers";
import {
  MAX_TAGS,
  TAG_MAX_LENGTH,
  articleStatusConfig,
  typeOptions,
} from "@/components/knowledge/knowledgeCatalogs";
import {
<<<<<<< HEAD
  KNOWLEDGE_TYPE_OPTIONS,
  knowledgeTypeLabel,
} from "@/components/knowledge/knowledgeCatalogs";
import { listUsers, type AdminUser } from "@/services/api/adminUsers";
import {
  ARTICLE_STATUS_ARCHIVED,
  ARTICLE_STATUS_APPROVED,
  ARTICLE_STATUS_DRAFT,
  ARTICLE_STATUS_IN_REVIEW,
  ARTICLE_STATUS_OBSOLETE,
  ARTICLE_STATUS_PUBLISHED,
  ARTICLE_STATUS_REJECTED,
  ARTICLE_STATUS_UPDATED,
=======
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
  createArticle,
  createCategory,
  changeArticleStatus,
  deleteArticle,
  deleteCategory,
  listArticles,
  listCategories,
  updateArticle,
  updateCategory,
  type KnowledgeArticle,
  type KnowledgeCategory,
} from "@/services/api/knowledge";
import {
  useCreateMutation,
  useDeleteMutation,
  useListQuery,
  usePagedQuery,
  useUpdateMutation,
  mutationErrorMessage,
} from "@/hooks/queries";

/**
<<<<<<< HEAD
 * F17-T004 — KnowledgePage conectada ao backend (substitui o mock de
 * `data/dashboardData`). Mudanças de contrato: `category` é um `categoryId`
 * (resolvido contra GET /knowledge/categories — o DTO de artigo não traz o
 * nome), `lastUpdate` e `tags` não existem no `KnowledgeArticleDto` (saíram;
 * entrou o `status`), `status` viaja como **número** (enum sem
 * JsonStringEnumConverter no MVC) e a busca de `term` é real (substring no
 * repositório) — aplicada no submit. Rascunhos (Draft) o backend já esconde
 * de quem não escreve; nada de gating extra aqui.
 *
 * F24-T001/K1: a identificação da spec docs/32 §5–§6 voltou ao DTO —
 * tipo (catálogo em `knowledgeCatalogs.ts`), descrição, tags, autor e
 * responsável (nomes já resolvidos pelo backend); o formulário de
 * criação ganhou os campos e o card mostra tudo.
=======
 * F17-T004 + ADR-030 Fase 5 — KnowledgePage com React Query e visões
 * tabela/grid/kanban (preferência persistida por entidade). Kanban de
 * artigos agrupa por status (8 estados de docs/32 §7); transição de status
 * usa `POST /articles/{id}/status` (Draft/Published/Archived — 400 pt-BR
 * com as opções válidas vira banner). CRUD de artigos completo (create
 * duplo do form + PUT + DELETE) e abas Artigos/Categorias com CRUD de
 * categorias (409 com artigos vinculados). Escrita atrás de `Can operate`
 * (exibição; policy Operate + antiforgery continuam sendo a autoridade).
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
 */

const PAGE_SIZE = 20;

<<<<<<< HEAD
// F24-T002/K2 (ADR-034): rótulos verbatim da spec docs/32 §7 —
// "Publicada"/"Arquivada" feminino mudou do legado "Publicado"/
// "Arquivado". Números = knowledge.ts (0–2 legados, 3–7 novos).
// F24-T004/K4: rótulos das origens (docs/32 §9 — "Chamados" = Incident
// na API; PDI em caixa alta). Chave = enum em string no dto.
const originLabels: Record<string, string> = {
  Task: "Task",
  Pdi: "PDI",
  Incident: "Chamado",
};

const statusConfig: Record<number, { label: string; bg: string; color: string }> = {
  [ARTICLE_STATUS_DRAFT]: { label: "Rascunho", bg: "#fef3c7", color: "#d97706" },
  [ARTICLE_STATUS_IN_REVIEW]: { label: "Em revisão", bg: "#ffedd5", color: "#ea580c" },
  [ARTICLE_STATUS_APPROVED]: { label: "Aprovada", bg: "#ccfbf1", color: "#0d9488" },
  [ARTICLE_STATUS_PUBLISHED]: { label: "Publicada", bg: "#dcfce7", color: "#16a34a" },
  [ARTICLE_STATUS_UPDATED]: { label: "Atualizada", bg: "#e0f2fe", color: "#0284c7" },
  [ARTICLE_STATUS_ARCHIVED]: { label: "Arquivada", bg: "#f1f5f9", color: "#475569" },
  [ARTICLE_STATUS_OBSOLETE]: { label: "Obsoleta", bg: "#e2e8f0", color: "#64748b" },
  [ARTICLE_STATUS_REJECTED]: { label: "Rejeitada", bg: "#fee2e2", color: "#dc2626" },
};
=======
/** Colunas do kanban = os 8 estados do workflow (rótulos pt-BR). */
const statusColumns = Object.entries(articleStatusConfig).map(
  ([status, config]) => ({ key: status, label: config.label }),
);
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c

/** Transições aceitas pelo endpoint de status (contrato do service). */
const statusTransitionOptions = [
  { value: "Draft", label: "Rascunho" },
  { value: "Published", label: "Publicada" },
  { value: "Archived", label: "Arquivada" },
];

interface ArticleFormState {
  mode: "create" | "edit";
  target: KnowledgeArticle | null;
}

const emptyArticleForm = {
  title: "",
  content: "",
  categoryId: "",
  type: "",
  description: "",
  tags: [] as string[],
  tagInput: "",
  responsibleId: "",
};

const emptyCategoryForm = { name: "", description: "" };

export function KnowledgePage() {
  const [tab, setTab] = useState<"articles" | "categories">("articles");
  const [articleView, setArticleView] = useEntityViewMode("knowledge", "grid");
  const [categoryView, setCategoryView] = useEntityViewMode("knowledgeCategories", "table");

  const [page, setPage] = useState(1);
  const [term, setTerm] = useState("");
  const [termInput, setTermInput] = useState("");
  // F24-T005/K5 — filtro "pendentes de revisão" (docs/32 §12).
  const [pendingOnly, setPendingOnly] = useState(false);
  // F24-T007/K7 — busca avançada (docs/32 §13): filtros estruturados
  // sobre os campos que existem no modelo.
  const [categoryIdFilter, setCategoryIdFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [authorFilter, setAuthorFilter] = useState("");
  const [tagFilter, setTagFilter] = useState("");

<<<<<<< HEAD
  const [creating, setCreating] = useState(false);
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");
  const [categoryId, setCategoryId] = useState("");
  // F24-T001/K1 — identificação (docs/32 §6).
  const [articleType, setArticleType] = useState("");
  const [description, setDescription] = useState("");
  const [tagsInput, setTagsInput] = useState("");
  const [responsibleId, setResponsibleId] = useState("");
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [saving, setSaving] = useState(false);
=======
  const articles = usePagedQuery<KnowledgeArticle>(["knowledge", page, term], () =>
    listArticles({ term: term || undefined, page, pageSize: PAGE_SIZE }),
  );
  const categories = useListQuery<KnowledgeCategory>(["knowledgeCategories"], () =>
    listCategories().then((result) => result.items),
  );
  const users = useListQuery<AdminUser>(["users"], () =>
    // 403 de Viewer em /admin/users silencia — select fica só com "Sem".
    listUsers().catch(() => [] as AdminUser[]),
  );

  const [notice, setNotice] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const [articleFormOpen, setArticleFormOpen] = useState(false);
  const [articleForm, setArticleForm] = useState<ArticleFormState>({ mode: "create", target: null });
  const [values, setValues] = useState(emptyArticleForm);
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
  const [formError, setFormError] = useState<string | null>(null);
  const [deleteArticleTarget, setDeleteArticleTarget] = useState<KnowledgeArticle | null>(null);

<<<<<<< HEAD
  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setPageData(
        await listArticles({
          term: term || undefined,
          page,
          pageSize: PAGE_SIZE,
          pendingReview: pendingOnly || undefined,
          // F24-T007/K7 — busca avançada (docs/32 §13).
          categoryId: categoryIdFilter || undefined,
          type: typeFilter || undefined,
          authorId: authorFilter || undefined,
          tag: tagFilter || undefined,
        }),
      );
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, [term, page, pendingOnly, categoryIdFilter, typeFilter, authorFilter, tagFilter]);

  useEffect(() => {
    void load();
  }, [load]);

  // Categorias uma única vez: servem para o chip (categoryId → nome) e para
  // o select da criação. Falha aqui degrada para "Sem categoria"/select
  // vazio — a lista de artigos segue independente.
  useEffect(() => {
    listCategories()
      .then((result) => setCategories(result.items))
      .catch(() => undefined);
  }, []);
=======
  const [categoryFormOpen, setCategoryFormOpen] = useState(false);
  const [categoryForm, setCategoryForm] = useState<{ mode: "create" | "edit"; target: KnowledgeCategory | null }>({
    mode: "create",
    target: null,
  });
  const [categoryValues, setCategoryValues] = useState(emptyCategoryForm);
  const [deleteCategoryTarget, setDeleteCategoryTarget] = useState<KnowledgeCategory | null>(null);

  const createArticleMutation = useCreateMutation({
    invalidate: ["knowledge"],
    mutationFn: (input: Parameters<typeof createArticle>[0]) => createArticle(input),
    onSuccess: () => setNotice("Artigo criado como rascunho — publique pela troca de status."),
  });
  const updateArticleMutation = useUpdateMutation({
    invalidate: ["knowledge"],
    mutationFn: (input: { id: string } & Parameters<typeof updateArticle>[1]) => {
      const { id, ...rest } = input;
      return updateArticle(id, rest);
    },
    onSuccess: () => setNotice("Artigo atualizado."),
  });
  const deleteArticleMutation = useDeleteMutation({
    invalidate: ["knowledge"],
    mutationFn: (id: string) => deleteArticle(id),
    onSuccess: () => setNotice("Artigo excluído."),
  });
  const statusMutation = useUpdateMutation({
    invalidate: ["knowledge"],
    mutationFn: (input: { id: string; status: "Draft" | "Published" | "Archived" }) =>
      changeArticleStatus(input.id, input.status),
    onSuccess: (_data, vars) => {
      setActionError(null);
      setNotice(`Status alterado para ${statusTransitionOptions.find((o) => o.value === vars.status)?.label ?? vars.status}.`);
    },
    onError: (error) => setActionError(mutationErrorMessage(error)),
  });

  const createCategoryMutation = useCreateMutation({
    invalidate: ["knowledgeCategories"],
    mutationFn: (input: { name: string; description?: string }) => createCategory(input),
    onSuccess: () => setNotice("Categoria criada."),
  });
  const updateCategoryMutation = useUpdateMutation({
    invalidate: ["knowledgeCategories"],
    mutationFn: (input: { id: string; name: string; description?: string }) =>
      updateCategory(input.id, { name: input.name, description: input.description }),
    onSuccess: () => setNotice("Categoria atualizada."),
  });
  const deleteCategoryMutation = useDeleteMutation({
    invalidate: ["knowledgeCategories"],
    mutationFn: (id: string) => deleteCategory(id),
    onSuccess: () => setNotice("Categoria excluída."),
    onError: (error) => setActionError(mutationErrorMessage(error)),
  });

  // Tags (K1): Enter/botão adiciona; validação local espelha o domínio
  // (10 tags, 30 chars, dedupe case-insensitive) — o servidor continua
  // sendo a autoridade.
  function addTag() {
    const tag = values.tagInput.trim();
    if (!tag) return;
    if (tag.length > TAG_MAX_LENGTH) {
      setFormError(`A tag pode ter no máximo ${TAG_MAX_LENGTH} caracteres.`);
      return;
    }
    if (values.tags.length >= MAX_TAGS) {
      setFormError(`O artigo pode ter no máximo ${MAX_TAGS} tags.`);
      return;
    }
    if (values.tags.some((existing) => existing.toLowerCase() === tag.toLowerCase())) {
      setValues((current) => ({ ...current, tagInput: "" }));
      setFormError(null);
      return;
    }
    setValues((current) => ({ ...current, tags: [...current.tags, tag], tagInput: "" }));
    setFormError(null);
  }
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c

  // F24-T001/K1: select de responsável (policy Operate — mesma policy do
  // form). Falha aqui deixa o select vazio; o campo é opcional.
  useEffect(() => {
    listUsers()
      .then(setUsers)
      .catch(() => setUsers([]));
  }, []);

  function applyTerm(event: FormEvent) {
    event.preventDefault();
    setTerm(termInput.trim());
    setPage(1);
  }

<<<<<<< HEAD
  // F24-T007/K7 — um lugar só para desligar qualquer filtro (o botão
  // "Limpar" do header passa a valer para todos).
  const anyFilterActive = Boolean(
    term || pendingOnly || categoryIdFilter || typeFilter || authorFilter || tagFilter,
  );

  function clearFilters() {
    setTermInput("");
    setTerm("");
    setPendingOnly(false);
    setCategoryIdFilter("");
    setTypeFilter("");
    setAuthorFilter("");
    setTagFilter("");
    setPage(1);
  }

  /** Visual de chip de filtro ativo (mesmo padrão do "Revisões"). */
  const filterChipStyle = (active: boolean) => ({
    padding: "5px 10px",
    ...(active
      ? { background: "#eff6ff", color: "#1d4ed8", borderColor: "#bfdbfe" }
      : {}),
  });

  async function submitCreate(event: FormEvent) {
=======
  function openArticleCreate() {
    setArticleForm({ mode: "create", target: null });
    setValues(emptyArticleForm);
    setFormError(null);
    setArticleFormOpen(true);
  }

  function openArticleEdit(article: KnowledgeArticle) {
    setArticleForm({ mode: "edit", target: article });
    setValues({
      title: article.title,
      content: article.content,
      categoryId: article.categoryId ?? "",
      type: article.type ?? "",
      description: article.description ?? "",
      tags: [...article.tags],
      tagInput: "",
      responsibleId: article.responsibleId ?? "",
    });
    setFormError(null);
    setArticleFormOpen(true);
  }

  async function submitArticleForm(event: FormEvent) {
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
    event.preventDefault();
    setFormError(null);
    const title = values.title.trim();
    const content = values.content.trim();
    if (!title) {
      setFormError("Informe o título do artigo.");
      return;
    }
    if (!content) {
      setFormError("Informe o conteúdo do artigo.");
      return;
    }
    if (!values.type) {
      setFormError("Selecione o tipo do artigo.");
      return;
    }

    const input = {
      title,
      content,
      categoryId: values.categoryId || undefined,
      type: values.type,
      description: values.description.trim() || undefined,
      tags: values.tags,
      responsibleId: values.responsibleId || undefined,
    };

    try {
      if (articleForm.mode === "create") {
        await createArticleMutation.mutateAsync(input);
      } else if (articleForm.target) {
        await updateArticleMutation.mutateAsync({ id: articleForm.target.id, ...input });
      }
      setArticleFormOpen(false);
    } catch (caught) {
      setFormError(mutationErrorMessage(caught as Error));
    }
  }

  async function confirmDeleteArticle() {
    if (!deleteArticleTarget) return;
    try {
      await deleteArticleMutation.mutateAsync(deleteArticleTarget.id);
      setDeleteArticleTarget(null);
      setActionError(null);
    } catch (caught) {
      setActionError(mutationErrorMessage(caught as Error));
    }
  }

  async function submitCategoryForm(event: FormEvent) {
    event.preventDefault();
    const name = categoryValues.name.trim();
    if (!name) {
      setFormError("O nome da categoria é obrigatório.");
      return;
    }
    setFormError(null);
    try {
<<<<<<< HEAD
      // F24-T001/K1: tags como chips por vírgula (o domínio normaliza
      // trim/lowercase/dedup e valida os limites em pt-BR → 400).
      const tags = tagsInput
        .split(",")
        .map((tag) => tag.trim())
        .filter(Boolean);
      await createArticle({
        title: trimmedTitle,
        content: trimmedContent,
        categoryId: categoryId || undefined,
        type: articleType || undefined,
        description: description.trim() || undefined,
        tags: tags.length ? tags : undefined,
        responsibleId: responsibleId || undefined,
      });
      setCreating(false);
      setTitle("");
      setContent("");
      setCategoryId("");
      setArticleType("");
      setDescription("");
      setTagsInput("");
      setResponsibleId("");
      setNotice("Artigo criado como rascunho — publique pela troca de status.");
      await load();
=======
      if (categoryForm.mode === "create") {
        await createCategoryMutation.mutateAsync({ name, description: categoryValues.description });
      } else if (categoryForm.target) {
        await updateCategoryMutation.mutateAsync({
          id: categoryForm.target.id,
          name,
          description: categoryValues.description,
        });
      }
      setCategoryFormOpen(false);
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
    } catch (caught) {
      setFormError(mutationErrorMessage(caught as Error));
    }
  }

  async function confirmDeleteCategory() {
    if (!deleteCategoryTarget) return;
    try {
      await deleteCategoryMutation.mutateAsync(deleteCategoryTarget.id);
      setDeleteCategoryTarget(null);
      setActionError(null);
    } catch (caught) {
      setActionError(mutationErrorMessage(caught as Error));
    }
  }

  const categoryName = (id: string | null): string => {
    if (!id) return "Sem categoria";
    return categories.items.find((category) => category.id === id)?.name ?? "Sem categoria";
  };

  const articlesList = articles.items;
  const totalCount = articles.totalCount;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
  const listError =
    mutationErrorMessage(articles.error as Error | null) ??
    mutationErrorMessage(categories.error as Error | null);

  const isArticlesTab = tab === "articles";
  const subtitle = isArticlesTab
    ? articles.isPending
      ? "Carregando…"
      : `${totalCount} ${totalCount === 1 ? "artigo" : "artigos"} · ${categories.items.length} categorias`
    : `${categories.items.length} ${categories.items.length === 1 ? "categoria" : "categorias"}`;

  if (articles.isPending || categories.isPending) {
    return <LoadingBlock />;
  }

  function statusSelect(article: KnowledgeArticle, width: string | number = "100%") {
    return (
      <SelectField
        value=""
        aria-label={`Alterar status de "${article.title}"`}
        placeholder={articleStatusConfig[article.status]?.label ?? String(article.status)}
        options={statusTransitionOptions}
        style={{ width }}
        onChange={(target) => {
          if (!target) return;
          statusMutation.mutate({
            id: article.id,
            status: target as "Draft" | "Published" | "Archived",
          });
        }}
      />
    );
  }

  // F24-T007/K7: opções de autor vêm do próprio DTO (id + nome já
  // resolvidos pelo backend — sem /admin/users): só quem tem artigo,
  // exatamente o conjunto útil. Autor selecionado sem artigo na página
  // ganha uma opção sintética para o filtro não sumir da UI.
  const authorOptions = [
    ...new Map(
      articles
        .filter((article) => article.authorId)
        .map((article) => [
          article.authorId as string,
          article.authorName || "Sem nome",
        ]),
    ),
  ].map(([value, label]) => ({ value, label }));
  if (authorFilter && !authorOptions.some((option) => option.value === authorFilter)) {
    authorOptions.push({ value: authorFilter, label: "Autor filtrado" });
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Base de Conhecimento"
        subtitle={subtitle}
        actions={
<<<<<<< HEAD
          <div style={{ display: "flex", gap: 8 }}>
            <form onSubmit={applyTerm} style={{ display: "flex", gap: 6 }}>
              <SearchInput
                value={termInput}
                onChange={setTermInput}
                placeholder="Buscar artigos..."
                variant="muted"
                width={160}
                onEnter={() => { setTerm(termInput.trim()); setPage(1); }}
              />
              <Button
                type="submit"
                variant="ghost"
                style={{ padding: "5px 10px" }}
              >
                Filtrar
              </Button>
              {/* F24-T005/K5 — alerta §12 ("Knowledge necessita de
                  revisão") como filtro + chip no card. */}
              <Button
                type="button"
                variant="ghost"
                onClick={() => { setPendingOnly((value) => !value); setPage(1); }}
                style={{
                  padding: "5px 10px",
                  ...(pendingOnly
                    ? { background: "#fef3c7", color: "#92400e", borderColor: "#fde68a" }
                    : {}),
                }}
              >
                <i className="pi pi-clock" style={{ fontSize: 10 }} /> Revisões
              </Button>
              {anyFilterActive && (
=======
          <div className="flex align-items-center gap-2 flex-wrap">
            {isArticlesTab && (
              <form onSubmit={applyTerm} style={{ display: "flex", gap: 6 }}>
                <SearchInput
                  value={termInput}
                  onChange={setTermInput}
                  placeholder="Buscar artigos..."
                  variant="muted"
                  width={160}
                  onEnter={() => { setTerm(termInput.trim()); setPage(1); }}
                />
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
                <Button
                  type="submit"
                  variant="ghost"
<<<<<<< HEAD
                  onClick={clearFilters}
=======
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
                  style={{ padding: "5px 10px" }}
                >
                  Filtrar
                </Button>
                {term && (
                  <Button
                    type="button"
                    variant="ghost"
                    onClick={() => { setTermInput(""); setTerm(""); setPage(1); }}
                    style={{ padding: "5px 10px" }}
                  >
                    Limpar
                  </Button>
                )}
              </form>
            )}
            <ViewSwitcher
              mode={isArticlesTab ? articleView : categoryView}
              onChange={isArticlesTab ? setArticleView : setCategoryView}
            />
            <Can level="operate">
              {isArticlesTab ? (
                <Button
                  variant="primary"
                  onClick={openArticleCreate}
                  style={{ padding: "5px 12px" }}
                >
                  <i className="pi pi-plus" style={{ fontSize: 10 }} /> Novo Artigo
                </Button>
              ) : (
                <Button
                  variant="primary"
                  onClick={() => {
                    setCategoryForm({ mode: "create", target: null });
                    setCategoryValues(emptyCategoryForm);
                    setFormError(null);
                    setCategoryFormOpen(true);
                  }}
                  style={{ padding: "5px 12px" }}
                >
                  <i className="pi pi-plus" style={{ fontSize: 10 }} /> Nova Categoria
                </Button>
              )}
            </Can>
          </div>
        }
      />

      <div style={{ display: "flex", gap: 4, borderBottom: "1px solid #e2e8f0" }}>
        {([
          { id: "articles", label: "Artigos" },
          { id: "categories", label: "Categorias" },
        ] as const).map((item) => (
          <button
            key={item.id}
            type="button"
            onClick={() => setTab(item.id)}
            style={{
              padding: "8px 14px",
              fontSize: 13,
              fontWeight: 500,
              color: tab === item.id ? "#3b82f6" : "#64748b",
              background: "none",
              border: "none",
              borderBottom: tab === item.id ? "2px solid #3b82f6" : "2px solid transparent",
              marginBottom: -1,
              cursor: "pointer",
            }}
          >
            {item.label}
          </button>
        ))}
      </div>

      {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}

      {(actionError || listError) && (
        <ErrorBanner
          message={actionError ?? listError ?? "Não foi possível falar com o servidor. Tente novamente."}
          onRetry={() => {
            setActionError(null);
            void articles.refetch();
            void categories.refetch();
          }}
        />
      )}

<<<<<<< HEAD
      {/* F24-T007/K7 — busca avançada (docs/32 §13): filtros sobre os
          campos que existem no modelo (categoria, autor, tipo, tag).
          "Sistema"/"setor" da spec não existem no modelo — sem chip. */}
      <div
        style={{
          display: "flex",
          flexWrap: "wrap",
          gap: 8,
          alignItems: "center",
          background: "white",
          border: "1px solid #e5e7eb",
          borderRadius: 8,
          padding: "8px 12px",
        }}
      >
        <SelectField
          value={categoryIdFilter}
          onChange={(value) => { setCategoryIdFilter(value); setPage(1); }}
          placeholder="Todas as categorias"
          options={[
            { value: "", label: "Todas as categorias" },
            ...categories.map((category) => ({
              value: category.id,
              label: category.name,
            })),
          ]}
          style={{ minWidth: 160, fontSize: 12 }}
          aria-label="Filtrar por categoria"
        />
        <SelectField
          value={authorFilter}
          onChange={(value) => { setAuthorFilter(value); setPage(1); }}
          placeholder="Todos os autores"
          options={[
            { value: "", label: "Todos os autores" },
            ...authorOptions,
          ]}
          style={{ minWidth: 160, fontSize: 12 }}
          aria-label="Filtrar por autor"
        />
        <div style={{ display: "flex", flexWrap: "wrap", gap: 4, alignItems: "center" }}>
          <span style={{ fontSize: 11, color: "#9ca3af" }}>Tipo:</span>
          <Button
            type="button"
            variant="ghost"
            onClick={() => { setTypeFilter(""); setPage(1); }}
            style={filterChipStyle(!typeFilter)}
          >
            Todos
          </Button>
          {KNOWLEDGE_TYPE_OPTIONS.map((option) => (
            <Button
              key={option.value}
              type="button"
              variant="ghost"
              onClick={() => {
                setTypeFilter(option.value === typeFilter ? "" : option.value);
                setPage(1);
              }}
              style={filterChipStyle(typeFilter === option.value)}
            >
              {option.label}
            </Button>
          ))}
        </div>
        {tagFilter && (
          <Button
            type="button"
            variant="ghost"
            onClick={() => { setTagFilter(""); setPage(1); }}
            style={filterChipStyle(true)}
            title="Remover filtro de tag"
          >
            #{tagFilter} <i className="pi pi-times" style={{ fontSize: 10 }} />
          </Button>
        )}
      </div>

      {creating && (
        <form onSubmit={submitCreate} style={{ display: "flex", flexDirection: "column", gap: 10, padding: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Novo artigo</div>
=======
      {isArticlesTab ? (
        <EntityView<KnowledgeArticle>
          mode={articleView}
          items={articlesList}
          getKey={(article) => article.id}
          emptyMessage={listError ? "—" : "Nenhum artigo encontrado"}
          kanban={{ groupOf: (article) => String(article.status), columns: statusColumns }}
          table={
            <DataTablePanel value={articlesList} dataKey="id" emptyMessage={listError ? "—" : "Nenhum artigo encontrado"}>
              <Column header="Título" body={(article: KnowledgeArticle) => (
                <div>
                  <div style={{ fontWeight: 500, fontSize: 13 }}>{article.title}</div>
                  {article.description && (
                    <div style={{ fontSize: 11, color: "#94a3b8" }}>{article.description}</div>
                  )}
                </div>
              )} />
              <Column header="Categoria" body={(article: KnowledgeArticle) => categoryName(article.categoryId)} style={{ width: 150 }} />
              <Column header="Tipo" body={(article: KnowledgeArticle) => article.type ?? "—"} style={{ width: 170 }} />
              <Column header="Status" body={(article: KnowledgeArticle) => {
                const status = articleStatusConfig[article.status] ?? {
                  label: String(article.status), bg: "#f1f5f9", color: "#64748b",
                };
                return <StatusChip label={status.label} background={status.bg} color={status.color} fontSize={10} fontWeight={600} />;
              }} style={{ width: 115 }} />
              <Column header="Responsável" body={(article: KnowledgeArticle) => article.responsibleName ?? "—"} style={{ width: 150 }} />
              <Column
                header=""
                style={{ width: 240 }}
                body={(article: KnowledgeArticle) => (
                  <Can level="operate">
                    <div className="flex gap-1 align-items-center">
                      <Button variant="ghost" onClick={() => openArticleEdit(article)}>
                        Editar
                      </Button>
                      <Button variant="ghost" onClick={() => setDeleteArticleTarget(article)}>
                        Excluir
                      </Button>
                      {statusSelect(article, 140)}
                    </div>
                  </Can>
                )}
              />
            </DataTablePanel>
          }
          renderCard={(article) => (
            <div style={{ background: "white", borderRadius: 8, border: "1px solid #e5e7eb", padding: 16, display: "flex", flexDirection: "column", gap: 8 }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
                <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                  <StatusChip
                    label={categoryName(article.categoryId)}
                    background="#eff6ff"
                    color="#3b82f6"
                    fontSize={10}
                    fontWeight={600}
                  />
                  {article.type && (
                    <StatusChip
                      label={article.type}
                      background="#f1f5f9"
                      color="#475569"
                      fontSize={10}
                      fontWeight={600}
                    />
                  )}
                </div>
                {(() => {
                  const status = articleStatusConfig[article.status] ?? {
                    label: String(article.status), bg: "#f1f5f9", color: "#64748b",
                  };
                  return (
                    <StatusChip
                      label={status.label}
                      background={status.bg}
                      color={status.color}
                      fontSize={10}
                      fontWeight={600}
                    />
                  );
              })()}
              </div>
              <div style={{ fontSize: 13, fontWeight: 600 }}>{article.title}</div>
              {article.description && (
                <div style={{
                  fontSize: 12, color: "#6b7280", fontStyle: "italic",
                  display: "-webkit-box", WebkitLineClamp: 2, WebkitBoxOrient: "vertical", overflow: "hidden",
                }}>
                  {article.description}
                </div>
              )}
              <div style={{
                fontSize: 12, color: "#6b7280", lineHeight: 1.4,
                display: "-webkit-box", WebkitLineClamp: 4, WebkitBoxOrient: "vertical", overflow: "hidden",
              }}>
                {article.content}
              </div>
              {article.tags.length > 0 && (
                <div style={{ display: "flex", gap: 4, flexWrap: "wrap" }}>
                  {article.tags.map((tag) => (
                    <span
                      key={tag}
                      style={{ background: "#eef2ff", color: "#6366f1", borderRadius: 999, padding: "1px 7px", fontSize: 10, fontWeight: 600 }}
                    >
                      #{tag}
                    </span>
                  ))}
                </div>
              )}
              {(article.authorName || article.responsibleName) && (
                <div style={{ fontSize: 11, color: "#94a3b8" }}>
                  {[
                    article.authorName ? `por ${article.authorName}` : null,
                    article.responsibleName ? `resp. ${article.responsibleName}` : null,
                  ].filter(Boolean).join(" · ")}
                </div>
              )}
              <Can level="operate">
                <div className="flex gap-1 align-items-center" style={{ marginTop: 4, borderTop: "1px solid #f3f4f6", paddingTop: 8 }}>
                  <Button variant="ghost" onClick={() => openArticleEdit(article)}>
                    Editar
                  </Button>
                  <Button variant="ghost" onClick={() => setDeleteArticleTarget(article)}>
                    Excluir
                  </Button>
                </div>
                <div style={{ marginTop: 6 }}>
                  {statusSelect(article)}
                </div>
              </Can>
            </div>
          )}
        />
      ) : (
        <EntityView<KnowledgeCategory>
          mode={categoryView}
          items={categories.items}
          getKey={(category) => category.id}
          emptyMessage={listError ? "—" : "Nenhuma categoria cadastrada"}
          table={
            <DataTablePanel value={categories.items} dataKey="id" emptyMessage={listError ? "—" : "Nenhuma categoria cadastrada"}>
              <Column header="Nome" body={(category: KnowledgeCategory) => category.name} />
              <Column header="Descrição" body={(category: KnowledgeCategory) => category.description ?? "—"} />
              <Column
                header="Artigos"
                style={{ width: 110 }}
                body={(category: KnowledgeCategory) =>
                  articlesList.filter((article) => article.categoryId === category.id).length
                }
              />
              <Column
                header=""
                style={{ width: 150 }}
                body={(category: KnowledgeCategory) => (
                  <Can level="operate">
                    <div className="flex gap-1">
                      <Button
                        variant="ghost"
                        onClick={() => {
                          setCategoryForm({ mode: "edit", target: category });
                          setCategoryValues({ name: category.name, description: category.description ?? "" });
                          setFormError(null);
                          setCategoryFormOpen(true);
                        }}
                      >
                        Editar
                      </Button>
                      <Button variant="ghost" onClick={() => setDeleteCategoryTarget(category)}>
                        Excluir
                      </Button>
                    </div>
                  </Can>
                )}
              />
            </DataTablePanel>
          }
          renderCard={(category) => (
            <div className="uni-panel" style={{ padding: 16 }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8, marginBottom: 6 }}>
                <span style={{ fontWeight: 600, fontSize: 14 }}>
                  <i className="pi pi-folder" style={{ marginRight: 6, color: "#3b82f6" }} />
                  {category.name}
                </span>
                <StatusChip
                  label={`${articlesList.filter((article) => article.categoryId === category.id).length} artigo(s)`}
                  background="#eff6ff"
                  color="#3b82f6"
                  fontSize={10}
                  fontWeight={600}
                />
              </div>
              <p style={{ fontSize: 12, color: "#64748b" }}>{category.description ?? "—"}</p>
              <Can level="operate">
                <div className="flex gap-1" style={{ marginTop: 8 }}>
                  <Button
                    variant="ghost"
                    onClick={() => {
                      setCategoryForm({ mode: "edit", target: category });
                      setCategoryValues({ name: category.name, description: category.description ?? "" });
                      setFormError(null);
                      setCategoryFormOpen(true);
                    }}
                  >
                    Editar
                  </Button>
                  <Button variant="ghost" onClick={() => setDeleteCategoryTarget(category)}>
                    Excluir
                  </Button>
                </div>
              </Can>
            </div>
          )}
        />
      )}

      {isArticlesTab && totalCount > PAGE_SIZE && (
        <Pagination
          page={page}
          totalPages={totalPages}
          disabled={articles.isFetching}
          onPage={setPage}
        />
      )}

      <Dialog
        visible={articleFormOpen}
        onHide={() => setArticleFormOpen(false)}
        header={articleForm.mode === "create" ? "Novo artigo" : `Editar — ${articleForm.target?.title ?? ""}`}
        style={{ width: "min(720px, 95vw)" }}
      >
        <form onSubmit={submitArticleForm} style={{ display: "flex", flexDirection: "column", gap: 10 }}>
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
          <div style={{ display: "grid", gridTemplateColumns: "1fr 240px", gap: 10 }}>
            <FormField label="Título" error={null}>
              <input
                value={values.title}
                onChange={(event) => setValues((current) => ({ ...current, title: event.target.value }))}
                placeholder="Título * (máx. 200)"
                autoFocus
                maxLength={200}
                style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
              />
            </FormField>
            <FormField label="Tipo" error={null}>
              <SelectField
                value={values.type}
                onChange={(value) => setValues((current) => ({ ...current, type: value }))}
                placeholder="Selecione o tipo *"
                options={[
                  { value: "", label: "Selecione o tipo *" },
                  ...typeOptions,
                ]}
              />
            </FormField>
            <FormField label="Categoria" error={null}>
              <SelectField
                value={values.categoryId}
                onChange={(value) => setValues((current) => ({ ...current, categoryId: value }))}
                placeholder="Sem categoria"
                options={[
                  { value: "", label: "Sem categoria" },
                  ...categories.items.map((category) => ({
                    value: category.id,
                    label: category.name,
                  })),
                ]}
              />
            </FormField>
            <FormField label="Responsável" error={null}>
              <SelectField
                value={values.responsibleId}
                onChange={(value) => setValues((current) => ({ ...current, responsibleId: value }))}
                placeholder="Sem responsável"
                options={[
                  { value: "", label: "Sem responsável" },
                  ...users.items.map((user) => ({
                    value: user.userId,
                    label: user.displayName,
                  })),
                ]}
              />
            </FormField>
          </div>
<<<<<<< HEAD
          {/* F24-T001/K1 — identificação (docs/32 §5–§6). */}
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
            <SelectField
              value={articleType}
              onChange={setArticleType}
              placeholder="Sem tipo"
              options={[
                { value: "", label: "Sem tipo" },
                ...KNOWLEDGE_TYPE_OPTIONS,
              ]}
              aria-label="Tipo do artigo"
            />
            <SelectField
              value={responsibleId}
              onChange={setResponsibleId}
              placeholder="Sem responsável"
              options={[
                { value: "", label: "Sem responsável" },
                ...users.map((user) => ({
                  value: user.userId,
                  label: user.displayName || user.email,
                })),
              ]}
              aria-label="Responsável pelo artigo"
            />
          </div>
          <input
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            placeholder="Descrição (máx. 500) — resumo do que este artigo cobre"
            maxLength={500}
            style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
          />
          <input
            value={tagsInput}
            onChange={(event) => setTagsInput(event.target.value)}
            placeholder="Tags separadas por vírgula (máx. 20, até 50 caracteres cada)"
            style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
          />
          <textarea
            value={content}
            onChange={(event) => setContent(event.target.value)}
            placeholder="Conteúdo * (máx. 20000)"
            rows={6}
            style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, resize: "vertical", fontFamily: "inherit" }}
          />
=======
          <FormField label="Descrição (máx. 500)" error={null}>
            <input
              value={values.description}
              onChange={(event) => setValues((current) => ({ ...current, description: event.target.value }))}
              placeholder="Descrição (máx. 500)"
              maxLength={500}
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, width: "100%" }}
            />
          </FormField>
          <div style={{ display: "flex", gap: 6, alignItems: "center", flexWrap: "wrap" }}>
            <input
              value={values.tagInput}
              onChange={(event) => setValues((current) => ({ ...current, tagInput: event.target.value }))}
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  event.preventDefault();
                  addTag();
                }
              }}
              placeholder={`Tags — Enter para adicionar (máx. ${MAX_TAGS})`}
              maxLength={TAG_MAX_LENGTH}
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, width: 260 }}
            />
            <Button type="button" variant="ghost" onClick={addTag} style={{ padding: "5px 10px" }}>
              Adicionar
            </Button>
            {values.tags.map((tag) => (
              <span
                key={tag}
                style={{ display: "inline-flex", alignItems: "center", gap: 4, background: "#eef2ff", color: "#6366f1", borderRadius: 999, padding: "2px 8px", fontSize: 11, fontWeight: 600 }}
              >
                #{tag}
                <button
                  type="button"
                  onClick={() => setValues((current) => ({ ...current, tags: current.tags.filter((existing) => existing !== tag) }))}
                  aria-label={`Remover tag ${tag}`}
                  style={{ border: "none", background: "transparent", color: "#6366f1", cursor: "pointer", padding: 0, fontSize: 11 }}
                >
                  ×
                </button>
              </span>
            ))}
          </div>
          <FormField label="Conteúdo" error={null}>
            <textarea
              value={values.content}
              onChange={(event) => setValues((current) => ({ ...current, content: event.target.value }))}
              placeholder="Conteúdo * (máx. 20000)"
              rows={6}
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, resize: "vertical", fontFamily: "inherit", width: "100%" }}
            />
          </FormField>
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
          {formError && <FormError message={formError} />}
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <Button
              type="button"
              variant="ghost"
              onClick={() => setArticleFormOpen(false)}
              style={{ padding: "6px 14px" }}
            >
              Cancelar
            </Button>
            <Button
              type="submit"
              variant="primary"
              disabled={createArticleMutation.isPending || updateArticleMutation.isPending}
              style={{ padding: "6px 14px" }}
            >
              {createArticleMutation.isPending || updateArticleMutation.isPending
                ? "Salvando…"
                : articleForm.mode === "create" ? "Criar artigo" : "Salvar"}
            </Button>
          </div>
        </form>
      </Dialog>

<<<<<<< HEAD
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))", gap: 12 }}>
        {articles.map((article) => {
          const status = statusConfig[article.status] ?? {
            label: String(article.status), bg: "#f1f5f9", color: "#64748b",
          };
          const typeLabel = knowledgeTypeLabel(article.type);
          return (
            <div key={article.id} style={{ background: "white", borderRadius: 8, border: "1px solid #e5e7eb", padding: 16, display: "flex", flexDirection: "column", gap: 8 }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 6, flexWrap: "wrap" }}>
                  <StatusChip
                    label={categoryName(article.categoryId)}
                    background="#eff6ff"
                    color="#3b82f6"
                    fontSize={10}
                    fontWeight={600}
                  />
                  {typeLabel && (
                    <StatusChip
                      label={typeLabel}
                      background="#f5f3ff"
                      color="#7c3aed"
                      fontSize={10}
                      fontWeight={600}
                    />
                  )}
                  {/* F24-T005/K5 — "Knowledge necessita de revisão" (§12). */}
                  {article.pendingReview && (
                    <StatusChip
                      label="Revisar"
                      background="#fef3c7"
                      color="#92400e"
                      fontSize={10}
                      fontWeight={600}
                    />
                  )}
                </div>
                <StatusChip
                  label={status.label}
                  background={status.bg}
                  color={status.color}
                  fontSize={10}
                  fontWeight={600}
                />
              </div>
              <div style={{ fontSize: 13, fontWeight: 600 }}>{article.title}</div>
              {article.description && (
                <div style={{ fontSize: 12, color: "#374151", lineHeight: 1.4 }}>
                  {article.description}
                </div>
              )}
              <div style={{
                fontSize: 12, color: "#6b7280", lineHeight: 1.4,
                display: "-webkit-box", WebkitLineClamp: 4, WebkitBoxOrient: "vertical", overflow: "hidden",
              }}>
                {article.content}
              </div>
              {article.tags.length > 0 && (
                <div style={{ display: "flex", flexWrap: "wrap", gap: 4 }}>
                  {article.tags.map((tag) => (
                    <button
                      key={tag}
                      type="button"
                      onClick={() => { setTagFilter(tag); setPage(1); }}
                      title={`Filtrar por #${tag}`}
                      style={{
                        fontSize: 10, background: "#f1f5f9", color: "#475569",
                        border: "1px solid #e2e8f0", borderRadius: 999, padding: "1px 7px",
                        cursor: "pointer",
                      }}
                    >
                      #{tag}
                    </button>
                  ))}
                </div>
              )}
              {article.origins.length > 0 && (
                <div style={{ display: "flex", alignItems: "center", flexWrap: "wrap", gap: 4 }}>
                  <span style={{ fontSize: 10, color: "#9ca3af" }}>Origem:</span>
                  {article.origins.map((origin) => (
                    <span
                      key={`${origin.type}:${origin.id}`}
                      title={origin.id}
                      style={{
                        fontSize: 10, background: "#f0fdfa", color: "#0f766e",
                        border: "1px solid #ccfbf1", borderRadius: 999, padding: "1px 7px",
                      }}
                    >
                      {originLabels[origin.type] ?? origin.type}
                    </span>
                  ))}
                </div>
              )}
              {(article.authorName || article.responsibleName) && (
                <div style={{ fontSize: 11, color: "#9ca3af", display: "flex", gap: 10, flexWrap: "wrap" }}>
                  {article.authorName && article.authorId && (
                    <button
                      type="button"
                      onClick={() => { setAuthorFilter(article.authorId as string); setPage(1); }}
                      title="Filtrar por este autor"
                      style={{
                        fontSize: 11, color: "#9ca3af", background: "none",
                        border: "none", padding: 0, cursor: "pointer",
                        textDecoration: "underline",
                      }}
                    >
                      Autor: {article.authorName}
                    </button>
                  )}
                  {article.responsibleName && <span>Responsável: {article.responsibleName}</span>}
                </div>
              )}
            </div>
          );
        })}
        {!loading && articles.length === 0 && (
          <div style={{ gridColumn: "1 / -1", textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13, background: "white", borderRadius: 8, border: "1px solid #e5e7eb" }}>
            {error ? "—" : "Nenhum artigo encontrado"}
=======
      <Dialog
        visible={categoryFormOpen}
        onHide={() => setCategoryFormOpen(false)}
        header={categoryForm.mode === "create" ? "Nova categoria" : `Editar — ${categoryForm.target?.name ?? ""}`}
      >
        <form onSubmit={submitCategoryForm} style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <FormField label="Nome" error={null}>
            <input
              autoFocus
              value={categoryValues.name}
              onChange={(event) => setCategoryValues((current) => ({ ...current, name: event.target.value }))}
              placeholder="Nome da categoria"
              style={{ padding: "8px 10px", fontSize: 13, border: "1px solid #e2e8f0", borderRadius: 6, width: "100%" }}
            />
          </FormField>
          <FormField label="Descrição (opcional)" error={null}>
            <input
              value={categoryValues.description}
              onChange={(event) => setCategoryValues((current) => ({ ...current, description: event.target.value }))}
              placeholder="Descrição"
              style={{ padding: "8px 10px", fontSize: 13, border: "1px solid #e2e8f0", borderRadius: 6, width: "100%" }}
            />
          </FormField>
          {formError && <FormError message={formError} />}
          <div className="flex justify-content-end gap-2" style={{ marginTop: 4 }}>
            <Button variant="ghost" onClick={() => setCategoryFormOpen(false)}>
              Cancelar
            </Button>
            <Button variant="primary" type="submit">
              {categoryForm.mode === "create" ? "Criar" : "Salvar"}
            </Button>
>>>>>>> e3fcaff58af6d26d93c6d14fd9c659552852b34c
          </div>
        </form>
      </Dialog>

      <ConfirmDialog
        visible={deleteArticleTarget !== null}
        onHide={() => setDeleteArticleTarget(null)}
        onConfirm={() => void confirmDeleteArticle()}
        header="Excluir artigo"
        message={
          deleteArticleTarget
            ? `Excluir "${deleteArticleTarget.title}"? O backend valida o ciclo Draft→Published→Archived.`
            : ""
        }
        confirmLabel="Excluir"
      />

      <ConfirmDialog
        visible={deleteCategoryTarget !== null}
        onHide={() => setDeleteCategoryTarget(null)}
        onConfirm={() => void confirmDeleteCategory()}
        header="Excluir categoria"
        message={
          deleteCategoryTarget
            ? `Excluir "${deleteCategoryTarget.name}"? Artigos vinculados bloqueiam a exclusão com 409.`
            : ""
        }
        confirmLabel="Excluir"
      />
    </div>
  );
}
