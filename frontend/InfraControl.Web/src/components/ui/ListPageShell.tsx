import type { ReactNode } from "react";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { PageHeader } from "@/components/ui/PageHeader";

/**
 * Contagem no padrão das páginas de índice: "0 clientes", "1 cliente",
 * "3 clientes".
 *
 * O plural é SEMPRE escrito pela página, nunca inferido: "usuário" + "s" é
 * "usuárioss", e uma regra de bolso quebraria silenciosamente metade das
 * listas do produto.
 */
export function countLabel(total: number, singular: string, plural: string): string {
  return `${total} ${total === 1 ? singular : plural}`;
}

/**
 * F29-T001B — a casca que toda página de índice recriava à mão.
 *
 * O levantamento achou o mesmo esqueleto em 32 páginas: raiz
 * `flex column gap:16` (33 ocorrências), `PageHeader` com subtítulo
 * `loading ? "Carregando…" : "N Ns"` (14 páginas idênticas) e o bloco
 * `{error && <ErrorBanner … onRetry>}` (31 páginas).
 *
 * Ela NÃO conhece a tabela: o conteúdo entra por `children`, então serve
 * tanto o `DataTable` do PrimeReact quanto a lista própria da AlertsPage.
 * Isso é de propósito — acoplar casca a tabela seria trocar uma duplicação
 * por um casamento que a próxima página não casa.
 */
export function ListPageShell({
  title,
  singular,
  plural,
  total = 0,
  loading = false,
  note,
  notice,
  error,
  onRetry,
  actions,
  footer,
  children,
}: {
  title: string;
  /** Substantivo para "1 …" (ex.: `cliente`). */
  singular: string;
  /** Substantivo para "0 …" e "2 …" (ex.: `clientes`). */
  plural: string;
  /** Total da coleção — não precisa ser o estado do React Query, `0` basta. */
  total?: number;
  loading?: boolean;
  /** Complemento do subtítulo (ex.: `busca "rack"`), já sem o "·". */
  note?: string;
  /** Aviso que não é erro (`NoticeBanner`), renderizado ACIMA do erro. */
  notice?: ReactNode;
  /** Mensagem de erro; enquanto presente, o conteúdo continua visível. */
  error?: string | null;
  onRetry?: () => void;
  actions?: ReactNode;
  /** Rodapé: paginação, total de páginas, legenda. */
  footer?: ReactNode;
  children?: ReactNode;
}) {
  const subtitle = loading
    ? "Carregando…"
    : note
      ? `${countLabel(total, singular, plural)} · ${note}`
      : countLabel(total, singular, plural);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <PageHeader title={title} subtitle={subtitle} actions={actions} />
      {notice}
      {error != null && <ErrorBanner message={error} onRetry={onRetry} />}
      {children}
      {footer}
    </div>
  );
}
