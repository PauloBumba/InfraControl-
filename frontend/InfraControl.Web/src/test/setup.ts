import "@testing-library/jest-dom/vitest";
import { cleanup, configure } from "@testing-library/react";
import { afterEach } from "vitest";

// findBy* do Testing Library esperam1s no default — sob contenção de
// máquina (Docker/AV) a render legítima passa disso e o teste falha à
// toa.5s cobre a rajada; falha de verdade (elemento nunca aparece)
// continua falhando.
configure({ asyncUtilTimeout: 5000 });

// jsdom não tem engine de layout: `HTMLElement.offsetParent` é SEMPRE null.
// O `DomHandler.absolutePosition` do PrimeReact aí cai em
// `getHiddenElementDimensions`, que mede o overlay com display:block e
// "restaura" display:none — ou seja, todo `<Menu popup>` abre INVISÍVEL no
// jsdom (e `getByRole("menu")` não o acha, corretamente). Em browser o
// offsetParent existe e o caminho não roda; este shim devolve o comportamento
// de browser. Qualquer ancestral serve: os cálculos de posição usam
// getBoundingClientRect, que continua zerado aqui — irrelevante para os testes.
Object.defineProperty(HTMLElement.prototype, "offsetParent", {
  get(this: HTMLElement) {
    return this.parentElement;
  },
  configurable: true,
});

// `globals: false` no vitest.config.ts desliga o auto-cleanup do Testing
// Library (ele depende de um `afterEach` global) — sem isto as renders
// acumulam entre testes e os `getBy*` acham duplicatas.
afterEach(() => {
  cleanup();
});
