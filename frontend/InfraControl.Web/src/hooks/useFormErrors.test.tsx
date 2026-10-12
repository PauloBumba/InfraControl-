import { describe, expect, it } from "vitest";
import { act, renderHook } from "@testing-library/react";
import { useFormErrors } from "./useFormErrors";

/**
 * F29-T001B — o estado de erro dos formulários.
 *
 * `const [formError, setFormError] = useState<string | null>(null)` estava
 * reimplementado em 12 páginas/dialogs, com as mesmas três chamadas: zerar ao
 * abrir, gravar na validação, gravar no catch.
 *
 * O que o hook acrescenta — e é o motivo dele existir — é a associação entre
 * erro e campo. Hoje várias páginas penduram o erro do formulário INTEIRO em
 * UM campo fixo (`<FormField label="Título" error={formError}>`): se a
 * validação falhar porque o *conteúdo* está vazio, o recado aparece escrito
 * embaixo do título. Aqui o campo é declarado em quem falha.
 */
describe("useFormErrors", () => {
  it("should start with no error at all", () => {
    const { result } = renderHook(() => useFormErrors());

    expect(result.current.error).toBeNull();
    expect(result.current.errorFor("title")).toBeNull();
  });

  it("should keep a form-level failure away from every field", () => {
    const { result } = renderHook(() => useFormErrors());

    act(() => result.current.fail("Selecione ao menos uma role."));

    expect(result.current.error).toBe("Selecione ao menos uma role.");
    expect(result.current.errorFor("title")).toBeNull();
    expect(result.current.errorFor("content")).toBeNull();
  });

  it("should attach a failure to the field that failed", () => {
    const { result } = renderHook(() => useFormErrors());

    act(() => result.current.fail("Informe o conteúdo do artigo.", "content"));

    expect(result.current.errorFor("content")).toBe("Informe o conteúdo do artigo.");
    expect(result.current.errorFor("title")).toBeNull();
  });

  it("should let the last failure win", () => {
    const { result } = renderHook(() => useFormErrors());

    act(() => result.current.fail("Informe o título do artigo.", "title"));
    act(() => result.current.fail("Informe o conteúdo do artigo.", "content"));

    expect(result.current.errorFor("title")).toBeNull();
    expect(result.current.errorFor("content")).toBe("Informe o conteúdo do artigo.");
  });

  it("reset should clear the message AND the field it was attached to", () => {
    const { result } = renderHook(() => useFormErrors());

    act(() => result.current.fail("Informe o título do artigo.", "title"));
    act(() => result.current.reset());

    expect(result.current.error).toBeNull();
    expect(result.current.errorFor("title")).toBeNull();
  });
});
