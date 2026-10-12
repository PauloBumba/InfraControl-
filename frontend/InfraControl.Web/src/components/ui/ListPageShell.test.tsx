import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ListPageShell, countLabel } from "./ListPageShell";

describe("countLabel", () => {
  it("should use the singular for exactly one", () => {
    expect(countLabel(1, "cliente", "clientes")).toBe("1 cliente");
  });

  it("should use the plural for zero and for more than one", () => {
    expect(countLabel(0, "cliente", "clientes")).toBe("0 clientes");
    expect(countLabel(2, "cliente", "clientes")).toBe("2 clientes");
  });

  it("should never guess the plural — it is written by the page", () => {
    // "usuário" + "s" = "usuárioss": se a casca inferisse, quebraria silencioso.
    expect(countLabel(1, "usuário", "usuários")).toBe("1 usuário");
    expect(countLabel(3, "usuário", "usuários")).toBe("3 usuários");
  });
});

describe("ListPageShell", () => {
  it("should render the title and the count subtitle", () => {
    render(
      <ListPageShell title="Clientes" singular="cliente" plural="clientes" total={3}>
        <div>conteúdo</div>
      </ListPageShell>,
    );

    expect(screen.getByRole("heading", { level: 1, name: "Clientes" })).toBeInTheDocument();
    expect(screen.getByText("3 clientes")).toBeInTheDocument();
    expect(screen.getByText("conteúdo")).toBeInTheDocument();
  });

  it("should replace the count with Carregando… while loading", () => {
    render(
      <ListPageShell title="Clientes" singular="cliente" plural="clientes" total={3} loading>
        <div>conteúdo</div>
      </ListPageShell>,
    );

    expect(screen.getByText("Carregando…")).toBeInTheDocument();
    expect(screen.queryByText("3 clientes")).not.toBeInTheDocument();
  });

  it("should append the note after the count", () => {
    render(
      <ListPageShell
        title="Clientes"
        singular="cliente"
        plural="clientes"
        total={1}
        note='busca "rack"'
      />,
    );

    expect(screen.getByText('1 cliente · busca "rack"')).toBeInTheDocument();
  });

  it("should show the error as an alert and call onRetry", async () => {
    const onRetry = vi.fn();
    render(
      <ListPageShell
        title="Clientes"
        singular="cliente"
        plural="clientes"
        error="Não foi possível falar com o servidor. Tente novamente."
        onRetry={onRetry}
      />,
    );

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("Não foi possível falar com o servidor. Tente novamente.");

    await userEvent.click(screen.getByRole("button", { name: "Tentar de novo" }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it("should render actions and the footer", () => {
    render(
      <ListPageShell
        title="Alertas"
        singular="alerta"
        plural="alertas"
        actions={<button type="button">Filtrar</button>}
        footer={<div>Página 1 de 2</div>}
      />,
    );

    expect(screen.getByRole("button", { name: "Filtrar" })).toBeInTheDocument();
    expect(screen.getByText("Página 1 de 2")).toBeInTheDocument();
  });

  it("should keep the notice above the error, where the pages already had it", () => {
    const { container } = render(
      <ListPageShell
        title="Hosts monitorados"
        singular="host"
        plural="hosts"
        notice={<div>Sincronização concluída</div>}
        error="Não foi possível sincronizar."
      />,
    );

    expect(screen.getByText("Sincronização concluída")).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent("Não foi possível sincronizar.");
    // A ordem importa: a ZabbixHostsPage já mostrava o aviso ACIMA do erro,
    // e a casca não pode reordenar o que a página escolheu mostrar.
    expect(container.innerHTML.indexOf("Sincronização concluída")).toBeLessThan(
      container.innerHTML.indexOf("Não foi possível sincronizar."),
    );
  });
});
