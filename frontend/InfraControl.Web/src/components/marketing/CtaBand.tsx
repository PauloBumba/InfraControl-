import { useId, type ReactNode } from "react";

type CtaBandProps = {
  title: ReactNode;
  text?: ReactNode;
  actions: ReactNode;
};

/**
 * F50-T009 — banda de CTA única.
 *
 * As 3 páginas montavam a `.uni-public-band` com markup divergente
 * (Home: `band-content > band-text` + botão; Recursos: `h2 + actions` com
 * botão sem gradiente; Contato: `h2` + botão e padding/gradiente próprios
 * no `contact.css`) — o visual mudava de página para página.
 */
export function CtaBand({ title, text, actions }: CtaBandProps) {
  const titleId = useId();

  return (
    <section className="uni-public-band" aria-labelledby={titleId}>
      <h2 id={titleId}>{title}</h2>
      {text ? <p className="uni-public-band-text">{text}</p> : null}
      <div className="uni-public-actions">{actions}</div>
    </section>
  );
}
