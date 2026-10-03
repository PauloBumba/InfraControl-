/**
 * Paginação manual de lista (Fase A do levantamento docs/28): o bloco
 * "Página X de Y + Anterior/Próxima" que era reescrito em 7 páginas.
 * Páginas que migrarem para `DataTable` do PrimeReact usam o paginator
 * nativo — este componente é para quem mantém tabela própria.
 * `disabled` congela os botões durante o carregamento.
 */
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
  const buttonStyle = (isDisabled: boolean) => ({
    padding: "5px 10px",
    borderRadius: 6,
    border: "1px solid #e2e8f0",
    background: "white",
    fontSize: 12,
    color: "#374151",
    cursor: isDisabled ? "default" : "pointer",
    opacity: isDisabled ? 0.5 : 1,
  });

  return (
    <div
      className="flex align-items-center justify-content-end gap-2"
      style={{ fontSize: 12, color: "#64748b" }}
    >
      <span>
        Página {page} de {totalPages}
      </span>
      <button
        type="button"
        onClick={() => onPage(Math.max(1, page - 1))}
        disabled={page <= 1 || disabled}
        style={buttonStyle(page <= 1 || disabled)}
      >
        Anterior
      </button>
      <button
        type="button"
        onClick={() => onPage(Math.min(totalPages, page + 1))}
        disabled={page >= totalPages || disabled}
        style={buttonStyle(page >= totalPages || disabled)}
      >
        Próxima
      </button>
    </div>
  );
}
