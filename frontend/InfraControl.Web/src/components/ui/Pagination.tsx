/**
 * Paginação manual de lista (Fase A do levantamento docs/28): o bloco
 * "Página X de Y + Anterior/Próxima" que era reescrito em 7 páginas.
 * Páginas que migrarem para `DataTable` do PrimeReact usam o paginator
 * nativo — este componente é para quem mantém tabela própria.
 * `disabled` congela os botões durante o carregamento.
 */
import { Button } from "@/components/ui/Button";

export function Pagination({
  page,
  totalPages,
  disabled = false,
  onPage,
}: {
  page: number;
  totalPages: number;
  disabled?: boolean;
  onPage: (page: number) => void;
}) {
  return (
    <div
      className="flex align-items-center justify-content-end gap-2"
      style={{ fontSize: 12, color: "#64748b" }}
    >
      <span>
        Página {page} de {totalPages}
      </span>
      <Button
        variant="ghost"
        onClick={() => onPage(Math.max(1, page - 1))}
        disabled={page <= 1 || disabled}
      >
        Anterior
      </Button>
      <Button
        variant="ghost"
        onClick={() => onPage(Math.min(totalPages, page + 1))}
        disabled={page >= totalPages || disabled}
      >
        Próxima
      </Button>
    </div>
  );
}
