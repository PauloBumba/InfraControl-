/**
 * Campo de formulário com label (Fase B do levantamento docs/28): o wrapper
 * `label` vertical (texto 12px `#64748b` + controle) que era reescrito em
 * ~10 páginas com form. O controle (input/select/textarea) vai como `children`
 * — o componente não decide o widget, só o layout do campo.
 * `error` (opcional) renderiza a mensagem inline embaixo, no padrão atual
 * (`#b91c1c`); para erros soltos do form (após o último campo) use `FormError`.
 */
import type { CSSProperties, ReactNode } from "react";
import { Message } from "primereact/message";

export function FormField({
  label,
  error,
  width,
  children,
}: {
  label: string;
  error?: string | null;
  width?: number;
  children: ReactNode;
}) {
  return (
    <label
      style={{
        display: "flex",
        flexDirection: "column",
        gap: 4,
        fontSize: 12,
        color: "#64748b",
        width,
      }}
    >
      {label}
      {children}
      {error && <span style={{ fontSize: 11, color: "#b91c1c" }}>{error}</span>}
    </label>
  );
}

/**
 * Erro de formulário solto (após os campos, antes dos botões), agora com o
 * `Message` acessível do PrimeReact. `style` cobre variações locais (ex.:
 * Network usa `alignSelf: "center"`).
 */
export function FormError({
  message,
  style,
}: {
  message: string;
  style?: CSSProperties;
}) {
  if (!message) return null;

  return (
    <Message severity="error" text={message} className="uni-form-message" style={style} />
  );
}
