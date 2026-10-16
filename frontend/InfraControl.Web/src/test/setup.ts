import "@testing-library/jest-dom/vitest";
import { cleanup, configure } from "@testing-library/react";
import { afterEach } from "vitest";

// findBy* do Testing Library esperam1s no default — sob contenção de
// máquina (Docker/AV) a render legítima passa disso e o teste falha à
// toa.5s cobre a rajada; falha de verdade (elemento nunca aparece)
// continua falhando.
configure({ asyncUtilTimeout: 5000 });

// `globals: false` no vitest.config.ts desliga o auto-cleanup do Testing
// Library (ele depende de um `afterEach` global) — sem isto as renders
// acumulam entre testes e os `getBy*` acham duplicatas.
afterEach(() => {
  cleanup();
});
