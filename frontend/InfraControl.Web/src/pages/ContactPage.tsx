import { Link } from "react-router-dom";

/**
 * F26-T002 — página de contato.
 *
 * Decisão do usuário (2026-10-11): **canais honestos, sem formulário** —
 * não existe endpoint de contato no backend, então a página não mostra um
 * form que finge enviar mensagem.
 *
 * O endereço de suporte NÃO é inventado: vem de `VITE_SUPPORT_EMAIL`
 * (mesmo padrão opcional do `VITE_GOOGLE_CLIENT_ID`, documentado no
 * `.env.example`). Sem valor no build, a página diz que a instância é
 * self-hosted e manda falar com o administrador — em vez de exibir um
 * e-mail fictício que ninguém lê.
 *
 * O env é lido DENTRO da função (e não numa constante de módulo) porque a
 * avaliação de módulo acontece uma vez, no import — antes de qualquer
 * teste conseguir trocar o valor.
 */
export function getSupportEmail(): string {
  return (import.meta.env.VITE_SUPPORT_EMAIL ?? "").trim();
}

export function ContactPage() {
  const supportEmail = getSupportEmail();
  return (
    <>
      <section className="uni-public-hero">
        <p className="uni-public-eyebrow">Contato</p>
        <h1>Fale com a gente</h1>
        <p className="uni-public-lead">
          O InfraControl é self-hosted: quem o implanta é quem opera. Escolha
          o canal certo para o seu caso.
        </p>
      </section>

      <section className="uni-public-section">
        <div className="uni-public-grid">
          <article className="uni-public-card">
            <span className="uni-public-card-icon">
              <i className="pi pi-life" />
            </span>
            <h2>Suporte à instância</h2>
            {supportEmail ? (
              <p>
                Dúvidas sobre a instância que você usa:{" "}
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
            <h2>Acesso e senha</h2>
            <p>
              Não consegue entrar, esqueceu a senha ou quer trocá-la? O login
              e a recuperação de senha ficam na própria tela de entrada.
            </p>
          </article>

          <article className="uni-public-card">
            <span className="uni-public-card-icon">
              <i className="pi pi-book" />
            </span>
            <h2>O que a plataforma faz</h2>
            <p>
              Antes de perguntar, veja a lista completa de recursos —
              monitoramento, operação, governança e acesso.
            </p>
            <Link to="/recursos" className="uni-public-card-link">
              Ver recursos
            </Link>
          </article>
        </div>
      </section>

      <section aria-labelledby="entrar" className="uni-public-band">
        <h2 id="entrar">Já tem um usuário?</h2>
        <Link to="/login" className="uni-public-button">
          Entrar
        </Link>
      </section>
    </>
  );
}
