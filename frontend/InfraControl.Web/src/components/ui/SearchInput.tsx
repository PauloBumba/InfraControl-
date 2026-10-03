/**
 * Campo de busca com ícone (Fase B do levantamento docs/28): o input + `pi-search`
 * que era reescrito em 6 lugares (topbar do AppLayout, Clients, Knowledge, Logs,
 * Users, Audit). Duas variantes para preservar o visual atual de cada tela:
 * - `muted`   → fundo `#f1f5f9`, sem borda (filtros de página);
 * - `outlined`→ fundo branco com borda (barra de ferramentas, ex.: Logs).
 * `onEnter` dispara no Enter (busca imediata); o form de página continua
 * controlando o submit quando houver.
 */
export function SearchInput({
  value,
  onChange,
  placeholder,
  title,
  width = 210,
  variant = "muted",
  mono = false,
  autoFocus = false,
  onEnter,
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  title?: string;
  width?: number;
  variant?: "muted" | "outlined";
  mono?: boolean;
  autoFocus?: boolean;
  onEnter?: () => void;
}) {
  return (
    <div
      className="flex align-items-center gap-2"
      style={{
        background: variant === "muted" ? "#f1f5f9" : "#fff",
        border: variant === "outlined" ? "1px solid #e2e8f0" : "none",
        borderRadius: 6,
        padding: "6px 12px",
      }}
    >
      <i className="pi pi-search" style={{ fontSize: 12, color: "#94a3b8" }} />
      <input
        type="text"
        placeholder={placeholder}
        title={title}
        value={value}
        autoFocus={autoFocus}
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={
          onEnter ? (event) => event.key === "Enter" && onEnter() : undefined
        }
        style={{
          border: "none",
          background: "none",
          outline: "none",
          fontSize: 13,
          width,
          fontFamily: mono ? "monospace" : undefined,
        }}
      />
    </div>
  );
}
