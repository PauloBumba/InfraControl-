/**
 * Botão padrão UniFi (docs/28): centraliza as variantes `.uni-btn-*` que
 * estavam espalhadas em inline styles. Mantém o visual do app; páginas
 * novas e footers de Dialog devem preferir este wrapper.
 */
import type { ButtonHTMLAttributes, ReactNode } from "react";

type ButtonVariant = "primary" | "ghost" | "danger" | "warning" | "info";

const VARIANT_CLASS: Record<ButtonVariant, string> = {
  primary: "uni-btn-primary",
  ghost: "uni-btn-ghost",
  danger: "uni-btn-danger",
  warning: "uni-btn-warning",
  info: "uni-btn-info",
};

export function Button({
  variant = "primary",
  icon,
  children,
  className,
  type = "button",
  ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
  icon?: string;
  children?: ReactNode;
}) {
  return (
    <button
      type={type}
      className={["uni-btn", VARIANT_CLASS[variant], className].filter(Boolean).join(" ")}
      {...rest}
    >
      {icon && <i className={icon} style={{ fontSize: 12 }} />}
      {children}
    </button>
  );
}
