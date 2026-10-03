/**
 * Banner de aviso positivo (Fase B do levantamento docs/28): o bloco verde
 * ("Sessão encerrada", "Preferência salva"…) que era copiado em Sessions,
 * Remote e Incidents — par do `ErrorBanner`, com o mesmo layout. Tom padrão
 * `#f0fdf4`/`#15803d`; `background`/`color` cobrem as variantes legadas
 * (Sessions usa `#dcfce7`/`#166534`). `onDismiss` opcional mostra o ✕; `style`
 * cobre espaçamentos locais (ex.: Remote com `marginBottom`).
 */
import type { CSSProperties } from "react";

export function NoticeBanner({
  message,
  onDismiss,
  background = "#f0fdf4",
  color = "#15803d",
  style,
}: {
  message: string;
  onDismiss?: () => void;
  background?: string;
  color?: string;
  style?: CSSProperties;
}) {
  return (
    <div
      role="status"
      className="flex align-items-center gap-2"
      style={{
        padding: "10px 12px",
        borderRadius: 6,
        fontSize: 13,
        background,
        color,
        ...style,
      }}
    >
      <i className="pi pi-check-circle" />
      <span>{message}</span>
      {onDismiss && (
        <button
          type="button"
          onClick={onDismiss}
          style={{
            marginLeft: "auto",
            background: "none",
            border: "none",
            color: "inherit",
            cursor: "pointer",
          }}
          aria-label="Dispensar aviso"
        >
          <i className="pi pi-times" />
        </button>
      )}
    </div>
  );
}
