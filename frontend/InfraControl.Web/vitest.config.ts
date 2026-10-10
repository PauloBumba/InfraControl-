import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import path from "path";

/**
 * Configuração da suíte do front (F26-T001 — o front nunca teve testes;
 * Vitest + Testing Library + jsdom). O alias `@` espelha o vite.config.ts e
 * o `tsconfig.json`. Os testes vivem em `src/**\/*.test.{ts,tsx}` e ficam
 * FORA do `tsconfig.json` (include: src) só em espírito — o `tsc -b` do
 * build os enxerga; por isso o próprio arquivo de teste precisa passar no
 * typecheck estrito do projeto.
 */
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  test: {
    environment: "jsdom",
    globals: false,
    setupFiles: ["./src/test/setup.ts"],
    include: ["src/**/*.test.{ts,tsx}"],
    restoreMocks: true,
  },
});
