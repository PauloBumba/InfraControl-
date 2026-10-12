import { describe, expect, it } from "vitest";
import { errorMessage } from "./queries";

/**
 * F29-T001B — `messageFor(error: unknown)` estava reimplementado em 13 páginas
 * com o MESMO corpo, e a mesma string literal aparecia em 23 arquivos. A
 * função já quase existia: `mutationErrorMessage` (mesma frase, só que exige
 * `Error | null`) — o que faltava era cobrir o `caught` de um `catch`, que é
 * `unknown` e nem sempre é `Error`.
 */
describe("errorMessage", () => {
  it("should prefer the message the server sent", () => {
    expect(errorMessage(new Error("O nome de exibição é obrigatório."))).toBe(
      "O nome de exibição é obrigatório.",
    );
  });

  it("should fall back when the error carries no message", () => {
    expect(errorMessage(new Error(""))).toBe(
      "Não foi possível falar com o servidor. Tente novamente.",
    );
  });

  it("should fall back when it is not even an Error", () => {
    expect(errorMessage("boom")).toBe("Não foi possível falar com o servidor. Tente novamente.");
    expect(errorMessage(undefined)).toBe(
      "Não foi possível falar com o servidor. Tente novamente.",
    );
  });

  it("should honour a caller-supplied fallback", () => {
    // Knowledge e Login são as duas cópias com texto próprio — sem isto,
    // migrar as duas significaria trocar a mensagem que o usuário vê.
    expect(errorMessage(null, "Informe suas credenciais.")).toBe("Informe suas credenciais.");
    expect(
      errorMessage(new Error("Usuário ou senha incorretos."), "Informe suas credenciais."),
    ).toBe("Usuário ou senha incorretos.");
  });
});
