/**
 * Toggle reutilizável (Fase B′ docs/28 §11): wrapper fino do `InputSwitch`
 * do PrimeReact sobre os `uni-toggle-switch` / `role="switch"` manuais
 * (Settings ~15, Notifications prefs). Paleta via `prime-overrides.css`.
 */
import { InputSwitch, type InputSwitchChangeEvent } from "primereact/inputswitch";
import type { CSSProperties } from "react";

export function SwitchField({
  checked,
  onChange,
  disabled = false,
  title,
  "aria-label": ariaLabel,
  className,
  style,
}: {
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
  title?: string;
  "aria-label"?: string;
  className?: string;
  style?: CSSProperties;
}) {
  return (
    <InputSwitch
      checked={checked}
      disabled={disabled}
      title={title}
      aria-label={ariaLabel}
      className={["uni-switch", className].filter(Boolean).join(" ")}
      style={style}
      onChange={(event: InputSwitchChangeEvent) => onChange(Boolean(event.value))}
    />
  );
}
