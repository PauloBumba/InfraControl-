/**
 * Chip de status/severidade (Fase B do levantamento docs/28): o `<span>` de
 * cor que era reescrito em 14 páginas (~80 chips). O vocabulário e a paleta
 * continuam na página (cada tela mantém seu `severityConfig`/`statusConfig` —
 * zero mudança visual); aqui só se centraliza o markup.
 * `fontWeight` 600 para chips "fortes" (ex.: severidade da AlertsPage).
 */
export function StatusChip({
  label,
  background,
  color,
  fontWeight = 500,
  fontSize = 11,
  title,
}: {
  label: string;
  background: string;
  color: string;
  fontWeight?: number;
  fontSize?: number;
  title?: string;
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
      }}
    >
      {label}
    </span>
  );
}
