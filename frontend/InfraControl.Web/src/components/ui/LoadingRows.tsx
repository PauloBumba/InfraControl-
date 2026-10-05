/**
 * Skeleton de tabela/lista (Fase B docs/28): substitui o "Carregando…"
 * estático por linhas placeholder sem mudar o layout ao carregar.
 * - `rows`/`columns` → grade de Skeleton (dentro de painel/tabela);
 * - sem props extras → bloco simples (compatível com LoadingBlock).
 */
import { Skeleton } from "primereact/skeleton";

export function LoadingRows({
  rows = 5,
  columns = 4,
}: {
  rows?: number;
  columns?: number;
}) {
  return (
    <div className="uni-loading-rows" aria-busy="true" aria-label="Carregando">
      {Array.from({ length: rows }, (_, row) => (
        <div key={row} className="uni-loading-row">
          {Array.from({ length: columns }, (_, col) => (
            <Skeleton
              key={col}
              height="0.75rem"
              borderRadius="4px"
              className="uni-loading-cell"
            />
          ))}
        </div>
      ))}
    </div>
  );
}
