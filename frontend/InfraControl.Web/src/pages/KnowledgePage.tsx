import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Can } from "@/components/auth/Can";
import { Button } from "@/components/ui/Button";
import { Pagination } from "@/components/ui/Pagination";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { FormError } from "@/components/ui/FormField";
import { PageHeader } from "@/components/ui/PageHeader";
import { SearchInput } from "@/components/ui/SearchInput";
import { StatusChip } from "@/components/ui/StatusChip";
import { SelectField } from "@/components/ui/SelectField";
import {
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
  createArticle,
  listArticles,
  listCategories,
  type KnowledgeArticle,
  type KnowledgeCategory,
  type Paged,
} from "@/services/api/knowledge";

/**
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
 */

const PAGE_SIZE = 20;

// F24-T002/K2 (ADR-034): rótulos verbatim da spec docs/32 §7 —
// "Publicada"/"Arquivada" feminino mudou do legado "Publicado"/
// "Arquivado". Números = knowledge.ts (0–2 legados, 3–7 novos).
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

function messageFor(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "Não foi possível falar com o servidor. Tente novamente.";
}

export function KnowledgePage() {
  const [pageData, setPageData] = useState<Paged<KnowledgeArticle> | null>(null);
  const [categories, setCategories] = useState<KnowledgeCategory[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [page, setPage] = useState(1);
  const [term, setTerm] = useState("");
  const [termInput, setTermInput] = useState("");

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
  const [formError, setFormError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setPageData(
        await listArticles({ term: term || undefined, page, pageSize: PAGE_SIZE }),
      );
    } catch (caught) {
      setError(messageFor(caught));
    } finally {
      setLoading(false);
    }
  }, [term, page]);

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

  async function submitCreate(event: FormEvent) {
    event.preventDefault();
    const trimmedTitle = title.trim();
    const trimmedContent = content.trim();
    if (!trimmedTitle) {
      setFormError("Informe o título do artigo.");
      return;
    }
    if (!trimmedContent) {
      setFormError("Informe o conteúdo do artigo.");
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
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
    } catch (caught) {
      setFormError(messageFor(caught));
    } finally {
      setSaving(false);
    }
  }

  const categoryName = (id: string | null): string => {
    if (!id) return "Sem categoria";
    return categories.find((category) => category.id === id)?.name ?? "Sem categoria";
  };

  const articles = pageData?.items ?? [];
  const totalCount = pageData?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader
        title="Base de Conhecimento"
        subtitle={loading ? "Carregando…" : `${totalCount} ${totalCount === 1 ? "artigo" : "artigos"}`}
        actions={
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
            <Can level="operate">
              <Button
                variant="primary"
                onClick={() => { setCreating((value) => !value); setFormError(null); }}
                style={{ padding: "5px 12px" }}
              >
                <i className="pi pi-plus" style={{ fontSize: 10 }} /> Novo Artigo
              </Button>
            </Can>
          </div>
        }
      />

      {notice && <NoticeBanner message={notice} onDismiss={() => setNotice(null)} />}

      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      {creating && (
        <form onSubmit={submitCreate} style={{ display: "flex", flexDirection: "column", gap: 10, padding: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Novo artigo</div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 240px", gap: 10 }}>
            <input
              value={title}
              onChange={(event) => setTitle(event.target.value)}
              placeholder="Título * (máx. 200)"
              autoFocus
              maxLength={200}
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6 }}
            />
            <SelectField
              value={categoryId}
              onChange={setCategoryId}
              placeholder="Sem categoria"
              options={[
                { value: "", label: "Sem categoria" },
                ...categories.map((category) => ({
                  value: category.id,
                  label: category.name,
                })),
              ]}
            />
          </div>
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
          {formError && <FormError message={formError} />}
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <Button
              type="button"
              variant="ghost"
              onClick={() => { setCreating(false); setFormError(null); }}
              style={{ padding: "6px 14px" }}
            >
              Cancelar
            </Button>
            <Button
              type="submit"
              variant="primary"
              disabled={saving}
              style={{ padding: "6px 14px" }}
            >
              {saving ? "Salvando…" : "Criar artigo"}
            </Button>
          </div>
        </form>
      )}

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
                    <span
                      key={tag}
                      style={{
                        fontSize: 10, background: "#f1f5f9", color: "#475569",
                        border: "1px solid #e2e8f0", borderRadius: 999, padding: "1px 7px",
                      }}
                    >
                      #{tag}
                    </span>
                  ))}
                </div>
              )}
              {(article.authorName || article.responsibleName) && (
                <div style={{ fontSize: 11, color: "#9ca3af", display: "flex", gap: 10, flexWrap: "wrap" }}>
                  {article.authorName && <span>Autor: {article.authorName}</span>}
                  {article.responsibleName && <span>Responsável: {article.responsibleName}</span>}
                </div>
              )}
            </div>
          );
        })}
        {!loading && articles.length === 0 && (
          <div style={{ gridColumn: "1 / -1", textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13, background: "white", borderRadius: 8, border: "1px solid #e5e7eb" }}>
            {error ? "—" : "Nenhum artigo encontrado"}
          </div>
        )}
        {loading && articles.length === 0 && (
          <LoadingBlock style={{ gridColumn: "1 / -1", background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }} />
        )}
      </div>

      {totalCount > PAGE_SIZE && (
        <Pagination
          page={page}
          totalPages={totalPages}
          disabled={loading}
          onPage={setPage}
        />
      )}
    </div>
  );
}
