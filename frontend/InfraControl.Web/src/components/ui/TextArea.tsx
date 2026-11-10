import type { CSSProperties } from "react";

/**
 * F50-T003 — textarea compartilhado no padrão dos formulários da casa
 * (o mesmo look dos inputs inline: padding 7/10, 13px, raio 6, resize
 * vertical). Antes cada página reescrevia esse estilo à mão — e as três
 * páginas que ganharam campo de texto longo (Alerts/Incidents) usam ele.
 */
export function TextArea({
  value,
  onChange,
  rows = 3,
  maxLength,
  placeholder,
  disabled,
  id,
  ariaLabel,
  autoFocus,
  style,
}: {
  value: string;
  onChange: (value: string) => void;
  rows?: number;
  maxLength?: number;
  placeholder?: string;
  disabled?: boolean;
  id?: string;
  ariaLabel?: string;
  autoFocus?: boolean;
  style?: CSSProperties;
}) {
  return (
    <textarea
      id={id}
      aria-label={ariaLabel}
      value={value}
      onChange={(event) => onChange(event.target.value)}
      rows={rows}
      maxLength={maxLength}
      placeholder={placeholder}
      disabled={disabled}
      autoFocus={autoFocus}
      style={{
        width: "100%",
        padding: "7px 10px",
        fontSize: 13,
        border: "1px solid #d1d5db",
        borderRadius: 6,
        resize: "vertical",
        fontFamily: "inherit",
        ...style,
      }}
    />
  );
}
