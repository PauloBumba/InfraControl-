import { Link } from "react-router-dom";

/**
 * F26-T002 — página de recursos.
 *
 * Sem design doc no repositório, o conteúdo foi DERIVADO DO PRODUTO REAL
 * (decisão do usuário 2026-10-11): cada item listado existe no código. Os
 * grupos espelham como o produto já se organiza no menu — monitoramento,
 * operação e governança — mais o bloco de acesso, que é onde ficam MFA,
 * política de senha e sessões.
 *
 * Regra desta página: não prometer o que não existe. Se um recurso não está
 * no produto, ele não entra aqui.
 */
const GROUPS = [
  {
    icon: "pi pi-chart-line",
    title: "Monitoramento",
    items: [
      "Zabbix: hosts monitorados, saúde da integração e sincronização de inventário",
      "UniFi: dashboard, página ao vivo, WiFi com SSIDs e telemetria dos APs",
      "Topologia da rede e inventário de dispositivos",
      "Clientes da rede: ARP, DHCP, tabela MAC e vizinhos",
      "Insights de tráfego por cliente e DPI por aplicação",
    ],
  },
  {
    icon: "pi pi-gauge",
    title: "Operação",
    items: [
      "Alertas e incidentes com SLA e histórico de recuperação",
      "Tarefas com responsável, prioridade e prazo",
      "Acesso remoto a dispositivos",
      "Notificações em tempo real (SignalR) com preferências por canal",
    ],
  },
  {
    icon: "pi pi-verified",
    title: "Governança",
    items: [
      "Projetos com PDI, fluxo de aprovação e histórico",
      "Base de conhecimento com categorias e publicação revisada",
      "Auditoria completa das ações",
      "Usuários, convites e sessões por dispositivo",
    ],
  },
  {
    icon: "pi pi-lock",
    title: "Acesso",
    items: [
      "Login por senha com MFA e conta Google",
      "Política de senha e revogação de sessões",
      "Permissões por papel: Viewer, Operator e Admin",
    ],
  },
] as const;

export function FeaturesPage() {
  return (
    <>
      <section className="uni-public-hero">
        <p className="uni-public-eyebrow">Recursos</p>
        <h1>O que o InfraControl faz</h1>
        <p className="uni-public-lead">
          Do monitoramento ao conhecimento da equipe, tudo em uma plataforma —
          com as mesmas permissões e a mesma auditoria do início ao fim.
        </p>
      </section>

      <section className="uni-public-section">
        <div className="uni-public-grid">
          {GROUPS.map((group) => (
            <article key={group.title} className="uni-public-card">
              <span className="uni-public-card-icon">
                <i className={group.icon} />
              </span>
              <h2>{group.title}</h2>
              <ul className="uni-public-list">
                {group.items.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </article>
          ))}
        </div>
      </section>

      <section aria-labelledby="falar" className="uni-public-band">
        <h2 id="falar">Quer saber como isso se aplica à sua operação?</h2>
        <div className="uni-public-actions">
          <Link to="/contato" className="uni-public-button">
            Falar com a gente
          </Link>
          <Link to="/login" className="uni-public-button uni-public-button-ghost">
            Entrar
          </Link>
        </div>
      </section>
    </>
  );
}
