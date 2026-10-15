import type { ReactNode } from "react";

/**
 * F26-T002 — Hero reutilizável para páginas públicas
 * Similar ao _PageHero.cshtml do Sentinel
 *
 * Props:
 * - badge: texto do badge (opcional)
 * - badgeIcon: classe do ícone PrimeIcons (opcional)
 * - title: título principal
 * - subtitle: subtítulo/descrição (opcional)
 * - children: conteúdo adicional após o hero (opcional)
 */
interface PublicHeroProps {
  badge?: string;
  badgeIcon?: string;
  title: string;
  subtitle?: string;
  children?: ReactNode;
}

export function PublicHero({
  badge,
  badgeIcon,
  title,
  subtitle,
  children,
}: PublicHeroProps) {
  return (
    <section className="uni-public-hero">
      {badge && (
        <div className="uni-public-hero-badge">
          {badgeIcon && <i className={badgeIcon} />}
          {badge}
        </div>
      )}
      <h1>{title}</h1>
      {subtitle && <p className="uni-public-lead">{subtitle}</p>}
      {children}
    </section>
  );
}
