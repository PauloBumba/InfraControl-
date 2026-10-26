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
            <p className="uni-public-eyebrow">Plataforma on-premises de operação de TI</p>
            <h1>Centralize sua infraestrutura. Tome decisões antecipadas.</h1>
            <p className="uni-public-lead">
              Monitore dispositivos, antecipe falhas, gerencie incidentes e mantenha o conhecimento da sua equipe — tudo em uma plataforma self-hosted, sem depender de nuvens externas.
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
            <svg viewBox="0 0 280 280" className="uni-public-hero-icon" role="img" aria-label="Ilustração de infraestrutura com server rack, rede e dashboard">
              <defs>
                <linearGradient id="heroBgGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" style={{stopColor:'#60a5fa',stopOpacity:0.15}} />
                  <stop offset="100%" style={{stopColor:'#3b82f6',stopOpacity:0.05}} />
                </linearGradient>
                <linearGradient id="heroPrimaryGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" style={{stopColor:'#3b82f6',stopOpacity:1}} />
                  <stop offset="50%" style={{stopColor:'#2563eb',stopOpacity:1}} />
                  <stop offset="100%" style={{stopColor:'#1d4ed8',stopOpacity:1}} />
                </linearGradient>
                <linearGradient id="heroGreenGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" style={{stopColor:'#4ade80',stopOpacity:1}} />
                  <stop offset="100%" style={{stopColor:'#22c55e',stopOpacity:1}} />
                </linearGradient>
                <linearGradient id="heroAmberGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" style={{stopColor:'#fbbf24',stopOpacity:1}} />
                  <stop offset="100%" style={{stopColor:'#eab308',stopOpacity:1}} />
                </linearGradient>
              </defs>

              {/* Background glow */}
              <circle cx="140" cy="140" r="120" fill="url(#heroBgGrad)" />

              {/* Server rack principal */}
              <g>
                <rect x="85" y="50" width="110" height="140" rx="12" fill="none" stroke="url(#heroPrimaryGrad)" strokeWidth="3" />
                <line x1="85" y1="80" x2="195" y2="80" stroke="url(#heroPrimaryGrad)" strokeWidth="1.5" opacity="0.3" />
                <line x1="85" y1="110" x2="195" y2="110" stroke="url(#heroPrimaryGrad)" strokeWidth="1.5" opacity="0.3" />
                <line x1="85" y1="140" x2="195" y2="140" stroke="url(#heroPrimaryGrad)" strokeWidth="1.5" opacity="0.3" />
                <line x1="85" y1="170" x2="195" y2="170" stroke="url(#heroPrimaryGrad)" strokeWidth="1.5" opacity="0.3" />

                <rect x="95" y="60" width="80" height="14" rx="4" fill="url(#heroPrimaryGrad)" opacity="0.9" />
                <rect x="95" y="90" width="80" height="14" rx="4" fill="url(#heroPrimaryGrad)" opacity="0.8" />
                <rect x="95" y="120" width="80" height="14" rx="4" fill="url(#heroPrimaryGrad)" opacity="0.7" />
                <rect x="95" y="150" width="80" height="14" rx="4" fill="url(#heroPrimaryGrad)" opacity="0.6" />

                <circle cx="165" cy="67" r="2.5" fill="url(#heroGreenGrad)" />
                <circle cx="165" cy="97" r="2.5" fill="url(#heroGreenGrad)" />
                <circle cx="165" cy="127" r="2.5" fill="url(#heroAmberGrad)" />
                <circle cx="165" cy="157" r="2.5" fill="url(#heroGreenGrad)" />
              </g>

              {/* Network connections */}
              <g stroke="url(#heroPrimaryGrad)" strokeWidth="2" fill="none" opacity="0.5">
                <path d="M 50 90 Q 70 90, 85 90" />
                <circle cx="50" cy="90" r="4" fill="url(#heroPrimaryGrad)" />

                <path d="M 50 150 Q 70 150, 85 150" />
                <circle cx="50" cy="150" r="4" fill="url(#heroPrimaryGrad)" />

                <path d="M 195 120 Q 220 120, 230 120" />
                <circle cx="230" cy="120" r="4" fill="url(#heroPrimaryGrad)" />
              </g>

              {/* Small dashboard card */}
              <g transform="translate(180, 180)">
                <rect x="0" y="0" width="80" height="60" rx="8" fill="none" stroke="url(#heroPrimaryGrad)" strokeWidth="2" opacity="0.6" />
                <rect x="8" y="8" width="20" height="4" rx="2" fill="url(#heroPrimaryGrad)" opacity="0.5" />
                <rect x="8" y="18" width="40" height="3" rx="1.5" fill="url(#heroPrimaryGrad)" opacity="0.3" />
                <rect x="8" y="26" width="30" height="3" rx="1.5" fill="url(#heroPrimaryGrad)" opacity="0.3" />
                <circle cx="65" cy="45" r="12" fill="none" stroke="url(#heroGreenGrad)" strokeWidth="2" opacity="0.6" />
                <path d="M 65 38 L 65 45 L 70 48" stroke="url(#heroGreenGrad)" strokeWidth="2" fill="none" opacity="0.6" />
              </g>

              {/* Decorative dots */}
              <g fill="url(#heroPrimaryGrad)" opacity="0.3">
                <circle cx="105" cy="67" r="1.5" />
                <circle cx="115" cy="67" r="1.5" />
                <circle cx="125" cy="67" r="1.5" />
                <circle cx="105" cy="97" r="1.5" />
                <circle cx="115" cy="97" r="1.5" />
                <circle cx="125" cy="97" r="1.5" />
                <circle cx="105" cy="127" r="1.5" />
                <circle cx="115" cy="127" r="1.5" />
                <circle cx="125" cy="127" r="1.5" />
                <circle cx="105" cy="157" r="1.5" />
                <circle cx="115" cy="157" r="1.5" />
                <circle cx="125" cy="157" r="1.5" />
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
        <h2 id="pilares">Do dado bruto à ação inteligente</h2>
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
            <h2 id="ja-tem-instancia">Pronto para operar?</h2>
            <p>
              Acesse sua instância e comece a transformar dados em decisões.
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
