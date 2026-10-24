import type { ReactNode } from "react";

/**
 * F40-T001 — card de KPI clicável do dashboard (substitui o MiniStat com
 * estilos inline da DashboardPage). Tema via classes `uni-dash-stat*`
 * (styles/pages/dashboard.css) — sem hex fixo, dark mode automático.
 */

export type StatTone = "blue" | "purple" | "green" | "warning" | "critical";

const toneClass: Record<StatTone, string> = {
  blue: "",
  purple: "is-purple",
  green: "is-green",
  warning: "is-warning",
  critical: "is-critical",
};

export function DeviceHealthCard({
  icon,
  label,
  value,
  color,
  tone,
  sub,
  onClick,
  leading,
}: {
  icon: string;
  label: string;
  value: string;
  /** Legado: cor do ícone em hex ainda aceita (paleta da página). */
  color?: string;
  /** Preferido: tom semântico mapeado para classes (dark-safe). */
  tone?: StatTone;
  sub?: string;
  onClick?: () => void;
  /** Slot opcional à esquerda (ex.: chip de severidade). */
  leading?: ReactNode;
}) {
  const content = (
    <>
      <div
        className={`uni-dash-stat-icon ${tone ? toneClass[tone] : ""}`.trim()}
        style={color && !tone ? { background: `${color}12`, color } : undefined}
      >
        <i className={`pi ${icon}`} />
      </div>
      <div className="uni-dash-stat-text">
        <div className="uni-dash-stat-label">{label}</div>
        <div className="uni-dash-stat-value">{value}</div>
        {sub && <div className="uni-dash-stat-sub">{sub}</div>}
      </div>
    </>
  );

  if (onClick) {
    return (
      <button type="button" className="uni-dash-stat" onClick={onClick}>
        {leading}
        {content}
      </button>
    );
  }

  return (
    <div className="uni-dash-stat">
      {leading}
      {content}
    </div>
  );
}
