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
  ARTICLE_STATUS_ARCHIVED,
  ARTICLE_STATUS_DRAFT,
  ARTICLE_STATUS_PUBLISHED,
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
 */

const PAGE_SIZE = 20;

const statusConfig: Record<number, { label: string; bg: string; color: string }> = {
  [ARTICLE_STATUS_DRAFT]: { label: "Rascunho", bg: "#fef3c7", color: "#d97706" },
  [ARTICLE_STATUS_PUBLISHED]: { label: "Publicado", bg: "#dcfce7", color: "#16a34a" },
  [ARTICLE_STATUS_ARCHIVED]: { label: "Arquivado", bg: "#f1f5f9", color: "#475569" },
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
      await createArticle({
        title: trimmedTitle,
        content: trimmedContent,
        categoryId: categoryId || undefined,
      });
      setCreating(false);
      setTitle("");
      setContent("");
      setCategoryId("");
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
          return (
            <div key={article.id} style={{ background: "white", borderRadius: 8, border: "1px solid #e5e7eb", padding: 16, display: "flex", flexDirection: "column", gap: 8 }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
                <StatusChip
                  label={categoryName(article.categoryId)}
                  background="#eff6ff"
                  color="#3b82f6"
                  fontSize={10}
                  fontWeight={600}
                />
                <StatusChip
                  label={status.label}
                  background={status.bg}
                  color={status.color}
                  fontSize={10}
                  fontWeight={600}
                />
              </div>
              <div style={{ fontSize: 13, fontWeight: 600 }}>{article.title}</div>
              <div style={{
                fontSize: 12, color: "#6b7280", lineHeight: 1.4,
                display: "-webkit-box", WebkitLineClamp: 4, WebkitBoxOrient: "vertical", overflow: "hidden",
              }}>
                {article.content}
              </div>
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
