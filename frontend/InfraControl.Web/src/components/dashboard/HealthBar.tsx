/**
 * F40-T001 — barra de saúde estilo UniFi: um dot colorido + rótulo por
 * domínio (Internet, Rede, Dispositivos…). Presentacional: a página deriva
 * os `items` dos dados reais; aqui só renderizamos.
 *
 * `unknown` é semântica de "sem dado" (zero ≠ erro — convenção F26 8.2):
 * não confundir com ok.
 */

export type HealthStatus = "ok" | "warning" | "critical" | "unknown";

export interface HealthItem {
  id: string;
  label: string;
  status: HealthStatus;
  /** Linha fina abaixo do estado (ex.: "2 em aberto"). */
  detail?: string;
}

const statusWords: Record<HealthStatus, string> = {
  ok: "Normal",
  warning: "Atenção",
  critical: "Crítico",
  unknown: "Sem dado",
};

export function HealthBar({
  items,
  className,
}: {
  items: HealthItem[];
  className?: string;
}) {
  return (
    <div
      role="group"
      aria-label="Estado geral da infraestrutura"
      className={["uni-dash-health", className].filter(Boolean).join(" ")}
    >
      {items.map((item) => (
        <div key={item.id} className="uni-dash-health-item">
          <span
            className={`uni-dash-health-dot is-${item.status}`}
            aria-hidden="true"
          />
          <div className="uni-dash-health-text">
            <div className="uni-dash-health-label">{item.label}</div>
            <div className="uni-dash-health-status">{statusWords[item.status]}</div>
            {item.detail && (
              <div className="uni-dash-health-detail">{item.detail}</div>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}

/** Estado derivado para chips/legendas reutilizáveis na página. */
export function healthStatusLabel(status: HealthStatus): string {
  return statusWords[status];
}
