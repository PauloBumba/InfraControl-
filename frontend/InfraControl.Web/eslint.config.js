import js from "@eslint/js";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import tseslint from "typescript-eslint";

export default tseslint.config(
  { ignores: ["dist/**", "node_modules/**"] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    plugins: { "react-hooks": reactHooks },
    rules: {
      // Regras do React usadas pelo código (os inline
      // `eslint-disable-next-line react-hooks/exhaustive-deps` pressupõem o
      // plugin). Só as duas clássicas: as demais do plugin vêm do React
      // Compiler e mudam o contrato do código existente.
      "react-hooks/rules-of-hooks": "error",
      "react-hooks/exhaustive-deps": "warn",
    },
  },
  {
    languageOptions: {
      globals: {
        ...globals.browser,
        ...globals.node,
      },
    },
    rules: {
      // `_` marca parâmetro/variável ignorado de propósito (callbacks do
      // SignalR/Roteadores) — o camelCase do projeto não convive com o
      // "never prefix" que a regra default do TS assumiria.
      "@typescript-eslint/no-unused-vars": [
        "error",
        {
          argsIgnorePattern: "^_",
          varsIgnorePattern: "^_",
          caughtErrorsIgnorePattern: "^_",
        },
      ],
    },
  },
);
