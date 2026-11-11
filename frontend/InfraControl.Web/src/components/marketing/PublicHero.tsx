import type { ReactNode } from "react";

type PublicHeroProps = {
  eyebrow: string;
  title: ReactNode;
  lead: string;
  actions: ReactNode;
  /** mockup do produto — sem ele o hero sai centralizado (Recursos/Contato) */
  visual?: ReactNode;
  /** faixa abaixo do conteúdo (ex.: métricas da Home) */
  foot?: ReactNode;
};

/**
 * F50-T009 — hero único das páginas públicas (fundo escuro full-bleed).
 *
 * Antes havia 2 modelos convivendo: o da Home (`.uni-public-hero-content`
 * com grid de 2 colunas) e o "pelado" de Recursos/Contato (eyebrow, h1 e
 * lead filhos diretos do `section`) — o CSS da base precisava adivinhar as
 * duas formas e cada page CSS repetia padding/gradiente próprio.
 */
export function PublicHero({ eyebrow, title, lead, actions, visual, foot }: PublicHeroProps) {
  const variant = visual ? "uni-public-hero--split" : "uni-public-hero--center";

  return (
    <section className={`uni-public-hero ${variant}`}>
      <div className="uni-public-hero-content">
        <div className="uni-public-hero-text">
          <p className="uni-public-eyebrow">{eyebrow}</p>
          <h1>{title}</h1>
          <p className="uni-public-lead">{lead}</p>
          <div className="uni-public-actions">{actions}</div>
        </div>
        {visual ? <div className="uni-public-hero-visual">{visual}</div> : null}
      </div>
      {foot ? <div className="uni-public-hero-foot">{foot}</div> : null}
    </section>
  );
}
