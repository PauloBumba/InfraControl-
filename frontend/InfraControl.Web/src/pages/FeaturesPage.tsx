import { Link } from "react-router-dom";

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

const MONITORING_FEATURES = [
  { icon: "pi pi-sitemap", name: "Topologia automática", desc: "Mapa da rede com conexões reais entre interfaces" },
  { icon: "pi pi-bolt", name: "Alertas inteligentes", desc: "Down com streak de 3 falhas, toner/drum baixo" },
  { icon: "pi pi-sync", name: "Autocorrelação", desc: "Supressão topológica de alertas downstream" },
  { icon: "pi pi-history", name: "Histórico completo", desc: "Resultados de checks com valores e duração" },
] as const;

const SECURITY_FEATURES = [
  { icon: "pi pi-shield", name: "MFA TOTP", desc: "Autenticador com códigos de recuperação" },
  { icon: "pi pi-key", name: "Sessões revogáveis", desc: "Teto absoluto de 12h e revogação por dispositivo" },
  { icon: "pi pi-users", name: "RBAC", desc: "Viewer, Operator, Admin com permissões granulares" },
  { icon: "pi pi-eye", name: "Auditoria", desc: "Toda ação registrada com ator, recurso e resultado" },
] as const;

export function FeaturesPage() {
  return (
    <>
      <section className="uni-public-hero">
        <p className="uni-public-eyebrow">Recursos completos</p>
        <h1>Descubra o poder da operação centralizada</h1>
        <p className="uni-public-lead">
          Uma ferramenta única para monitorar, detectar, operar e governar toda a sua infraestrutura — com segurança, rastreabilidade e controle total.
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

      <section aria-labelledby="monitoramento-detalhado" className="uni-public-section">
        <h2 id="monitoramento-detalhado">Monitoramento inteligente e automatizado</h2>
        <div className="uni-public-grid">
          {MONITORING_FEATURES.map((feature) => (
            <article key={feature.name} className="uni-public-card">
              <span className="uni-public-card-icon">
                <i className={feature.icon} />
              </span>
              <h3>{feature.name}</h3>
              <p>{feature.desc}</p>
            </article>
          ))}
        </div>
      </section>

      <section aria-labelledby="seguranca-detalhada" className="uni-public-section">
        <h2 id="seguranca-detalhada">Segurança de nível empresarial</h2>
        <div className="uni-public-grid">
          {SECURITY_FEATURES.map((feature) => (
            <article key={feature.name} className="uni-public-card">
              <span className="uni-public-card-icon">
                <i className={feature.icon} />
              </span>
              <h3>{feature.name}</h3>
              <p>{feature.desc}</p>
            </article>
          ))}
        </div>
      </section>

      <section aria-labelledby="arquitetura" className="uni-public-section">
        <h2 id="arquitetura">Arquitetura moderna e 100% on-premises</h2>
        <div className="uni-public-arch">
          <div className="uni-public-arch-layer primary">
            <div className="uni-public-arch-layer-header">Aplicação</div>
            <div className="uni-public-arch-layer-body">
              <span className="uni-public-arch-item">React + TypeScript</span>
              <span className="uni-public-arch-item">.NET 10</span>
              <span className="uni-public-arch-item">CQRS + MediatR</span>
            </div>
          </div>
          <div className="uni-public-arch-layer">
            <div className="uni-public-arch-layer-header">Dados</div>
            <div className="uni-public-arch-layer-body">
              <span className="uni-public-arch-item">PostgreSQL</span>
              <span className="uni-public-arch-item">Redis</span>
              <span className="uni-public-arch-item">RabbitMQ</span>
            </div>
          </div>
          <div className="uni-public-arch-layer">
            <div className="uni-public-arch-layer-header">Infraestrutura</div>
            <div className="uni-public-arch-layer-body">
              <span className="uni-public-arch-item">Docker Compose</span>
              <span className="uni-public-arch-item">Nginx</span>
              <span className="uni-public-arch-item">OpenTelemetry</span>
            </div>
          </div>
        </div>
      </section>

      <section aria-labelledby="falar" className="uni-public-band">
        <h2 id="falar">Pronto para transformar sua operação?</h2>
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
