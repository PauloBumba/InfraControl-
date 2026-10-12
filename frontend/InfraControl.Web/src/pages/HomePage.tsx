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
        <p className="uni-public-eyebrow">Plataforma de operação de infraestrutura</p>
        <h1>Toda a operação de infraestrutura em um só lugar</h1>
        <p className="uni-public-lead">
          Monitoramento, alertas que viram incidentes com SLA, tarefas, projetos
          e a base de conhecimento da sua equipe — com auditoria de cada ação.
        </p>
        <div className="uni-public-actions">
          <Link to="/login" className="uni-public-button">
            Entrar
          </Link>
          <Link to="/recursos" className="uni-public-button uni-public-button-ghost">
            Ver recursos
          </Link>
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
        <h2 id="ja-tem-instancia">Já tem uma instância?</h2>
        <p>
          Entre com seu usuário e senha, ou com a conta Google liberada na sua
          organização.
        </p>
        <Link to="/login" className="uni-public-button">
          Ir para o login
        </Link>
      </section>
    </>
  );
}
