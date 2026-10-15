import { Link } from "react-router-dom";

/**
 * F26-T002 — home da landing de vendas.
 *
 * Sem design doc no repositório, o conteúdo foi DERIVADO DO PRODUTO REAL
 * (decisão do usuário 2026-10-11). Cada pilar aponta para capacidade que
 * existe no código — nada de promessa genérica:
 *
 * - Monitorar → Zabbix (hosts/triggers/inventário) e UniFi (WiFi, APs,
 *   clientes ARP/DHCP, insights de tráfego e DPI);
 * - Detectar → o ciclo alerta → incidente → recuperação, com SLA;
 * - Operar   → tarefas, projetos com aprovação, acesso remoto, topologia;
 * - Governar → auditoria, sessões, convites, MFA e permissões por papel.
 */
const PILLARS = [
  {
    icon: "pi pi-eye",
    title: "Monitorar",
    body: "Zabbix para hosts, triggers e inventário; UniFi para WiFi, telemetria de APs, clientes da rede e insights de tráfego.",
  },
  {
    icon: "pi pi-bolt",
    title: "Detectar",
    body: "Alertas viram incidentes com SLA, responsável e histórico de recuperação — sem planilha paralela.",
  },
  {
    icon: "pi pi-wrench",
    title: "Operar",
    body: "Tarefas, projetos com fluxo de aprovação, acesso remoto e a topologia da sua rede.",
  },
  {
    icon: "pi pi-shield",
    title: "Governar",
    body: "Auditoria de cada ação, sessões, convites, MFA e permissões por papel: Viewer, Operator e Admin.",
  },
] as const;

export function HomePage() {
  return (
    <>
      <section className="uni-public-hero">
        <div className="uni-public-hero-content">
          <div className="uni-public-hero-text">
            <p className="uni-public-eyebrow">Plataforma de operação de infraestrutura</p>
            <h1>Toda a operação de infraestrutura em um só lugar</h1>
            <p className="uni-public-lead">
              Monitoramento, alertas que viram incidentes com SLA, tarefas, projetos
              e a base de conhecimento da sua equipe — com auditoria de cada ação.
            </p>
            <div className="uni-public-actions">
              <Link to="/login" className="uni-public-button">
                <i className="pi pi-sign-in" />
                <span>Entrar</span>
              </Link>
              <Link to="/recursos" className="uni-public-button uni-public-button-ghost">
                <i className="pi pi-info-circle" />
                <span>Ver recursos</span>
              </Link>
            </div>
          </div>
          <div className="uni-public-hero-visual">
            <svg viewBox="0 0 200 200" className="uni-public-hero-icon" role="img" aria-label="Ilustração de server rack com LEDs de status">
              <defs>
                <linearGradient id="infraHeroGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" style={{stopColor:'#3b82f6',stopOpacity:0.2}} />
                  <stop offset="100%" style={{stopColor:'#2563eb',stopOpacity:0.1}} />
                </linearGradient>
              </defs>
              <circle cx="100" cy="100" r="80" fill="url(#infraHeroGrad)" />
              <g stroke="#3b82f6" strokeWidth="2" fill="none">
                <rect x="60" y="50" width="80" height="100" rx="8" />
                <rect x="70" y="65" width="60" height="12" rx="4" fill="#3b82f6" fillOpacity="0.2" />
                <rect x="70" y="85" width="60" height="12" rx="4" fill="#3b82f6" fillOpacity="0.2" />
                <rect x="70" y="105" width="60" height="12" rx="4" fill="#3b82f6" fillOpacity="0.2" />
                <rect x="70" y="125" width="60" height="12" rx="4" fill="#3b82f6" fillOpacity="0.2" />
                <circle cx="115" cy="71" r="3" fill="#22c55e" />
                <circle cx="115" cy="91" r="3" fill="#22c55e" />
                <circle cx="115" cy="111" r="3" fill="#eab308" />
                <circle cx="115" cy="131" r="3" fill="#22c55e" />
              </g>
            </svg>
          </div>
        </div>
      </section>

      <section aria-labelledby="pilares" className="uni-public-section">
        <h2 id="pilares">Do sinal bruto à decisão</h2>
        <div className="uni-public-grid">
          {PILLARS.map((pillar) => (
            <article key={pillar.title} className="uni-public-card">
              <span className="uni-public-card-icon">
                <i className={pillar.icon} />
              </span>
              <h3>{pillar.title}</h3>
              <p>{pillar.body}</p>
            </article>
          ))}
        </div>
      </section>

      <section aria-labelledby="ja-tem-instancia" className="uni-public-band">
        <div className="uni-public-band-content">
          <div className="uni-public-band-text">
            <h2 id="ja-tem-instancia">Já tem uma instância?</h2>
            <p>
              Entre com seu usuário e senha, ou com a conta Google liberada na sua
              organização.
            </p>
          </div>
          <Link to="/login" className="uni-public-button">
            <i className="pi pi-arrow-right" />
            <span>Acessar plataforma</span>
          </Link>
        </div>
      </section>
    </>
  );
}
