/**
 * Select reutilizável (Fase B docs/28): wrapper fino do `Dropdown` do
 * PrimeReact sobre o `<select>` nativo repetido em ~8 páginas. A paleta
 * UniFi vem de `prime-overrides.css` (`.uni-select`). Options no formato
 * `{ label, value }` — value string (filtros/enums do front).
 */
import { Dropdown, type DropdownChangeEvent } from "primereact/dropdown";
import type { CSSProperties } from "react";

export type SelectOption = { label: string; value: string };

export function SelectField({
  value,
  onChange,
  options,
  placeholder,
  disabled = false,
  className,
  style,
  title,
  "aria-label": ariaLabel,
}: {
  value: string;
  onChange: (value: string) => void;
  options: SelectOption[];
  placeholder?: string;
  disabled?: boolean;
  className?: string;
  style?: CSSProperties;
  title?: string;
  "aria-label"?: string;
}) {
  return (
    <Dropdown
      value={value}
      onChange={(event: DropdownChangeEvent) => onChange(String(event.value ?? ""))}
      options={options}
      optionLabel="label"
      optionValue="value"
      placeholder={placeholder}
      disabled={disabled}
      className={["uni-select", className].filter(Boolean).join(" ")}
      style={style}
      title={title}
      aria-label={ariaLabel}
    />
  );
}
