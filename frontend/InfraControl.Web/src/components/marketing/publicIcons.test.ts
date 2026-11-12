import iconsCss from "../../../node_modules/primeicons/primeicons.css?raw";
import contactSource from "../../pages/ContactPage.tsx?raw";
import featuresSource from "../../pages/FeaturesPage.tsx?raw";
import homeSource from "../../pages/HomePage.tsx?raw";
import ctaBandSource from "./CtaBand.tsx?raw";
import dashboardMockupSource from "./DashboardMockup.tsx?raw";
import faqAccordionSource from "./FaqAccordion.tsx?raw";
import publicHeroSource from "./PublicHero.tsx?raw";
import publicLayoutSource from "./PublicLayout.tsx?raw";
import sectionHeadSource from "./SectionHead.tsx?raw";
import { describe, expect, it } from "vitest";

/**
 * F50-T009 — nenhum ícone inexistente no PrimeIcons no material público.
 *
 * O bug que motivou: `pi pi-life-ring` no card de suporte da Contato não
 * existe no `primeicons.css` — o `<i>` nasce vazio e o card aparece com um
 * quadrado sem glifo. Nenhum teste de texto pega isso, porque o ícone não
 * tem texto: só quebra na tela.
 *
 * Escopo: as 3 páginas públicas + os componentes de marketing. As demais
 * telas do app têm 3 ícones inválidos pré-existentes (`pi-plug`,
 * `pi-sticky-note`, `pi-terminal`) e são outra task — este teste tranca o
 * material de vendas, que é o que o usuário vê primeiro.
 *
 * Leitura via `?raw` (mesmo padrão do publicPagesScope.test.ts): o pacote
 * não tem `@types/node` e o runner não expõe `import.meta.url` como `file:`.
 */
const SOURCES = [
  { file: "HomePage.tsx", code: homeSource },
  { file: "FeaturesPage.tsx", code: featuresSource },
  { file: "ContactPage.tsx", code: contactSource },
  { file: "PublicLayout.tsx", code: publicLayoutSource },
  { file: "PublicHero.tsx", code: publicHeroSource },
  { file: "SectionHead.tsx", code: sectionHeadSource },
  { file: "CtaBand.tsx", code: ctaBandSource },
  { file: "FaqAccordion.tsx", code: faqAccordionSource },
  { file: "DashboardMockup.tsx", code: dashboardMockupSource },
] as const;

/** tokens `pi-*` do PrimeIcons (classe `pi pi-x` ou `pi-x` em template) */
const ICON_TOKEN = /\bpi-[a-z0-9-]+/g;

describe("ícones do material público (F50-T009)", () => {
  it("usa apenas glifos que existem no primeicons.css", () => {
    const missing: string[] = [];

    for (const { file, code } of SOURCES) {
      for (const [token] of code.matchAll(ICON_TOKEN)) {
        const exists = new RegExp(`\\.${token}\\b`).test(iconsCss);
        if (!exists) {
          missing.push(`${token} — ${file}`);
        }
      }
    }

    expect(missing, "ícone inexistente no PrimeIcons (renderiza quadrado vazio)").toEqual([]);
  });
});
