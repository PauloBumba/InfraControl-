import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Column } from "primereact/column";
import { Can } from "@/components/auth/Can";
import { Button } from "@/components/ui/Button";
import { ConfirmDialog, Dialog } from "@/components/ui/Dialog";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { FormField, FormError } from "@/components/ui/FormField";
import { useFormErrors } from "@/hooks/useFormErrors";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { Pagination } from "@/components/ui/Pagination";
import { SearchInput } from "@/components/ui/SearchInput";
import { StatusChip } from "@/components/ui/StatusChip";
import { SelectField } from "@/components/ui/SelectField";
import { ImageUpload } from "@/components/ui/ImageUpload";
import { resolveUploadSrc } from "@/components/ui/imageUploadTypes";
import {
  KNOWLEDGE_TYPE_OPTIONS,
  articleStatusConfig,
  articleStatusLabel,
  knowledgeCategoryAccent,
  knowledgeNextStatuses,
  knowledgeTypeLabel,
  type KnowledgeCategoryAccent,
} from "@/components/knowledge/knowledgeCatalogs";
import { listUsers, type AdminUser } from "@/services/api/adminUsers";
import {
  changeArticleStatus,
  createArticle,
  createCategory,
  deleteArticle,
  deleteCategory,
  getArticle,
  listArticles,
  listCategories,
  updateArticle,
  updateCategory,
  type KnowledgeArticle,
  type KnowledgeCategory,
  type Paged,
} from "@/services/api/knowledge";
import { uploadImage } from "@/services/api/uploads";
import { errorMessage } from "@/hooks/queries";

/** Upload reutilizável — Knowledge usa o módulo `knowledge` do store. */
async function uploadKnowledgeImage(file: File): Promise<string> {
  const result = await uploadImage(file, "knowledge");
  if (!result?.imagePath) throw new Error("Upload sem path.");
  return result.imagePath;
}

/**
 * F17-T004 — KnowledgePage conectada ao backend. F24 edição + visões
 * lista/tabela/quadro: a página ganhou o trocador de visão (`EntityView`,
 * preferência no localStorage), edição de artigo (PUT {id} com motivo
 * obrigatório — K3), exclusão (ConfirmDialog), troca de status (select só
 * com as transições válidas do ADR-034) e gerenciamento de categorias
 * (CRUD). O catálogo de status/transições saiu da página para
 * `knowledgeCatalogs.ts` (fonte única para as 3 visões + o seletor).
 *
 * O backend continua autoridade: 409 fora da matriz de status, 400 de
 * política, 404 pt-BR — tudo via `errors[0]` do envelope.
 */

const PAGE_SIZE = 20;

const originLabels: Record<string, string> = {
  Task: "Task",
  Pdi: "PDI",
  Incident: "Chamado",
};

/** Data do detalhe (revisão) — ISO do backend → pt-BR; inválida vira "—". */
function formatDate(value: string | null): string {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : date.toLocaleDateString("pt-BR");
}

export function KnowledgePage() {
  const [pageData, setPageData] = useState<Paged<KnowledgeArticle> | null>(null);
  const [categories, setCategories] = useState<KnowledgeCategory[]>([]);
  // F24-T008 — contagem real por categoria (a página só traz 20 artigos).
  const [categoryCounts, setCategoryCounts] = useState<Record<string, number>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [page, setPage] = useState(1);
  const [term, setTerm] = useState("");
  const [termInput, setTermInput] = useState("");
  const [pendingOnly, setPendingOnly] = useState(false);
  const [categoryIdFilter, setCategoryIdFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [authorFilter, setAuthorFilter] = useState("");
  const [tagFilter, setTagFilter] = useState("");

  // F24 visões — tabela/grid/quadro, preferência persistida.
  const [view, setView] = useEntityViewMode("knowledge", "grid");

  const [creating, setCreating] = useState(false);
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [articleType, setArticleType] = useState("");
  const [description, setDescription] = useState("");
  const [tagsInput, setTagsInput] = useState("");
  const [responsibleId, setResponsibleId] = useState("");
  const [imagePath, setImagePath] = useState<string | null>(null);
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [saving, setSaving] = useState(false);
  // F29-T001C-2 — um `useFormErrors` por form: a página tem 3 deles (criação,
  // edição, categoria) abertos ao mesmo tempo em telas diferentes, então um
  // único estado compartilhado apagaria o erro de um ao abrir o outro.
  // `statusError` fica com `useState` de propósito: só recebe recusa do
  // servidor, que já vive no nível do formulário — migrá-lo seria trocar um
  // estado por um hook que devolve sempre `field = null`.
  const {
    error: formError,
    field,
    reset: resetFormError,
    fail: failForm,
    errorFor,
  } = useFormErrors(); // criação

  // F24 edição — form controlado à parte do de criação.
  const [editing, setEditing] = useState<KnowledgeArticle | null>(null);
  const [editTitle, setEditTitle] = useState("");
  const [editContent, setEditContent] = useState("");
  const [editCategoryId, setEditCategoryId] = useState("");
  const [editType, setEditType] = useState("");
  const [editDescription, setEditDescription] = useState("");
  const [editTagsInput, setEditTagsInput] = useState("");
  const [editResponsibleId, setEditResponsibleId] = useState("");
  const [editImagePath, setEditImagePath] = useState<string | null>(null);
  const [editReason, setEditReason] = useState("");
  const [editSaving, setEditSaving] = useState(false);
  const {
    error: editError,
    field: editField,
    reset: resetEditError,
    fail: failEdit,
    errorFor: errorForEdit,
  } = useFormErrors(); // edição — 3 validações de 3 campos diferentes

  const [deleteTarget, setDeleteTarget] = useState<KnowledgeArticle | null>(null);

  // F24-T008 — detalhe do artigo ao clicar no card/linha. Abre com o item da
  // lista na hora e enriquece com GET/{id} (vínculos não vêm no search).
  const [detail, setDetail] = useState<KnowledgeArticle | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  // F24 status — só transições válidas (espelho do ADR-034).
  const [statusTarget, setStatusTarget] = useState<KnowledgeArticle | null>(null);
  const [newStatus, setNewStatus] = useState("");
  const [statusError, setStatusError] = useState<string | null>(null);
  const [statusSaving, setStatusSaving] = useState(false);

  // F24 categorias — CRUD (criar/editar/excluir).
  const [categoryForm, setCategoryForm] = useState<{
    mode: "create" | "edit";
    target: KnowledgeCategory | null;
  } | null>(null);
  const [categoryName, setCategoryName] = useState("");
  const [categoryDescription, setCategoryDescription] = useState("");
  const [categorySaving, setCategorySaving] = useState(false);
  const {
    error: categoryError,
    field: categoryField,
    reset: resetCategoryError,
    fail: failCategory,
    errorFor: errorForCategory,
  } = useFormErrors(); // CRUD de categoria
  const [deleteCategoryTarget, setDeleteCategoryTarget] = useState<KnowledgeCategory | null>(null);

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
          categoryId: categoryIdFilter || undefined,
          type: typeFilter || undefined,
          authorId: authorFilter || undefined,
          tag: tagFilter || undefined,
        }),
      );
    } catch (caught) {
      setError(errorMessage(caught, "Não foi possível carregar os artigos."));
    } finally {
      setLoading(false);
    }
  }, [term, page, pendingOnly, categoryIdFilter, typeFilter, authorFilter, tagFilter]);

  useEffect(() => {
    void load();
  }, [load]);

  /**
   * F24-T008 — total por categoria para os tiles do topo. Uma consulta
   * `pageSize=1` por categoria (o backend devolve `totalCount`); categorias
   * são poucas e o custo é 1 request leve cada. Falha individual degrada
   * para "—" em vez de derrubar a faixa.
   */
  const loadCategoryCounts = useCallback(async (list: KnowledgeCategory[]) => {
    if (list.length === 0) {
      setCategoryCounts({});
      return;
    }
    const entries = await Promise.all(
      list.map(async (category) => {
        try {
          const result = await listArticles({ categoryId: category.id, page: 1, pageSize: 1 });
          return [category.id, result.totalCount] as const;
        } catch {
          return [category.id, null] as const;
        }
      }),
    );
    setCategoryCounts(
      Object.fromEntries(entries.filter(([, total]) => total !== null)) as Record<string, number>,
    );
  }, []);

  const loadCategories = useCallback(async () => {
    try {
      const list = (await listCategories()).items;
      setCategories(list);
      void loadCategoryCounts(list);
    } catch {
      // degrada para "Sem categoria" — a lista de artigos segue.
    }
  }, [loadCategoryCounts]);

  useEffect(() => {
    void loadCategories();
  }, [loadCategories]);

  /** Recarrega a contagem das categorias já conhecidas (após mutação). */
  const refreshCategoryCounts = useCallback(() => {
    void loadCategoryCounts(categories);
  }, [categories, loadCategoryCounts]);

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

  const filterChipStyle = (active: boolean) => ({
    padding: "5px 10px",
    ...(active
      ? { background: "#eff6ff", color: "#1d4ed8", borderColor: "#bfdbfe" }
      : {}),
  });

  /** F24-T008 — tile de categoria do topo (ativo = cor da categoria). */
  const categoryTileStyle = (active: boolean, accent: KnowledgeCategoryAccent) => ({
    background: active ? accent.bg : "#f8fafc",
    border: `1px solid ${active ? accent.border : "#e2e8f0"}`,
    boxShadow: active ? `inset 0 0 0 1px ${accent.border}` : "none",
    borderRadius: 8,
    padding: 12,
    cursor: "pointer",
    transition: "all 0.2s",
    textAlign: "left" as const,
    minWidth: 0,
  });

  const categoryNameOf = (id: string | null): string => {
    if (!id) return "Sem categoria";
    return categories.find((category) => category.id === id)?.name ?? "Sem categoria";
  };

  const parseTags = (raw: string): string[] =>
    raw
      .split(",")
      .map((tag) => tag.trim())
      .filter(Boolean);

  async function submitCreate(event: FormEvent) {
    event.preventDefault();
    const trimmedTitle = title.trim();
    const trimmedContent = content.trim();
    if (!trimmedTitle) {
      failForm("Informe o título do artigo.", "title");
      return;
    }
    if (!trimmedContent) {
      failForm("Informe o conteúdo do artigo.", "content");
      return;
    }
    setSaving(true);
    resetFormError();
    try {
      const tags = parseTags(tagsInput);
      await createArticle({
        title: trimmedTitle,
        content: trimmedContent,
        categoryId: categoryId || undefined,
        type: articleType || undefined,
        description: description.trim() || undefined,
        tags: tags.length ? tags : undefined,
        responsibleId: responsibleId || undefined,
        imagePath: imagePath || undefined,
      });
      setCreating(false);
      setTitle("");
      setContent("");
      setCategoryId("");
      setArticleType("");
      setDescription("");
      setTagsInput("");
      setResponsibleId("");
      setImagePath(null);
      setNotice("Artigo criado como rascunho — publique pela troca de status.");
      await load();
      refreshCategoryCounts();
    } catch (caught) {
      // Recusa do servidor sem `field` → nível do formulário. `errorMessage`
      // aceita `unknown` (o `catch` devolve isso) e tem fallback.
      failForm(errorMessage(caught, "Não foi possível criar o artigo."));
    } finally {
      setSaving(false);
    }
  }

  // ── F24-T008 detalhe ──────────────────────────────────────────────────

  async function openDetail(article: KnowledgeArticle) {
    setDetail(article);
    setDetailLoading(true);
    try {
      const enriched = await getArticle(article.id);
      // Ignora resposta atrasada de um detalhe que já foi trocado/fechado.
      setDetail((current) => (current?.id === article.id && enriched ? enriched : current));
    } catch {
      // Mantém o item da lista — o detalhe essencial já está na tela.
    } finally {
      setDetailLoading(false);
    }
  }

  // ── F24 edição ────────────────────────────────────────────────────────

  function startEdit(article: KnowledgeArticle) {
    setEditing(article);
    setEditTitle(article.title);
    setEditContent(article.content);
    setEditCategoryId(article.categoryId ?? "");
    setEditType(article.type ?? "");
    setEditDescription(article.description ?? "");
    setEditTagsInput(article.tags.join(", "));
    setEditResponsibleId(article.responsibleId ?? "");
    setEditImagePath(article.imagePath);
    setEditReason("");
    resetEditError();
  }

  async function submitEdit(event: FormEvent) {
    event.preventDefault();
    if (!editing) return;
    const trimmedTitle = editTitle.trim();
    const trimmedContent = editContent.trim();
    const trimmedReason = editReason.trim();
    // Três validações, três campos: cada uma acende o próprio. Antes todas as
    // três iam para o mesmo `<FormError>` no pé do dialog, sem dizer qual era.
    if (!trimmedTitle) {
      failEdit("Informe o título do artigo.", "editTitle");
      return;
    }
    if (!trimmedContent) {
      failEdit("Informe o conteúdo do artigo.", "editContent");
      return;
    }
    if (!trimmedReason) {
      failEdit("Informe o motivo da alteração (vira version — K3).", "editReason");
      return;
    }
    setEditSaving(true);
    resetEditError();
    try {
      const tags = parseTags(editTagsInput);
      await updateArticle(editing.id, {
        title: trimmedTitle,
        content: trimmedContent,
        categoryId: editCategoryId || undefined,
        reason: trimmedReason,
        type: editType || undefined,
        description: editDescription.trim() || undefined,
        tags: tags.length ? tags : undefined,
        responsibleId: editResponsibleId || undefined,
        imagePath: editImagePath,
      });
      setEditing(null);
      setNotice("Artigo atualizado — uma nova versão foi registrada.");
      await load();
      refreshCategoryCounts();
    } catch (caught) {
      failEdit(errorMessage(caught, "Não foi possível atualizar o artigo."));
    } finally {
      setEditSaving(false);
    }
  }

  async function confirmDeleteArticle() {
    if (!deleteTarget) return;
    try {
      await deleteArticle(deleteTarget.id);
      setNotice(`Artigo "${deleteTarget.title}" excluído.`);
      await load();
      refreshCategoryCounts();
    } catch (caught) {
      setError(errorMessage(caught, "Não foi possível excluir o artigo."));
    }
  }

  // ── F24 status (ADR-034) ─────────────────────────────────────────────

  function openStatus(article: KnowledgeArticle) {
    setStatusTarget(article);
    setNewStatus("");
    setStatusError(null);
  }

  async function submitStatus() {
    if (!statusTarget || !newStatus) return;
    setStatusSaving(true);
    setStatusError(null);
    try {
      await changeArticleStatus(statusTarget.id, Number(newStatus));
      const label = articleStatusLabel(Number(newStatus));
      setStatusTarget(null);
      setNotice(`Status alterado para "${label}".`);
      await load();
    } catch (caught) {
      // 409 fora da matriz / 400 status desconhecido vêm em pt-BR.
      setStatusError(errorMessage(caught, "Não foi possível alterar o status."));
    } finally {
      setStatusSaving(false);
    }
  }

  // ── F24 categorias (CRUD) ────────────────────────────────────────────

  function openCategoryCreate() {
    setCategoryForm({ mode: "create", target: null });
    setCategoryName("");
    setCategoryDescription("");
    resetCategoryError();
  }

  async function submitCategory(event: FormEvent) {
    event.preventDefault();
    const trimmedName = categoryName.trim();
    if (!trimmedName) {
      failCategory("Informe o nome da categoria.", "categoryName");
      return;
    }
    setCategorySaving(true);
    resetCategoryError();
    try {
      if (categoryForm?.mode === "edit" && categoryForm.target) {
        await updateCategory(categoryForm.target.id, {
          name: trimmedName,
          description: categoryDescription.trim() || undefined,
        });
        setNotice("Categoria atualizada.");
      } else {
        await createCategory({
          name: trimmedName,
          description: categoryDescription.trim() || undefined,
        });
        setNotice("Categoria criada.");
      }
      setCategoryForm(null);
      await loadCategories();
    } catch (caught) {
      failCategory(errorMessage(caught, "Não foi possível salvar a categoria."));
    } finally {
      setCategorySaving(false);
    }
  }

  async function confirmDeleteCategory() {
    if (!deleteCategoryTarget) return;
    try {
      await deleteCategory(deleteCategoryTarget.id);
      setNotice(`Categoria "${deleteCategoryTarget.name}" excluída.`);
      await loadCategories();
      // artigos podem ter mudado de "sem categoria" — recarrega a grade.
      await load();
    } catch (caught) {
      setError(errorMessage(caught, "Não foi possível excluir a categoria."));
    }
  }

  const articles = pageData?.items ?? [];
  const totalCount = pageData?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const authorOptions = [
    ...new Map(
      articles
        .filter((article) => article.authorId)
        .map((article) => [article.authorId as string, article.authorName || "Sem nome"]),
    ),
  ].map(([value, label]) => ({ value, label }));
  if (authorFilter && !authorOptions.some((option) => option.value === authorFilter)) {
    authorOptions.push({ value: authorFilter, label: "Autor filtrado" });
  }

  // Transições disponíveis para o artigo sob troca (matriz ADR-034).
  const statusTargets = statusTarget
    ? knowledgeNextStatuses(statusTarget.status).map((status) => ({
        value: String(status),
        label: articleStatusConfig[status]?.label ?? String(status),
      }))
    : [];

  const userOptions = [
    { value: "", label: "Sem responsável" },
    ...users.map((user) => ({ value: user.userId, label: user.displayName || user.email })),
  ];

  const categorySelectOptions = [
    { value: "", label: "Sem categoria" },
    ...categories.map((category) => ({ value: category.id, label: category.name })),
  ];

  const detailStatus = detail
    ? articleStatusConfig[detail.status] ?? {
        label: String(detail.status),
        bg: "#f1f5f9",
        color: "#64748b",
      }
    : null;

  /** Card de artigo — usado na visão grid E no quadro (kanban). */
  function renderCard(article: KnowledgeArticle) {
    const status = articleStatusConfig[article.status] ?? {
      label: String(article.status),
      bg: "#f1f5f9",
      color: "#64748b",
    };
    const typeLabel = knowledgeTypeLabel(article.type);
    return (
      <div
        style={{
          background: "white",
          borderRadius: 8,
          border: "1px solid #e5e7eb",
          padding: 16,
          display: "flex",
          flexDirection: "column",
          gap: 12,
          transition: "all 0.2s",
          cursor: "pointer",
        }}
        onClick={() => void openDetail(article)}
      >
        {/* Imagem do artigo (se houver) */}
        {resolveUploadSrc(article.imagePath) && (
          <div style={{ width: "100%", height: "160px", borderRadius: 6, overflow: "hidden", background: "#f3f4f6" }}>
            <img
              src={resolveUploadSrc(article.imagePath)!}
              alt={article.title}
              style={{ width: "100%", height: "100%", objectFit: "cover" }}
              onError={(e) => {
                (e.target as HTMLImageElement).style.display = "none";
              }}
            />
          </div>
        )}

        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 6, flexWrap: "wrap" }}>
            <StatusChip
              label={categoryNameOf(article.categoryId)}
              background="#eff6ff"
              color="#3b82f6"
              fontSize={10}
              fontWeight={600}
            />
            {typeLabel && (
              <StatusChip label={typeLabel} background="#f5f3ff" color="#7c3aed" fontSize={10} fontWeight={600} />
            )}
            {article.pendingReview && (
              <StatusChip label="Revisar" background="#fef3c7" color="#92400e" fontSize={10} fontWeight={600} />
            )}
          </div>
          <StatusChip label={status.label} background={status.bg} color={status.color} fontSize={10} fontWeight={600} />
        </div>
        <div style={{ fontSize: 14, fontWeight: 600, lineHeight: 1.4 }}>{article.title}</div>
        {article.description && (
          <div style={{ fontSize: 13, color: "#374151", lineHeight: 1.4 }}>{article.description}</div>
        )}
        <div
          style={{
            fontSize: 12,
            color: "#6b7280",
            lineHeight: 1.4,
            display: "-webkit-box",
            WebkitLineClamp: 3,
            WebkitBoxOrient: "vertical",
            overflow: "hidden",
          }}
        >
          {article.content}
        </div>
        {article.tags.length > 0 && (
          <div style={{ display: "flex", flexWrap: "wrap", gap: 4 }}>
            {article.tags.map((tag) => (
              <button
                key={tag}
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  setTagFilter(tag);
                  setPage(1);
                }}
                title={`Filtrar por #${tag}`}
                style={{
                  fontSize: 10,
                  background: "#f1f5f9",
                  color: "#475569",
                  border: "1px solid #e2e8f0",
                  borderRadius: 999,
                  padding: "2px 8px",
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
                  fontSize: 10,
                  background: "#f0fdfa",
                  color: "#0f766e",
                  border: "1px solid #ccfbf1",
                  borderRadius: 999,
                  padding: "2px 8px",
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
                onClick={(e) => {
                  e.stopPropagation();
                  setAuthorFilter(article.authorId as string);
                  setPage(1);
                }}
                title="Filtrar por este autor"
                style={{
                  fontSize: 11,
                  color: "#9ca3af",
                  background: "none",
                  border: "none",
                  padding: 0,
                  cursor: "pointer",
                  textDecoration: "underline",
                }}
              >
                Autor: {article.authorName}
              </button>
            )}
            {article.responsibleName && <span>Responsável: {article.responsibleName}</span>}
          </div>
        )}
        {/* Ações (Operate) — editar / trocar status / excluir. */}
        <Can level="operate">
          <div style={{ display: "flex", gap: 6, marginTop: 4, flexWrap: "wrap" }}>
            <Button variant="ghost" onClick={(e) => { e.stopPropagation(); startEdit(article); }} style={{ padding: "4px 10px", fontSize: 12 }}>
              Editar
            </Button>
            {knowledgeNextStatuses(article.status).length > 0 && (
              <Button variant="ghost" onClick={(e) => { e.stopPropagation(); openStatus(article); }} style={{ padding: "4px 10px", fontSize: 12 }}>
                Status
              </Button>
            )}
            <Button variant="ghost" onClick={(e) => { e.stopPropagation(); setDeleteTarget(article); }} style={{ padding: "4px 10px", fontSize: 12 }}>
              Excluir
            </Button>
          </div>
        </Can>
      </div>
    );
  }

  /** Colunas da visão tabela (mesmo conteúdo das 3 visões). */
  function actionsBody(article: KnowledgeArticle) {
    return (
      <Can level="operate">
        <div className="flex gap-1">
          <Button variant="ghost" onClick={(event) => { event.stopPropagation(); startEdit(article); }} style={{ padding: "4px 8px", fontSize: 12 }}>
            Editar
          </Button>
          {knowledgeNextStatuses(article.status).length > 0 && (
            <Button variant="ghost" onClick={(event) => { event.stopPropagation(); openStatus(article); }} style={{ padding: "4px 8px", fontSize: 12 }}>
              Status
            </Button>
          )}
          <Button variant="ghost" onClick={(event) => { event.stopPropagation(); setDeleteTarget(article); }} style={{ padding: "4px 8px", fontSize: 12 }}>
            Excluir
          </Button>
        </div>
      </Can>
    );
  }

  function imageBody(article: KnowledgeArticle) {
    const src = resolveUploadSrc(article.imagePath);
    if (!src) return <span style={{ color: "#9ca3af", fontSize: 12 }}>—</span>;
    return (
      <img
        src={src}
        alt={article.title}
        style={{ width: 48, height: 48, borderRadius: 6, objectFit: "cover" }}
        onError={(e) => {
          (e.target as HTMLImageElement).style.display = "none";
        }}
      />
    );
  }

  function statusBody(article: KnowledgeArticle) {
    const visual = articleStatusConfig[article.status] ?? {
      label: String(article.status),
      bg: "#f1f5f9",
      color: "#64748b",
    };
    return (
      <StatusChip
        label={visual.label}
        background={visual.bg}
        color={visual.color}
        fontSize={10}
        fontWeight={600}
        style={{ whiteSpace: "nowrap", padding: "2px 6px" }}
      />
    );
  }

  return (
    <ListPageShell
      title="Base de Conhecimento"
      singular="artigo"
      plural="artigos"
      total={totalCount}
      loading={loading}
      notice={notice ? <NoticeBanner message={notice} onDismiss={() => setNotice(null)} /> : undefined}
      error={error}
      onRetry={() => void load()}
      actions={
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          {/* F24 visões — tabela/grid/quadro. */}
          <ViewSwitcher mode={view} onChange={setView} />
          <form onSubmit={applyTerm} style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
            <SearchInput
              value={termInput}
              onChange={setTermInput}
              placeholder="Buscar artigos..."
              variant="muted"
              width={160}
              onEnter={() => {
                setTerm(termInput.trim());
                setPage(1);
              }}
            />
            <Button type="submit" variant="ghost" style={{ padding: "5px 10px" }}>
              Filtrar
            </Button>
            <Button
              type="button"
              variant="ghost"
              onClick={() => {
                setPendingOnly((value) => !value);
                setPage(1);
              }}
              style={{
                padding: "5px 10px",
                ...(pendingOnly ? { background: "#fef3c7", color: "#92400e", borderColor: "#fde68a" } : {}),
              }}
            >
              <i className="pi pi-clock" style={{ fontSize: 10 }} /> Revisões
            </Button>
            {anyFilterActive && (
              <Button type="button" variant="ghost" onClick={clearFilters} style={{ padding: "5px 10px" }}>
                Limpar
              </Button>
            )}
          </form>
          <Can level="operate">
            <div className="flex gap-2">
              <Button
                variant="primary"
                onClick={() => {
                  setCreating((value) => !value);
                  resetFormError();
                }}
                style={{ padding: "5px 12px" }}
              >
                <i className="pi pi-plus" style={{ fontSize: 10 }} /> Novo Artigo
              </Button>
              <Button variant="ghost" onClick={openCategoryCreate} style={{ padding: "5px 12px" }}>
                <i className="pi pi-folder-open" style={{ fontSize: 10 }} /> Nova Categoria
              </Button>
            </div>
          </Can>
        </div>
      }
    >

      {/* F24-T008 — categorias visuais no topo. O filtro vale para qualquer
          role (leitura); o gerenciamento segue em "Nova Categoria" (Operate). */}
      <div
        style={{
          background: "white",
          border: "1px solid #e5e7eb",
          borderRadius: 8,
          padding: "16px",
          marginBottom: 16,
        }}
      >
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            gap: 8,
            flexWrap: "wrap",
            marginBottom: 12,
          }}
        >
          <div style={{ fontSize: 14, fontWeight: 600, color: "#334155" }}>
            Categorias ({categories.length})
          </div>
          <span style={{ fontSize: 11, color: "#94a3b8" }}>
            {categoryIdFilter ? "Filtrando por categoria" : "Clique em uma categoria para filtrar"}
          </span>
        </div>
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(180px, 1fr))",
            gap: 12,
          }}
        >
          <button
            type="button"
            onClick={() => {
              setCategoryIdFilter("");
              setPage(1);
            }}
            aria-pressed={!categoryIdFilter}
            style={categoryTileStyle(!categoryIdFilter, {
              bg: "#eff6ff",
              color: "#1d4ed8",
              border: "#bfdbfe",
            })}
          >
            <div style={{ fontSize: 13, fontWeight: 600, color: !categoryIdFilter ? "#1d4ed8" : "#334155" }}>
              Todas as categorias
            </div>
            <div style={{ fontSize: 11, color: "#64748b", marginTop: 4 }}>
              {totalCount} no total
            </div>
          </button>
          {categories.map((category) => {
            const accent = knowledgeCategoryAccent(category.name);
            const active = categoryIdFilter === category.id;
            const count = categoryCounts[category.id];
            return (
              <button
                key={category.id}
                type="button"
                onClick={() => {
                  setCategoryIdFilter(category.id);
                  setPage(1);
                }}
                aria-pressed={active}
                style={categoryTileStyle(active, accent)}
                title={category.description ?? undefined}
              >
                <div style={{ display: "flex", alignItems: "center", gap: 6, minWidth: 0 }}>
                  <span
                    aria-hidden
                    style={{
                      width: 8,
                      height: 8,
                      borderRadius: "50%",
                      background: accent.color,
                      flex: "0 0 auto",
                    }}
                  />
                  <div
                    style={{
                      fontSize: 13,
                      fontWeight: 600,
                      color: active ? accent.color : "#334155",
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {category.name}
                  </div>
                </div>
                {category.description && (
                  <div
                    style={{
                      fontSize: 11,
                      color: "#94a3b8",
                      marginTop: 2,
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {category.description}
                  </div>
                )}
                <div style={{ fontSize: 11, color: "#64748b", marginTop: 4 }}>
                  {count === undefined ? "— artigos" : `${count} ${count === 1 ? "artigo" : "artigos"}`}
                </div>
              </button>
            );
          })}
          {categories.length === 0 && (
            <span style={{ fontSize: 13, color: "#9ca3af", gridColumn: "1/-1" }}>
              Nenhuma categoria — crie a primeira em "Nova Categoria".
            </span>
          )}
        </div>
      </div>

      {/* F24-T007/K7 — busca avançada. */}
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
          onChange={(value) => {
            setCategoryIdFilter(value);
            setPage(1);
          }}
          placeholder="Todas as categorias"
          options={[
            { value: "", label: "Todas as categorias" },
            ...categories.map((category) => ({ value: category.id, label: category.name })),
          ]}
          style={{ minWidth: 160, fontSize: 12 }}
          aria-label="Filtrar por categoria"
        />
        <SelectField
          value={authorFilter}
          onChange={(value) => {
            setAuthorFilter(value);
            setPage(1);
          }}
          placeholder="Todos os autores"
          options={[{ value: "", label: "Todos os autores" }, ...authorOptions]}
          style={{ minWidth: 160, fontSize: 12 }}
          aria-label="Filtrar por autor"
        />
        <div style={{ display: "flex", flexWrap: "wrap", gap: 4, alignItems: "center" }}>
          <span style={{ fontSize: 11, color: "#9ca3af" }}>Tipo:</span>
          <Button
            type="button"
            variant="ghost"
            onClick={() => {
              setTypeFilter("");
              setPage(1);
            }}
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
            onClick={() => {
              setTagFilter("");
              setPage(1);
            }}
            style={filterChipStyle(true)}
            title="Remover filtro de tag"
          >
            #{tagFilter} <i className="pi pi-times" style={{ fontSize: 10 }} />
          </Button>
        )}
      </div>

      {creating && (
        <form
          onSubmit={submitCreate}
          style={{
            display: "flex",
            flexDirection: "column",
            gap: 10,
            padding: 14,
            background: "white",
            border: "1px solid #e5e7eb",
            borderRadius: 8,
          }}
        >
          <div style={{ fontSize: 13, fontWeight: 600 }}>Novo artigo</div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 240px", gap: 10 }}>
            <FormField label="Título" error={errorFor("title")}>
              <input
                value={title}
                onChange={(event) => setTitle(event.target.value)}
                placeholder="Título * (máx. 200)"
                autoFocus
                maxLength={200}
                style={{ width: "100%", padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
              />
            </FormField>
            <FormField label="Categoria">
              <SelectField
                value={categoryId}
                onChange={setCategoryId}
                placeholder="Sem categoria"
                options={categorySelectOptions}
                style={{ width: "100%" }}
              />
            </FormField>
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
            <FormField label="Tipo">
              <SelectField
                value={articleType}
                onChange={setArticleType}
                placeholder="Sem tipo"
                options={[{ value: "", label: "Sem tipo" }, ...KNOWLEDGE_TYPE_OPTIONS]}
                aria-label="Tipo do artigo"
                style={{ width: "100%" }}
              />
            </FormField>
            <FormField label="Responsável">
              <SelectField
                value={responsibleId}
                onChange={setResponsibleId}
                options={userOptions}
                aria-label="Responsável pelo artigo"
                style={{ width: "100%" }}
              />
            </FormField>
          </div>
          <FormField label="Descrição (opcional)">
            <input
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="Descrição (máx. 500) — resumo do que este artigo cobre"
              maxLength={500}
              style={{ width: "100%", padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
          </FormField>
          <FormField label="Tags">
            <input
              value={tagsInput}
              onChange={(event) => setTagsInput(event.target.value)}
              placeholder="Tags separadas por vírgula (máx. 20, até 50 caracteres cada)"
              style={{ width: "100%", padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
          </FormField>
          <FormField label="Imagem (opcional)">
            <ImageUpload
              value={imagePath}
              onChange={setImagePath}
              upload={uploadKnowledgeImage}
            />
          </FormField>
          <FormField label="Conteúdo" error={errorFor("content")}>
            <textarea
              value={content}
              onChange={(event) => setContent(event.target.value)}
              placeholder="Conteúdo * (máx. 20000)"
              rows={6}
              style={{
                width: "100%",
                padding: "7px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
                resize: "vertical",
                fontFamily: "inherit",
              }}
            />
          </FormField>
          {/* `field` setado = o recado já está no campo dele e o formulário
              fica calado; sem `field` (recusa do servidor) é aqui que ele
              aparece. */}
          <FormError message={field ? "" : (formError ?? "")} />
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <Button
              type="button"
              variant="ghost"
              onClick={() => {
                setCreating(false);
                resetFormError();
              }}
              style={{ padding: "6px 14px" }}
            >
              Cancelar
            </Button>
            <Button type="submit" variant="primary" disabled={saving} style={{ padding: "6px 14px" }}>
              {saving ? "Salvando…" : "Criar artigo"}
            </Button>
          </div>
        </form>
      )}

      {/* F24 visões — tabela / grid de cards / quadro por status. */}
      <EntityView<KnowledgeArticle>
        mode={view}
        items={articles}
        getKey={(article) => article.id}
        emptyMessage={error ? "—" : "Nenhum artigo encontrado"}
        kanban={{
          groupOf: (article) => String(article.status),
          columns: Object.entries(articleStatusConfig).map(([key, value]) => ({
            key,
            label: value.label,
          })),
        }}
        table={
          <DataTablePanel
            value={articles}
            dataKey="id"
            loading={loading}
            emptyMessage={error ? "—" : "Nenhum artigo encontrado"}
            onRowClick={(event) => void openDetail(event.data as KnowledgeArticle)}
            rowClassName={() => "cursor-pointer"}
          >
            <Column header="Imagem" body={imageBody} style={{ width: 70 }} />
            <Column
              header="Título"
              body={(article: KnowledgeArticle) => (
                <div>
                  <div style={{ fontWeight: 500, fontSize: 13 }}>{article.title}</div>
                  {article.description && (
                    <div style={{ fontSize: 11, color: "#94a3b8" }}>{article.description}</div>
                  )}
                </div>
              )}
              style={{ minWidth: 240 }}
            />
            <Column header="Categoria" body={(article: KnowledgeArticle) => categoryNameOf(article.categoryId)} style={{ width: 160 }} />
            <Column
              header="Tipo"
              body={(article: KnowledgeArticle) => knowledgeTypeLabel(article.type) ?? "—"}
              style={{ width: 160 }}
            />
            <Column header="Status" body={statusBody} style={{ width: 130 }} />
            <Column header="Autor" body={(article: KnowledgeArticle) => article.authorName ?? "—"} style={{ width: 150 }} />
            <Column
              header="Revisão"
              body={(article: KnowledgeArticle) =>
                article.pendingReview ? (
                  <StatusChip label="Revisar" background="#fef3c7" color="#92400e" fontSize={10} fontWeight={600} />
                ) : (
                  "—"
                )
              }
              style={{ width: 100 }}
            />
            <Column header="" body={actionsBody} style={{ width: 190 }} />
          </DataTablePanel>
        }
        renderCard={renderCard}
      />

      {view !== "table" && loading && articles.length === 0 && (
        <LoadingBlock style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }} />
      )}

      {totalCount > PAGE_SIZE && (
        <Pagination
          page={page}
          totalPages={totalPages}
          disabled={loading}
          onPage={setPage}
        />
      )}

      {/* ── Diálogos ─────────────────────────────────────────────────── */}

      {/* F24-T008 — detalhe ao clicar no card/linha (leitura para todos). */}
      <Dialog
        visible={detail !== null}
        onHide={() => setDetail(null)}
        header={detail?.title ?? "Artigo"}
        width={640}
      >
        {detail && detailStatus && (
          <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            {resolveUploadSrc(detail.imagePath) && (
              <img
                src={resolveUploadSrc(detail.imagePath)!}
                alt={detail.title}
                style={{
                  width: "100%",
                  maxHeight: 240,
                  objectFit: "cover",
                  borderRadius: 8,
                  background: "#f3f4f6",
                }}
                onError={(event) => {
                  (event.target as HTMLImageElement).style.display = "none";
                }}
              />
            )}

            <div style={{ display: "flex", alignItems: "center", flexWrap: "wrap", gap: 6 }}>
              <StatusChip label={categoryNameOf(detail.categoryId)} background="#eff6ff" color="#3b82f6" fontSize={10} fontWeight={600} />
              {knowledgeTypeLabel(detail.type) && (
                <StatusChip label={knowledgeTypeLabel(detail.type)!} background="#f5f3ff" color="#7c3aed" fontSize={10} fontWeight={600} />
              )}
              <StatusChip label={detailStatus.label} background={detailStatus.bg} color={detailStatus.color} fontSize={10} fontWeight={600} />
              <StatusChip label={`v${detail.version}`} background="#f1f5f9" color="#475569" fontSize={10} fontWeight={600} />
              {detail.pendingReview && (
                <StatusChip label="Revisar" background="#fef3c7" color="#92400e" fontSize={10} fontWeight={600} />
              )}
              {detailLoading && (
                <span style={{ fontSize: 11, color: "#94a3b8" }}>Atualizando…</span>
              )}
            </div>

            {detail.description && (
              <div style={{ fontSize: 13, color: "#475569" }}>{detail.description}</div>
            )}

            <div
              style={{
                whiteSpace: "pre-wrap",
                fontSize: 13,
                lineHeight: 1.6,
                color: "#1f2937",
                maxHeight: "45vh",
                overflowY: "auto",
                paddingRight: 4,
              }}
            >
              {detail.content}
            </div>

            {detail.tags.length > 0 && (
              <div style={{ display: "flex", flexWrap: "wrap", gap: 4 }}>
                {detail.tags.map((tag) => (
                  <button
                    key={tag}
                    type="button"
                    onClick={() => {
                      setTagFilter(tag);
                      setPage(1);
                      setDetail(null);
                    }}
                    title={`Filtrar por #${tag}`}
                    style={{
                      fontSize: 10,
                      background: "#f1f5f9",
                      color: "#475569",
                      border: "1px solid #e2e8f0",
                      borderRadius: 999,
                      padding: "2px 8px",
                      cursor: "pointer",
                    }}
                  >
                    #{tag}
                  </button>
                ))}
              </div>
            )}

            {detail.origins.length > 0 && (
              <div style={{ display: "flex", alignItems: "center", flexWrap: "wrap", gap: 4 }}>
                <span style={{ fontSize: 10, color: "#9ca3af" }}>Origem:</span>
                {detail.origins.map((origin) => (
                  <span
                    key={`${origin.type}:${origin.id}`}
                    title={origin.id}
                    style={{
                      fontSize: 10,
                      background: "#f0fdfa",
                      color: "#0f766e",
                      border: "1px solid #ccfbf1",
                      borderRadius: 999,
                      padding: "2px 8px",
                    }}
                  >
                    {originLabels[origin.type] ?? origin.type}
                  </span>
                ))}
              </div>
            )}

            {(detail.authorName || detail.responsibleName) && (
              <div style={{ fontSize: 11, color: "#64748b", display: "flex", gap: 12, flexWrap: "wrap" }}>
                {detail.authorName && <span>Autor: {detail.authorName}</span>}
                {detail.responsibleName && <span>Responsável: {detail.responsibleName}</span>}
              </div>
            )}

            {detail.nextReviewAt && (
              <div style={{ fontSize: 11, color: "#92400e" }}>
                Próxima revisão: {formatDate(detail.nextReviewAt)}
                {detail.reviewIntervalDays ? ` (a cada ${detail.reviewIntervalDays} dias)` : ""}
                {detail.reviewResponsibleName ? ` — ${detail.reviewResponsibleName}` : ""}
              </div>
            )}

            {detail.deviceIds.length + detail.incidentIds.length + detail.serviceIds.length > 0 && (
              <div style={{ fontSize: 11, color: "#64748b", display: "flex", gap: 12, flexWrap: "wrap" }}>
                {detail.deviceIds.length > 0 && <span>Devices: {detail.deviceIds.length}</span>}
                {detail.incidentIds.length > 0 && <span>Chamados: {detail.incidentIds.length}</span>}
                {detail.serviceIds.length > 0 && <span>Serviços: {detail.serviceIds.length}</span>}
              </div>
            )}

            <div style={{ display: "flex", gap: 8, justifyContent: "flex-end", flexWrap: "wrap" }}>
              <Can level="operate">
                <Button
                  type="button"
                  variant="ghost"
                  onClick={() => {
                    startEdit(detail);
                    setDetail(null);
                  }}
                  style={{ padding: "6px 14px" }}
                >
                  Editar
                </Button>
                {knowledgeNextStatuses(detail.status).length > 0 && (
                  <Button
                    type="button"
                    variant="ghost"
                    onClick={() => {
                      openStatus(detail);
                      setDetail(null);
                    }}
                    style={{ padding: "6px 14px" }}
                  >
                    Status
                  </Button>
                )}
              </Can>
              <Button type="button" variant="primary" onClick={() => setDetail(null)} style={{ padding: "6px 14px" }}>
                Fechar
              </Button>
            </div>
          </div>
        )}
      </Dialog>

      {/* Edição de artigo — motivo obrigatório (K3: vira versão). */}
      <Dialog
        visible={editing !== null}
        onHide={() => setEditing(null)}
        header="Editar artigo"
        width={560}
      >
        <form onSubmit={submitEdit} style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          <FormField label="Título" error={errorForEdit("editTitle")}>
            <input
              value={editTitle}
              onChange={(event) => setEditTitle(event.target.value)}
              maxLength={200}
              autoFocus
              style={{ width: "100%", padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
          </FormField>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
            <FormField label="Categoria">
              <SelectField value={editCategoryId} onChange={setEditCategoryId} options={categorySelectOptions} aria-label="Categoria" style={{ width: "100%" }} />
            </FormField>
            <FormField label="Tipo">
              <SelectField
                value={editType}
                onChange={setEditType}
                placeholder="Sem tipo"
                options={[{ value: "", label: "Sem tipo" }, ...KNOWLEDGE_TYPE_OPTIONS]}
                aria-label="Tipo do artigo"
                style={{ width: "100%" }}
              />
            </FormField>
          </div>
          <FormField label="Responsável">
            <SelectField value={editResponsibleId} onChange={setEditResponsibleId} options={userOptions} aria-label="Responsável" style={{ width: "100%" }} />
          </FormField>
          <FormField label="Descrição">
            <input
              value={editDescription}
              onChange={(event) => setEditDescription(event.target.value)}
              maxLength={500}
              style={{ width: "100%", padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
          </FormField>
          <FormField label="Tags (vírgula)">
            <input
              value={editTagsInput}
              onChange={(event) => setEditTagsInput(event.target.value)}
              style={{ width: "100%", padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
          </FormField>
          <FormField label="Imagem (opcional)">
            <ImageUpload
              value={editImagePath}
              onChange={setEditImagePath}
              upload={uploadKnowledgeImage}
            />
          </FormField>
          <FormField label="Conteúdo" error={errorForEdit("editContent")}>
            <textarea
              value={editContent}
              onChange={(event) => setEditContent(event.target.value)}
              rows={6}
              style={{
                width: "100%",
                padding: "7px 10px",
                fontSize: 13,
                border: "1px solid #d1d5db",
                borderRadius: 6,
                resize: "vertical",
                fontFamily: "inherit",
              }}
            />
          </FormField>
          {/* K3: o motivo vira nova versão — o único campo obrigatório que não
              existia antes. `FormField` substitui o `<label>` cru que estava
              aqui (mesmo wrapper que ele foi criado para unificar); o destaque
              âmbar sobrevive no `*` e na borda do input. */}
          <FormField label="Motivo da alteração * (vira nova versão)" error={errorForEdit("editReason")}>
            <input
              value={editReason}
              onChange={(event) => setEditReason(event.target.value)}
              maxLength={200}
              placeholder="Ex.: corrige passo do procedimento"
              style={{ width: "100%", padding: "7px 10px", fontSize: 13, border: "1px solid #fcd34d", borderRadius: 6 }}
            />
          </FormField>
          {/* `editField` setado = o recado está no campo dele; sem `field`
              (recusa do servidor) é aqui que ele aparece. */}
          <FormError message={editField ? "" : (editError ?? "")} />
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <Button type="button" variant="ghost" onClick={() => setEditing(null)} style={{ padding: "6px 14px" }}>
              Cancelar
            </Button>
            <Button type="submit" variant="primary" disabled={editSaving} style={{ padding: "6px 14px" }}>
              {editSaving ? "Salvando…" : "Salvar"}
            </Button>
          </div>
        </form>
      </Dialog>

      {/* Troca de status — só transições válidas (ADR-034). */}
      <Dialog
        visible={statusTarget !== null}
        onHide={() => setStatusTarget(null)}
        header="Alterar status"
        width={400}
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          <div style={{ fontSize: 12, color: "#64748b" }}>
            {statusTarget && (
              <>
                <strong>{statusTarget.title}</strong> está como{" "}
                <strong>{articleStatusLabel(statusTarget.status)}</strong>.
              </>
            )}
          </div>
          <SelectField
            value={newStatus}
            onChange={setNewStatus}
            placeholder="Novo status"
            options={[{ value: "", label: "Novo status" }, ...statusTargets]}
            aria-label="Novo status"
          />
          {statusError && <FormError message={statusError} />}
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <Button type="button" variant="ghost" onClick={() => setStatusTarget(null)} style={{ padding: "6px 14px" }}>
              Cancelar
            </Button>
            <Button
              type="button"
              variant="primary"
              disabled={!newStatus || statusSaving}
              onClick={() => void submitStatus()}
              style={{ padding: "6px 14px" }}
            >
              {statusSaving ? "Aplicando…" : "Aplicar"}
            </Button>
          </div>
        </div>
      </Dialog>

      {/* CRUD de categoria — criar/editar. */}
      <Dialog
        visible={categoryForm !== null}
        onHide={() => setCategoryForm(null)}
        header={categoryForm?.mode === "edit" ? "Editar categoria" : "Nova categoria"}
        width={400}
      >
        <form onSubmit={submitCategory} style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          <FormField label="Nome *" error={errorForCategory("categoryName")}>
            <input
              value={categoryName}
              onChange={(event) => setCategoryName(event.target.value)}
              maxLength={100}
              autoFocus
              style={{ width: "100%", padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
          </FormField>
          <FormField label="Descrição">
            <input
              value={categoryDescription}
              onChange={(event) => setCategoryDescription(event.target.value)}
              maxLength={500}
              style={{ width: "100%", padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
          </FormField>
          {/* `categoryField` setado = o recado está no campo dele; sem `field`
              (recusa do servidor) é aqui que ele aparece. */}
          <FormError message={categoryField ? "" : (categoryError ?? "")} />
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <Button type="button" variant="ghost" onClick={() => setCategoryForm(null)} style={{ padding: "6px 14px" }}>
              Cancelar
            </Button>
            <Button type="submit" variant="primary" disabled={categorySaving} style={{ padding: "6px 14px" }}>
              {categorySaving ? "Salvando…" : "Salvar"}
            </Button>
          </div>
        </form>
      </Dialog>

      {/* Exclusão de artigo. */}
      <ConfirmDialog
        visible={deleteTarget !== null}
        onHide={() => setDeleteTarget(null)}
        onConfirm={() => void confirmDeleteArticle()}
        header="Excluir artigo"
        message={`Excluir o artigo "${deleteTarget?.title}"? Esta ação não pode ser desfeita.`}
        confirmLabel="Excluir"
      />

      {/* Exclusão de categoria. */}
      <ConfirmDialog
        visible={deleteCategoryTarget !== null}
        onHide={() => setDeleteCategoryTarget(null)}
        onConfirm={() => void confirmDeleteCategory()}
        header="Excluir categoria"
        message={`Excluir a categoria "${deleteCategoryTarget?.name}"? Os artigos dela ficam sem categoria.`}
        confirmLabel="Excluir"
      />
    </ListPageShell>
  );
}
