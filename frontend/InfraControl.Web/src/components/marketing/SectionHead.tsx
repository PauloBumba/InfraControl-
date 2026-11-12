import type { ReactNode } from "react";

type SectionHeadProps = {
  /** rótulo curto acima do título (eyebrow) */
  eyebrow?: string;
  title: ReactNode;
  lead?: ReactNode;
  /** `left` quebra a monotonia de título centralizado em página inteira */
  align?: "center" | "left";
  /** id para `aria-labelledby` da seção */
  id?: string;
};

/**
 * F50-T009 — cabeçalho padronizado de seção (eyebrow + h2 + lead).
 *
 * Antes cada página montava o bloco à mão e a seção tinha só um `h2`
 * solto sem respiro definido — a razão vertical variava por página.
 */
export function SectionHead({ eyebrow, title, lead, align = "center", id }: SectionHeadProps) {
  return (
    <header
      className={`uni-public-section-head${align === "left" ? " uni-public-section-head--left" : ""}`}
    >
      {eyebrow ? <p className="uni-public-eyebrow">{eyebrow}</p> : null}
      <h2 id={id}>{title}</h2>
      {lead ? <p className="uni-public-section-lead">{lead}</p> : null}
    </header>
  );
}
