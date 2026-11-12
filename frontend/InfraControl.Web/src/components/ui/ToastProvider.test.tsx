import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { ToastProvider, TOAST_DURATION_MS, useToast } from "./ToastProvider";

function Trigger() {
  const toast = useToast();
  return (
    <div>
      <button type="button" onClick={() => toast.success("Projeto criado.")}>
        ok
      </button>
      <button type="button" onClick={() => toast.error("Falha ao salvar.")}>
        erro
      </button>
      <button type="button" onClick={() => toast.info("Atualizando…")}>
        info
      </button>
    </div>
  );
}

function renderToasts() {
  return render(
    <ToastProvider>
      <Trigger />
    </ToastProvider>,
  );
}

describe("ToastProvider", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("shows a success toast with the message content (role=status)", () => {
    renderToasts();
    fireEvent.click(screen.getByRole("button", { name: "ok" }));

    const toast = screen.getByRole("status");
    expect(toast).toHaveTextContent("Projeto criado.");
    expect(toast).toHaveAttribute("data-testid", "toast");
  });

  it("auto-dismisses on the useEffect timeline after TOAST_DURATION_MS", () => {
    renderToasts();
    fireEvent.click(screen.getByRole("button", { name: "ok" }));
    expect(screen.getByRole("status")).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(TOAST_DURATION_MS - 1);
    });
    expect(screen.getByRole("status")).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(2);
    });
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });

  it("renders errors with role=alert and info with role=status", () => {
    renderToasts();
    fireEvent.click(screen.getByRole("button", { name: "erro" }));
    fireEvent.click(screen.getByRole("button", { name: "info" }));

    expect(screen.getByRole("alert")).toHaveTextContent("Falha ao salvar.");
    expect(screen.getByRole("status")).toHaveTextContent("Atualizando…");
  });

  it("dismisses through the close button and keeps the others", () => {
    renderToasts();
    fireEvent.click(screen.getByRole("button", { name: "ok" }));
    fireEvent.click(screen.getByRole("button", { name: "info" }));

    const successToast = screen
      .getByText("Projeto criado.")
      .closest('[data-testid="toast"]') as HTMLElement;
    fireEvent.click(
      within(successToast).getByRole("button", { name: "Fechar" }),
    );
    expect(screen.queryByText("Projeto criado.")).not.toBeInTheDocument();
    expect(screen.getByText("Atualizando…")).toBeInTheDocument();
  });

  it("keeps at most 3 toasts (the oldest leaves)", () => {
    renderToasts();
    fireEvent.click(screen.getByRole("button", { name: "ok" }));
    fireEvent.click(screen.getByRole("button", { name: "erro" }));
    fireEvent.click(screen.getByRole("button", { name: "info" }));
    fireEvent.click(screen.getByRole("button", { name: "ok" }));

    expect(screen.queryByText("Projeto criado.")).toBeInTheDocument();
    expect(screen.getAllByTestId("toast")).toHaveLength(3);
  });

  it("is a no-op outside the provider (adoção gradual sem quebrar páginas)", () => {
    render(<Trigger />);
    fireEvent.click(screen.getByRole("button", { name: "ok" }));
    expect(screen.queryByTestId("toast")).not.toBeInTheDocument();
  });
});
