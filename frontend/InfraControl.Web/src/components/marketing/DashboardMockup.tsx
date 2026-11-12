/**
 * F50-T009 — mockup do dashboard no lugar do SVG de rack desenhado à mão
 * (que flutuava com `float` infinito e era o elemento mais "feito às
 * pressas" da landing).
 *
 * É puramente decorativo (`aria-hidden`): mostra a CARA do produto —
 * KPIs, gráfico de latência e lista de dispositivos — sem inventar dado
 * que o produto não tem. Os valores são ilustrativos e nunca aparecem para
 * leitor de tela.
 */
const ROWS = [
  { state: "ok", name: "sw-nucleo-01", detail: "up · 3 ms" },
  { state: "warn", name: "ap-3-andar", detail: "loss 12%" },
  { state: "ok", name: "srv-banco", detail: "up · 11 ms" },
] as const;

const KPIS = [
  { label: "Hosts", value: "142", trend: "+4", tone: "up" },
  { label: "Disponibilidade", value: "99,94%", trend: "7d", tone: "flat" },
  { label: "Alertas abertos", value: "3", trend: "-7", tone: "down" },
] as const;

export function DashboardMockup() {
  return (
    <div className="uni-public-mockup" aria-hidden="true">
      <div className="uni-public-mockup-top">
        <span className="uni-public-mockup-dots">
          <i />
          <i />
          <i />
        </span>
        <span className="uni-public-mockup-path">infracontrol / dashboard</span>
        <span className="uni-public-mockup-live">
          <i className="pi pi-circle-fill" />
          ao vivo
        </span>
      </div>

      <div className="uni-public-mockup-body">
        <div className="uni-public-mockup-kpis">
          {KPIS.map((kpi) => (
            <div key={kpi.label} className="uni-public-kpi">
              <span className="uni-public-kpi-label">{kpi.label}</span>
              <strong className="uni-public-kpi-value">{kpi.value}</strong>
              <span className={`uni-public-kpi-trend uni-public-kpi-trend--${kpi.tone}`}>
                {kpi.trend}
              </span>
            </div>
          ))}
        </div>

        <div className="uni-public-mockup-chart">
          <div className="uni-public-mockup-chart-head">
            <span>Latência — últimas 24h</span>
            <strong>18 ms</strong>
          </div>
          <svg viewBox="0 0 320 96" preserveAspectRatio="none" focusable="false">
            <defs>
              <linearGradient id="uniMockArea" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="#60a5fa" stopOpacity="0.38" />
                <stop offset="100%" stopColor="#60a5fa" stopOpacity="0" />
              </linearGradient>
            </defs>
            <path
              d="M0,72 C18,66 32,44 50,48 C68,52 80,30 98,34 C116,38 128,58 146,54 C164,50 176,26 194,30 C212,34 224,50 242,46 C260,42 272,22 290,26 C304,29 312,40 320,36 L320,96 L0,96 Z"
              fill="url(#uniMockArea)"
            />
            <path
              d="M0,72 C18,66 32,44 50,48 C68,52 80,30 98,34 C116,38 128,58 146,54 C164,50 176,26 194,30 C212,34 224,50 242,46 C260,42 272,22 290,26 C304,29 312,40 320,36"
              fill="none"
              stroke="#93c5fd"
              strokeWidth="2.5"
              strokeLinecap="round"
            />
          </svg>
        </div>

        <ul className="uni-public-mockup-rows">
          {ROWS.map((row) => (
            <li key={row.name}>
              <i className={`uni-public-mockup-dot uni-public-mockup-dot--${row.state}`} />
              <span className="uni-public-mockup-name">{row.name}</span>
              <span className="uni-public-mockup-detail">{row.detail}</span>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
