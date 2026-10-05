/**
 * Chip de status/severidade (Fase B docs/28): markup único (span idêntico ao
 * das páginas). A paleta continua na página (cada tela passa background/color
 * — zero mudança visual); aqui só se centraliza o componente. `style` cobre
 * variações locais (ex.: `flexShrink` no Dashboard).
 */
import type { CSSProperties } from "react";

export function StatusChip({
  label,
  background,
  color,
  fontWeight = 500,
  fontSize = 11,
  title,
  style,
}: {
  label: string;
  background: string;
  color: string;
  fontWeight?: number;
  fontSize?: number;
  title?: string;
  style?: CSSProperties;
}) {
  return (
    <span
      title={title}
      style={{
        fontSize,
        fontWeight,
        padding: "2px 8px",
        borderRadius: 4,
        background,
        color,
        ...style,
      }}
    >
      {label}
    </span>
  );
}
