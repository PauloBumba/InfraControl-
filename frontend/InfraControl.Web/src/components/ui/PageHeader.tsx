import type { ReactNode } from "react";

/**
 * Cabeçalho de página padrão (Fase A do levantamento docs/28): título +
 * subtítulo/contempág à esquerda e ações à direita, no layout que todas as
 * páginas recriavam à mão. Usa classes PrimeFlex (`flex`, `align-items-*`,
 * `justify-content-*`, `gap-*`) — decisão do levantamento: PrimeFlex só nos
 * componentes novos, sem reescrever as páginas existentes.
 */
export function PageHeader({
  title,
  subtitle,
  actions,
}: {
  title: string;
  subtitle?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <div className="flex align-items-center justify-content-between flex-wrap gap-2">
      <div>
        <h1 style={{ fontSize: 20, fontWeight: 700 }}>{title}</h1>
        {subtitle != null && (
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>{subtitle}</p>
        )}
      </div>
      {actions != null && (
        <div className="flex align-items-center flex-wrap gap-2">{actions}</div>
      )}
    </div>
  );
}
