/**
 * Estado de carregamento centralizado (Fase B do levantamento docs/28): o
 * "Carregando…" que era reescrito em 19 páginas com o mesmo estilo
 * (centralizado, `#94a3b8`, 13px, padding 24).
 * - sem props  → `<div>` para listas/painéis;
 * - `colSpan`   → linha de tabela (`<tr><td colSpan>`) dentro do `<tbody>`;
 * - `style`     → variações locais preservadas (ex.: `gridColumn` em grids,
 *                 card branco do inventário, compacto do Dashboard).
 * O "Carregando…" do subtítulo do cabeçalho (contagem) fica na página — é
 * texto do `PageHeader`, não bloco de conteúdo.
 */
import type { CSSProperties } from "react";

export function LoadingBlock({
  colSpan,
  style: extraStyle,
}: {
  colSpan?: number;
  style?: CSSProperties;
}) {
  const style: CSSProperties = {
    textAlign: "center",
    padding: 24,
    color: "#94a3b8",
    fontSize: 13,
    ...extraStyle,
  };

  if (colSpan !== undefined) {
    return (
      <tr>
        <td colSpan={colSpan} style={style}>
          Carregando…
        </td>
      </tr>
    );
  }

  return <div style={style}>Carregando…</div>;
}
