import { Link } from "react-router-dom";
import { CtaBand } from "@/components/marketing/CtaBand";
import { FaqList } from "@/components/marketing/FaqAccordion";
import { PublicHero } from "@/components/marketing/PublicHero";
import { SectionHead } from "@/components/marketing/SectionHead";
import { getSupportEmail } from "@/services/support";

/** Contrato antigo da página: `getSupportEmail` continua exportado daqui. */
export { getSupportEmail };

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
    <div className="uni-public-page--contact">
      <PublicHero
        eyebrow="Suporte e contato"
        title="Encontre o caminho certo para sua dúvida"
        lead="O InfraControl é self-hosted: sua equipe controla a implantação e a operação. Escolha o canal adequado para o seu caso."
        actions={
          <>
            <Link to="/login" className="uni-public-button">
              <i className="pi pi-sign-in" aria-hidden="true" />
              <span>Entrar</span>
            </Link>
            <Link to="/recursos" className="uni-public-button uni-public-button-ghost">
              <span>Ver recursos</span>
            </Link>
          </>
        }
      />

      <section className="uni-public-section" aria-labelledby="canais">
        <SectionHead
          id="canais"
          eyebrow="Canais honestos"
          title="Sem formulário que finge enviar"
          lead="Não existe endpoint de contato no backend — por isso a página mostra canais reais em vez de um form que não entregaria nada."
        />
        <div className="uni-public-grid uni-public-channels">
          <article className="uni-public-card">
            <span className="uni-public-card-icon">
              <i className="pi pi-headphones" aria-hidden="true" />
            </span>
            <h3>Suporte da sua instância</h3>
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
              <i className="pi pi-key" aria-hidden="true" />
            </span>
            <h3>Problemas de acesso</h3>
            <p>
              Não consegue entrar, esqueceu a senha ou precisa trocá-la? O login
              e a recuperação de senha ficam na própria tela de entrada.
            </p>
            <Link to="/login" className="uni-public-card-link">
              Ir para a tela de entrada
            </Link>
          </article>

          <article className="uni-public-card">
            <span className="uni-public-card-icon">
              <i className="pi pi-book" aria-hidden="true" />
            </span>
            <h3>Funcionalidades da plataforma</h3>
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

      <section className="uni-public-section uni-public-section--alt" aria-labelledby="faq-contato">
        <SectionHead
          id="faq-contato"
          eyebrow="Perguntas frequentes"
          title="Dúvidas comuns"
          lead="Instalação, requisitos e suporte — respondidos sem enrolação."
        />
        <FaqList items={FAQ} />
      </section>

      <CtaBand
        title="Já tem acesso à sua instância?"
        actions={
          <>
            <Link to="/login" className="uni-public-button">
              <i className="pi pi-sign-in" aria-hidden="true" />
              <span>Entrar</span>
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
