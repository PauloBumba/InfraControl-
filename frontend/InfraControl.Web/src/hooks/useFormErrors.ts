import { useCallback, useState } from "react";

/**
 * F29-T001B — o estado de erro dos formulários.
 *
 * `const [formError, setFormError] = useState<string | null>(null)` estava
 * reimplementado em 12 páginas e dialogs, sempre com as mesmas três chamadas:
 * zerar ao abrir o formulário, gravar na validação, gravar no `catch`.
 *
 * O que o hook acrescenta — e é o motivo dele existir — é a associação entre
 * erro e campo. Várias páginas penduram hoje o erro do formulário INTEIRO em
 * UM campo fixo (`<FormField label="Título" error={formError}>`): quando a
 * validação falha porque o *conteúdo* está vazio, o recado aparece escrito
 * embaixo do título, apontando o campo errado. Aqui quem falha declara o
 * próprio campo.
 *
 * `error` é o recado do formulário inteiro (para `<FormError>`/`ErrorBanner`);
 * `errorFor(nome)` é o mesmo recado só para o campo que o originou, e `null`
 * para todos os outros.
 */
export function useFormErrors() {
  const [error, setError] = useState<string | null>(null);
  /** Campo a que o erro se refere; `null` = erro do formulário inteiro. */
  const [field, setField] = useState<string | null>(null);

  /** Zera erro e campo — abrir o formulário, ou começar uma tentativa nova. */
  const reset = useCallback(() => {
    setError(null);
    setField(null);
  }, []);

  /** Grava a falha; `onField` a ata a um campo específico, senão é do formulário. */
  const fail = useCallback((message: string, onField?: string) => {
    setError(message);
    setField(onField ?? null);
  }, []);

  /** Mensagem para o `FormField` de `name` — `null` se o erro não é dele. */
  const errorFor = useCallback(
    (name: string): string | null => (field === name ? error : null),
    [field, error],
  );

  return { error, field, reset, fail, errorFor };
}
