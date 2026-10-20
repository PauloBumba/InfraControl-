import { useState } from "react";
import { Link } from "react-router-dom";

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

function FaqItem({ question, answer }: { question: string; answer: string }) {
  const [open, setOpen] = useState(false);
  return (
    <div className={`uni-public-faq-item ${open ? "open" : ""}`}>
      <button className="uni-public-faq-question" onClick={() => setOpen(!open)} aria-expanded={open}>
        <span>{question}</span>
        <i className="pi pi-angle-down" />
      </button>
      {open && <div className="uni-public-faq-answer">{answer}</div>}
    </div>
  );
}

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

      <section className="uni-public-section" aria-label="Estatísticas">
        <div className="uni-public-stats">
          {STATS.map((stat) => (
            <div key={stat.label} className="uni-public-stat">
              <div className="uni-public-stat-value">{stat.value}</div>
              <div className="uni-public-stat-label">{stat.label}</div>
            </div>
          ))}
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

      <section aria-labelledby="como-funciona" className="uni-public-section">
        <h2 id="como-funciona">Como funciona</h2>
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

      <section aria-labelledby="integracoes" className="uni-public-section">
        <h2 id="integracoes">Integrações</h2>
        <div className="uni-public-integrations">
          {INTEGRATIONS.map((integration) => (
            <div key={integration.name} className="uni-public-integration">
              <div className="uni-public-integration-icon">
                <i className={integration.icon} />
              </div>
              <span className="uni-public-integration-name">{integration.name}</span>
            </div>
          ))}
        </div>
      </section>

      <section aria-labelledby="faq" className="uni-public-section">
        <h2 id="faq">Perguntas frequentes</h2>
        <div className="uni-public-faq">
          {FAQ.map((item) => (
            <FaqItem key={item.question} question={item.question} answer={item.answer} />
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
