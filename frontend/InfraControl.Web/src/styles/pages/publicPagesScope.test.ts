import { describe, expect, it } from "vitest";
import contactCss from "./contact.css?raw";
import featuresCss from "./features.css?raw";
import homeCss from "./home.css?raw";

/**
 * F50-T006 (F6) — contrato de escopo das folhas das páginas públicas.
 *
 * O bug: `styles.css` importa home → features → contact GLOBALMENTE e os
 * três arquivos redefinem os mesos seletores (`.uni-public-hero`,
 * `::before/::after`, `.uni-public-hero h1`, `.uni-public-lead`,
 * `.uni-public-band*`) — o último (contact.css) vencia em TODAS as páginas.
 * Concreto: a home perdia `max-width: 580px`/`margin-bottom: 32px` do lead
 * (recebia 640px/margin auto do contato) e a animação do h1 (contact não
 * tem); a features perdia o lead 680px e a banda própria.
 *
 * A correção: cada página tem uma raiz `.uni-public-page--{página}` e TODA
 * regra dos seus arquivos é declarada sob esse escopo — o que é compartilhado
 * (band, faq, actions) vive no `public-base.css`, que continua global. Este
 * teste tranca que ninguém desescopar um arquivo novo.
 *
 * `?raw` exige `css: true` no vitest.config.ts (o default stuba CSS).
 */
const PAGES = [
  { file: "home.css", css: homeCss, scope: ".uni-public-page--home" },
  { file: "features.css", css: featuresCss, scope: ".uni-public-page--features" },
  { file: "contact.css", css: contactCss, scope: ".uni-public-page--contact" },
] as const;

function stripComments(css: string): string {
  return css.replace(/\/\*[\s\S]*?\*\//g, "");
}

/**
 * Seletores de todas as regras de um trecho CSS — desce em @media/@supports,
 * ignora @keyframes (nome, não seletor) e separa seletores agrupados por
 * vírgula. Sem aninhamento nativo nos arquivos da casa (verificado).
 */
function ruleSelectors(css: string): string[] {
  const source = stripComments(css);
  const selectors: string[] = [];
  let index = 0;

  while (index < source.length) {
    const open = source.indexOf("{", index);
    if (open === -1) break;
    const head = source.slice(index, open).trim();

    let depth = 1;
    let cursor = open + 1;
    while (cursor < source.length && depth > 0) {
      if (source[cursor] === "{") depth++;
      else if (source[cursor] === "}") depth--;
      cursor++;
    }
    const body = source.slice(open + 1, cursor - 1);

    if (head.startsWith("@keyframes")) {
      // nome da animação — não é seletor
    } else if (head.startsWith("@media") || head.startsWith("@supports")) {
      selectors.push(...ruleSelectors(body));
    } else {
      for (const selector of head.split(",")) {
        const trimmed = selector.trim().replace(/\s+/g, " ");
        if (trimmed) selectors.push(trimmed);
      }
    }
    index = cursor;
  }

  return selectors;
}

describe.each(PAGES)("escopo de $file (F50-T006)", ({ css, scope }) => {
  it(`declara toda regra sob ${scope}`, () => {
    const selectors = ruleSelectors(css);
    expect(selectors.length).toBeGreaterThan(10);

    const unscoped = selectors.filter(
      (selector) => selector !== scope && !selector.startsWith(`${scope} `),
    );
    expect(unscoped, "regras fora do escopo da página (vazamento global)").toEqual([]);
  });
});
