import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import "./toast.css";

export type ToastVariant = "success" | "error" | "info";

export interface ToastItem {
  id: number;
  message: string;
  variant: ToastVariant;
}

export interface ToastApi {
  success: (message: string) => void;
  error: (message: string) => void;
  info: (message: string) => void;
}

/** Duração da linha de tempo (auto-dismiss) — usada também no CSS via var. */
export const TOAST_DURATION_MS = 4000;

/** Máximo de toasts na pilha: o mais antigo sai. */
export const MAX_TOASTS = 3;

// Fora do provider a API é no-op: páginas/testes adotam useToast() sem
// depender de wrapper obrigatório (adoção gradual pela aplicação).
const NOOP_TOAST: ToastApi = {
  success: () => undefined,
  error: () => undefined,
  info: () => undefined,
};

const ToastContext = createContext<ToastApi>(NOOP_TOAST);

/**
 * Host global de notificações transitórias (F50 — "mesmo método para
 * toda a aplicação"): mensagem some sozinha após TOAST_DURATION_MS
 * (timeline em useEffect + setTimeout) e pode ser fechada manualmente.
 * Uso: <ToastProvider> no root (main.tsx) + useToast() na página.
 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const nextId = useRef(1);

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);

  const push = useCallback((message: string, variant: ToastVariant) => {
    const id = nextId.current;
    nextId.current += 1;
    setToasts((current) => [
      ...current.slice(-(MAX_TOASTS - 1)),
      { id, message, variant },
    ]);
  }, []);

  const api = useMemo<ToastApi>(
    () => ({
      success: (message) => push(message, "success"),
      error: (message) => push(message, "error"),
      info: (message) => push(message, "info"),
    }),
    [push],
  );

  return (
    <ToastContext.Provider value={api}>
      {children}
      <ToastHost toasts={toasts} onDismiss={dismiss} />
    </ToastContext.Provider>
  );
}

export function useToast(): ToastApi {
  return useContext(ToastContext);
}

function ToastHost({
  toasts,
  onDismiss,
}: {
  toasts: ToastItem[];
  onDismiss: (id: number) => void;
}) {
  if (toasts.length === 0) return null;
  return (
    <div className="uni-toast-host" aria-live="polite">
      {toasts.map((toast) => (
        <ToastCard key={toast.id} toast={toast} onDismiss={onDismiss} />
      ))}
    </div>
  );
}

function ToastCard({
  toast,
  onDismiss,
}: {
  toast: ToastItem;
  onDismiss: (id: number) => void;
}) {
  // Linha de tempo: some sozinho após TOAST_DURATION_MS; o cleanup do
  // clearTimeout evita timer órfão ao desmontar/trocar de toast.
  useEffect(() => {
    const timer = window.setTimeout(
      () => onDismiss(toast.id),
      TOAST_DURATION_MS,
    );
    return () => window.clearTimeout(timer);
  }, [toast.id, onDismiss]);

  const icon =
    toast.variant === "success"
      ? "pi-check-circle"
      : toast.variant === "error"
        ? "pi-times-circle"
        : "pi-info-circle";

  return (
    <div
      className={`uni-toast uni-toast--${toast.variant}`}
      role={toast.variant === "error" ? "alert" : "status"}
      data-testid="toast"
      style={
        { "--uni-toast-duration": `${TOAST_DURATION_MS}ms` } as React.CSSProperties
      }
    >
      <i className={`pi ${icon} uni-toast__icon`} aria-hidden />
      <span className="uni-toast__message">{toast.message}</span>
      <button
        type="button"
        className="uni-toast__close"
        aria-label="Fechar"
        onClick={() => onDismiss(toast.id)}
      >
        <i className="pi pi-times" aria-hidden />
      </button>
      <span className="uni-toast__timeline" aria-hidden />
    </div>
  );
}
