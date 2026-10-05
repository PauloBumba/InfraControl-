import { useEffect, type CSSProperties, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/Button";

interface DialogProps {
  visible: boolean;
  onHide: () => void;
  header: string;
  children: ReactNode;
  footer?: ReactNode;
  style?: CSSProperties;
  className?: string;
  width?: number | string;
}

export function Dialog({
  visible,
  onHide,
  header,
  children,
  footer,
  style,
  className,
  width = 440,
}: DialogProps) {
  useEffect(() => {
    if (!visible) return;

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onHide();
    };

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", onKeyDown);

    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [visible, onHide]);

  if (!visible) return null;

  const panelStyle: CSSProperties = {
    width: typeof width === "number" ? `${width}px` : width,
    ...style,
  };

  return createPortal(
    <div className="uni-modal-root" role="presentation">
      <button
        type="button"
        className="uni-modal-backdrop"
        aria-label="Fechar"
        onClick={onHide}
      />
      <div
        className={`uni-modal ${className ?? ""}`.trim()}
        role="dialog"
        aria-modal="true"
        aria-label={header}
        style={panelStyle}
        onClick={(event) => event.stopPropagation()}
      >
        <div className="uni-modal-header">
          <h2 className="uni-modal-title">{header}</h2>
          <button
            type="button"
            className="uni-modal-close"
            aria-label="Fechar"
            onClick={onHide}
          >
            <i className="pi pi-times" />
          </button>
        </div>
        <div className="uni-modal-body">{children}</div>
        {footer != null && <div className="uni-modal-footer">{footer}</div>}
      </div>
    </div>,
    document.body,
  );
}

interface ConfirmDialogProps {
  visible: boolean;
  onHide: () => void;
  onConfirm: () => void;
  header: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  severity?: "danger" | "warning" | "info";
}

export function ConfirmDialog({
  visible,
  onHide,
  onConfirm,
  header,
  message,
  confirmLabel = "Confirmar",
  cancelLabel = "Cancelar",
  severity = "danger",
}: ConfirmDialogProps) {
  const handleConfirm = () => {
    onConfirm();
    onHide();
  };

  return (
    <Dialog
      visible={visible}
      onHide={onHide}
      header={header}
      width={400}
      footer={
        <div className="uni-modal-actions">
          <Button variant="ghost" onClick={onHide}>
            {cancelLabel}
          </Button>
          <Button variant={severity} onClick={handleConfirm}>
            {confirmLabel}
          </Button>
        </div>
      }
    >
      <p className="uni-modal-message">{message}</p>
    </Dialog>
  );
}
