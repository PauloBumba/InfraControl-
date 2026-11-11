import { useId, useState } from "react";

export type FaqEntry = {
  readonly question: string;
  readonly answer: string;
};

/**
 * F50-T009 — FAQ extraído (estava copiado em `HomePage` e `ContactPage`,
 * com os 2 `FaqItem` idênticos decidindo sozinhos a classe `open`).
 *
 * Resposta no DOM sempre (`hidden`) com `aria-controls` apontando para ela:
 * o botão expõe `aria-expanded` e o leitor de tela encontra a resposta
 * mesmo fechada.
 */
export function FaqItem({ question, answer, defaultOpen = false }: FaqEntry & { defaultOpen?: boolean }) {
  const [open, setOpen] = useState(defaultOpen);
  const answerId = useId();

  return (
    <div className={`uni-public-faq-item${open ? " open" : ""}`}>
      <button
        type="button"
        className="uni-public-faq-question"
        aria-expanded={open}
        aria-controls={answerId}
        onClick={() => setOpen((current) => !current)}
      >
        <span>{question}</span>
        <i className="pi pi-chevron-down" aria-hidden="true" />
      </button>
      <div id={answerId} className="uni-public-faq-answer" hidden={!open}>
        <p>{answer}</p>
      </div>
    </div>
  );
}

export function FaqList({ items }: { items: readonly FaqEntry[] }) {
  return (
    <div className="uni-public-faq">
      {items.map((item) => (
        <FaqItem key={item.question} {...item} />
      ))}
    </div>
  );
}
