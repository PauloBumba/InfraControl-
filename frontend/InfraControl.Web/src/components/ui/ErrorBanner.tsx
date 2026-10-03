/**
 * Banner de erro inline (Fase A do levantamento docs/28): o mesmo bloco que
 * era copiado em 19 arquivos ("Não foi possível falar com o servidor…" +
 * "Tentar de novo"). `onRetry` é opcional — sem ele, vira só um aviso
 * (ex.: erro de formulário com validação). `color` cobre a variante legada
 * `#dc2626` (Sessions); `style` cobre espaçamentos locais (ex.: Remote com
 * `marginBottom`). `role="alert"` para leitores de tela (já era o padrão da
 * Sessions). pt-BR por contrato de UI.
 */
import type { CSSProperties } from "react";

export function ErrorBanner({
  message,
  onRetry,
  color = "#b91c1c",
  style,
}: {
  message: string;
  onRetry?: () => void;
  color?: string;
  style?: CSSProperties;
}) {
  return (
    <div
      role="alert"
      className="flex align-items-center gap-2"
      style={{
        padding: "10px 12px",
        borderRadius: 6,
        fontSize: 13,
        background: "#fef2f2",
        color,
        ...style,
      }}
    >
      <i className="pi pi-exclamation-circle" />
      <span>{message}</span>
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          style={{
            marginLeft: "auto",
            background: "none",
            border: "none",
            color: "inherit",
            cursor: "pointer",
            fontWeight: 600,
          }}
        >
          Tentar de novo
        </button>
      )}
    </div>
  );
}
