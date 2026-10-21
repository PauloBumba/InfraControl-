import { Link } from "react-router-dom";

export function getSupportEmail(): string {
  return (import.meta.env.VITE_SUPPORT_EMAIL ?? "").trim();
}

const FAQ = [
  {
    question: "Como instalo o InfraControl?",
    answer: "Use Docker Compose: clone o repositório, configure o .env e execute docker compose up -d. A plataforma sobe completa com PostgreSQL, Redis e RabbitMQ.",
  },
  {
    question: "Posso testar sem instalar?",
    answer: "Sim. O código é open source. Clone o repositório, execute docker compose up e explore todas as funcionalidades em ambiente local.",
  },
  {
    question: "Quais são os requisitos mínimos?",
    answer: "Um servidor com Docker e Docker Compose instalados. Recomendamos 4GB de RAM e 20GB de disco para ambiente de desenvolvimento.",
  },
  {
    question: "Como funciona o suporte?",
    answer: "Sendo self-hosted, o suporte é fornecido pelo administrador da sua instância. Para dúvidas sobre o projeto, use a página de contato.",
  },
] as const;

export function ContactPage() {
  const supportEmail = getSupportEmail();
  return (
    <>
      <section className="uni-public-hero">
        <p className="uni-public-eyebrow">Suporte e contato</p>
        <h1>Encontre o caminho certo para sua dúvida</h1>
        <p className="uni-public-lead">
          O InfraControl é self-hosted: sua equipe controla a implantação e a operação. Escolha o canal adequado para o seu caso.
        </p>
      </section>

      <section className="uni-public-section">
        <div className="uni-public-grid">
          <article className="uni-public-card">
            <span className="uni-public-card-icon">
              <i className="pi pi-life-ring" />
            </span>
            <h2>Suporte da sua instância</h2>
            {supportEmail ? (
              <p>
                Dúvidas sobre o uso da sua instância:{" "}
                <a href={`mailto:${supportEmail}`}>{supportEmail}</a>
              </p>
            ) : (
              <p>
                Esta instância é <strong>self-hosted</strong> e não publica um
                canal de suporte. Procure o administrador da sua organização —
                é ele quem implanta e opera o InfraControl.
              </p>
            )}
          </article>

          <article className="uni-public-card">
            <span className="uni-public-card-icon">
              <i className="pi pi-key" />
            </span>
            <h2>Problemas de acesso</h2>
            <p>
              Não consegue entrar, esqueceu a senha ou precisa trocá-la? O login
              e a recuperação de senha ficam na própria tela de entrada.
            </p>
          </article>

          <article className="uni-public-card">
            <span className="uni-public-card-icon">
              <i className="pi pi-book" />
            </span>
            <h2>Funcionalidades da plataforma</h2>
            <p>
              Antes de perguntar, explore a lista completa de recursos —
              monitoramento, operação, governança e segurança.
            </p>
            <Link to="/recursos" className="uni-public-card-link">
              Ver recursos
            </Link>
          </article>
        </div>
      </section>

      <section aria-labelledby="faq-contato" className="uni-public-section">
        <h2 id="faq-contato">Dúvidas comuns</h2>
        <div className="uni-public-faq">
          {FAQ.map((item) => (
            <div key={item.question} className="uni-public-faq-item">
              <div className="uni-public-faq-question">
                <span>{item.question}</span>
              </div>
              <div className="uni-public-faq-answer">{item.answer}</div>
            </div>
          ))}
        </div>
      </section>

      <section aria-labelledby="entrar" className="uni-public-band">
        <h2 id="entrar">Já tem acesso à sua instância?</h2>
        <Link to="/login" className="uni-public-button">
          Entrar
        </Link>
      </section>
    </>
  );
}
