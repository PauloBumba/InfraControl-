import { Link } from "react-router-dom";
import { CtaBand } from "@/components/marketing/CtaBand";
import { PublicHero } from "@/components/marketing/PublicHero";
import { SectionHead } from "@/components/marketing/SectionHead";

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

const ARCH_LAYERS = [
  {
    title: "Aplicação",
    primary: true,
    items: ["React + TypeScript", ".NET 10", "CQRS + MediatR"],
  },
  {
    title: "Dados",
    primary: false,
    items: ["PostgreSQL", "Redis", "RabbitMQ"],
  },
  {
    title: "Infraestrutura",
    primary: false,
    items: ["Docker Compose", "Nginx", "OpenTelemetry"],
  },
] as const;

export function FeaturesPage() {
  return (
    <div className="uni-public-page--features">
      <PublicHero
        eyebrow="Recursos completos"
        title="Descubra o poder da operação centralizada"
        lead="Uma ferramenta única para monitorar, detectar, operar e governar toda a sua infraestrutura — com segurança, rastreabilidade e controle total."
        actions={
          <>
            <Link to="/login" className="uni-public-button">
              <i className="pi pi-sign-in" aria-hidden="true" />
              <span>Entrar</span>
            </Link>
            <Link to="/contato" className="uni-public-button uni-public-button-ghost">
              <span>Falar com a gente</span>
            </Link>
          </>
        }
      />

      <section className="uni-public-section" aria-labelledby="grupos">
        <SectionHead
          id="grupos"
          eyebrow="Como o produto se organiza"
          title="Quatro frentes, um só painel"
          lead="Os grupos espelham a estrutura real da plataforma — cada item listado existe no código."
        />
        <div className="uni-public-grid uni-public-groups">
          {GROUPS.map((group) => (
            <article key={group.title} className="uni-public-card">
              <span className="uni-public-card-icon">
                <i className={group.icon} aria-hidden="true" />
              </span>
              <h3>{group.title}</h3>
              <ul className="uni-public-list">
                {group.items.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </article>
          ))}
        </div>
      </section>

      <section className="uni-public-section uni-public-section--alt" aria-labelledby="monitoramento-detalhado">
        <SectionHead
          id="monitoramento-detalhado"
          eyebrow="Monitoramento"
          title="Monitoramento inteligente e automatizado"
          lead="Descoberta de rede, correlação e histórico em um ciclo só."
        />
        <div className="uni-public-grid">
          {MONITORING_FEATURES.map((feature) => (
            <article key={feature.name} className="uni-public-card">
              <span className="uni-public-card-icon">
                <i className={feature.icon} aria-hidden="true" />
              </span>
              <h3>{feature.name}</h3>
              <p>{feature.desc}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="uni-public-section" aria-labelledby="seguranca-detalhada">
        <SectionHead
          id="seguranca-detalhada"
          eyebrow="Segurança"
          title="Segurança de nível empresarial"
          lead="Identidade, sessão e trilha de auditoria tratadas como cidadãos de primeira classe."
        />
        <div className="uni-public-grid">
          {SECURITY_FEATURES.map((feature) => (
            <article key={feature.name} className="uni-public-card">
              <span className="uni-public-card-icon">
                <i className={feature.icon} aria-hidden="true" />
              </span>
              <h3>{feature.name}</h3>
              <p>{feature.desc}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="uni-public-section uni-public-section--alt" aria-labelledby="arquitetura">
        <SectionHead
          id="arquitetura"
          eyebrow="Arquitetura"
          title="Arquitetura moderna e 100% on-premises"
          lead="Camadas separadas, mensageria e telemetria — sem serviço gerenciado obrigatório."
        />
        <div className="uni-public-arch">
          {ARCH_LAYERS.map((layer) => (
            <div
              key={layer.title}
              className={`uni-public-arch-layer${layer.primary ? " primary" : ""}`}
            >
              <div className="uni-public-arch-layer-header">{layer.title}</div>
              <div className="uni-public-arch-layer-body">
                {layer.items.map((item) => (
                  <span key={item} className="uni-public-arch-item">
                    {item}
                  </span>
                ))}
              </div>
            </div>
          ))}
        </div>
      </section>

      <CtaBand
        title="Pronto para transformar sua operação?"
        actions={
          <>
            <Link to="/contato" className="uni-public-button">
              <span>Falar com a gente</span>
            </Link>
            <Link to="/login" className="uni-public-button uni-public-button-ghost">
              <span>Entrar</span>
            </Link>
          </>
        }
      />
    </div>
  );
}
