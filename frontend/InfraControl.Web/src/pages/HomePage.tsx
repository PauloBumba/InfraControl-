import { Link } from "react-router-dom";
import { CtaBand } from "@/components/marketing/CtaBand";
import { DashboardMockup } from "@/components/marketing/DashboardMockup";
import { FaqList } from "@/components/marketing/FaqAccordion";
import { PublicHero } from "@/components/marketing/PublicHero";
import { SectionHead } from "@/components/marketing/SectionHead";

const PILLARS = [
  {
    icon: "pi pi-eye",
    title: "Monitorar",
    body: "Zabbix para hosts, triggers e inventário; UniFi para WiFi, telemetria de APs, clientes da rede e insights de tráfego.",
  },
  {
    icon: "pi pi-bolt",
    title: "Detectar",
    body: "Alertas viram incidentes com SLA, responsável e histórico de recuperação — sem planilhas paralelas.",
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

const STATS = [
  { value: "24/7", label: "Monitoramento contínuo" },
  { value: "100%", label: "On-premises" },
  { value: "3", label: "Papéis de acesso" },
  { value: "∞", label: "Dispositivos" },
] as const;

const STEPS = [
  {
    number: "01",
    title: "Instale",
    body: "Docker Compose sobe PostgreSQL, Redis, RabbitMQ e a plataforma completa em minutos.",
  },
  {
    number: "02",
    title: "Descubra",
    body: "ICMP, SNMP, LLDP, CDP, ARP e DHCP encontram dispositivos e topologia automaticamente.",
  },
  {
    number: "03",
    title: "Monitore",
    body: "Ping, SNMP, HTTP e TCP com alertas que viram incidentes com SLA e recuperação.",
  },
  {
    number: "04",
    title: "Opere",
    body: "Tarefas, projetos, acesso remoto e base de conhecimento — tudo com auditoria.",
  },
] as const;

const INTEGRATIONS = [
  { icon: "pi pi-chart-line", name: "Zabbix" },
  { icon: "pi pi-wifi", name: "UniFi" },
  { icon: "pi pi-server", name: "SNMP" },
  { icon: "pi pi-shield", name: "Guacamole" },
  { icon: "pi pi-book", name: "Knowledge Base" },
  { icon: "pi pi-bell", name: "SignalR" },
] as const;

const ON_PREM_POINTS = [
  "Nada sai do seu datacenter: base, alertas, auditoria e arquivos ficam na sua rede.",
  "Docker Compose sobe API, workers, web, PostgreSQL, Redis e RabbitMQ em minutos.",
  "Código aberto: audite o que roda, versione a configuração e atualize no seu ritmo.",
] as const;

const FAQ = [
  {
    question: "O InfraControl é realmente on-premises?",
    answer: "Sim. Toda a plataforma roda no seu próprio servidor ou datacenter via Docker Compose. Sem dependência de nuvem externa.",
  },
  {
    question: "Quais dispositivos são suportados?",
    answer: "Qualquer dispositivo com IP: switches, roteadores, servidores, impressoras, câmeras, APs, telefones IP e UPS. Monitoramento via ICMP, SNMP, HTTP e TCP.",
  },
  {
    question: "Como funciona o controle de acesso?",
    answer: "Três papéis: Viewer (somente leitura), Operator (operação) e Admin (administração). MFA TOTP, sessões revogáveis e auditoria completa.",
  },
  {
    question: "Posso testar antes de instalar?",
    answer: "Sim. O projeto é open source. Clone o repositório, execute docker compose up e explore todas as funcionalidades em ambiente local.",
  },
] as const;

export function HomePage() {
  return (
    <div className="uni-public-page--home">
      <PublicHero
        eyebrow="Plataforma on-premises de operação de TI"
        title="Centralize sua infraestrutura. Tome decisões antecipadas."
        lead="Monitore dispositivos, antecipe falhas, gerencie incidentes e mantenha o conhecimento da sua equipe — tudo em uma plataforma self-hosted, sem depender de nuvens externas."
        actions={
          <>
            <Link to="/login" className="uni-public-button">
              <i className="pi pi-sign-in" aria-hidden="true" />
              <span>Entrar</span>
            </Link>
            <Link to="/recursos" className="uni-public-button uni-public-button-ghost">
              <i className="pi pi-info-circle" aria-hidden="true" />
              <span>Ver recursos</span>
            </Link>
          </>
        }
        visual={<DashboardMockup />}
        foot={
          <div className="uni-public-stats">
            {STATS.map((stat) => (
              <div key={stat.label} className="uni-public-stat">
                <div className="uni-public-stat-value">{stat.value}</div>
                <div className="uni-public-stat-label">{stat.label}</div>
              </div>
            ))}
          </div>
        }
      />

      <section className="uni-public-section" aria-labelledby="pilares">
        <SectionHead
          id="pilares"
          eyebrow="Quatro frentes"
          title="Do dado bruto à ação inteligente"
          lead="Cada pilar aponta para uma capacidade que existe no produto — nada de promessa genérica."
        />
        <div className="uni-public-grid">
          {PILLARS.map((pillar) => (
            <article key={pillar.title} className="uni-public-card">
              <span className="uni-public-card-icon">
                <i className={pillar.icon} aria-hidden="true" />
              </span>
              <h3>{pillar.title}</h3>
              <p>{pillar.body}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="uni-public-section uni-public-section--alt" aria-labelledby="on-prem">
        <div className="uni-public-split">
          <div>
            <SectionHead
              align="left"
              id="on-prem"
              eyebrow="On-premises de verdade"
              title="A sua infraestrutura não sai do seu datacenter"
              lead="Instale uma vez, na sua rede, com as ferramentas que a sua equipe já opera."
            />
            <ul className="uni-public-list">
              {ON_PREM_POINTS.map((point) => (
                <li key={point}>{point}</li>
              ))}
            </ul>
          </div>
          <div className="uni-public-terminal">
            <div className="uni-public-terminal-bar">bash — infracontrol</div>
            <pre>
              <span className="cmd">$ git clone https://github.com/PauloBumba/InfraControl-.git</span>
              {"\n"}
              <span className="cmd">$ cd InfraControl- &amp;&amp; docker compose up -d</span>
              {"\n"}
              <span className="ok">✓ api · workers · web · postgres · redis · rabbitmq</span>
            </pre>
          </div>
        </div>
      </section>

      <section className="uni-public-section" aria-labelledby="como-funciona">
        <SectionHead
          id="como-funciona"
          eyebrow="Como funciona"
          title="Do primeiro ping ao relatório executivo"
          lead="Quatro passos entre subir a plataforma e operar com histórico e SLA."
        />
        <div className="uni-public-steps">
          {STEPS.map((step) => (
            <div key={step.number} className="uni-public-step">
              <div className="uni-public-step-number">{step.number}</div>
              <h3>{step.title}</h3>
              <p>{step.body}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="uni-public-section uni-public-section--alt" aria-labelledby="integracoes">
        <SectionHead
          id="integracoes"
          eyebrow="Integrações"
          title="Conecta com o que você já usa"
          lead="Fontes de dado, canais de alerta e ferramentas de acesso que a plataforma consome de verdade."
        />
        <div className="uni-public-integrations">
          {INTEGRATIONS.map((integration) => (
            <div key={integration.name} className="uni-public-integration">
              <span className="uni-public-integration-icon">
                <i className={integration.icon} aria-hidden="true" />
              </span>
              <span className="uni-public-integration-name">{integration.name}</span>
            </div>
          ))}
        </div>
      </section>

      <section className="uni-public-section" aria-labelledby="faq">
        <SectionHead
          id="faq"
          eyebrow="Perguntas frequentes"
          title="O que perguntam antes de implantar"
        />
        <FaqList items={FAQ} />
      </section>

      <CtaBand
        title="Pronto para operar?"
        text="Acesse sua instância e comece a transformar dados em decisões."
        actions={
          <>
            <Link to="/login" className="uni-public-button">
              <i className="pi pi-arrow-right" aria-hidden="true" />
              <span>Acessar plataforma</span>
            </Link>
            <Link to="/recursos" className="uni-public-button uni-public-button-ghost">
              <span>Ver recursos</span>
            </Link>
          </>
        }
      />
    </div>
  );
}
