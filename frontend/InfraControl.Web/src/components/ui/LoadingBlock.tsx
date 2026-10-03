/**
 * Estado de carregamento centralizado (Fase B do levantamento docs/28): o
 * "Carregando…" que era reescrito em 19 páginas com o mesmo estilo
 * (centralizado, `#94a3b8`, 13px, padding 24).
 * - sem props  → `<div>` para listas/painéis;
 * - `colSpan`   → linha de tabela (`<tr><td colSpan>`) dentro do `<tbody>`.
 * O "Carregando…" do subtítulo do cabeçalho (contagem) fica na página — é
 * texto do `PageHeader`, não bloco de conteúdo.
 */
export function LoadingBlock({ colSpan }: { colSpan?: number }) {
  const style = {
    textAlign: "center" as const,
    padding: 24,
    color: "#94a3b8",
    fontSize: 13,
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
