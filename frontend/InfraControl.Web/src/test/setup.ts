import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";

// `globals: false` no vitest.config.ts desliga o auto-cleanup do Testing
// Library (ele depende de um `afterEach` global) — sem isto as renders
// acumulam entre testes e os `getBy*` acham duplicatas.
afterEach(() => {
  cleanup();
});
