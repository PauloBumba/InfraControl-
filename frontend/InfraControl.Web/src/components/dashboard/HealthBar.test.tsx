import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HealthBar } from "./HealthBar";
import { DeviceHealthCard } from "./DeviceHealthCard";

/**
 * F40-T001 — barra de saúde estilo UniFi (dots + rótulo + estado).
 * Componente puramente presentacional: a página deriva os itens dos dados
 * reais; aqui só validamos markup, estados e acessibilidade.
 */
describe("HealthBar (F40-T001)", () => {
  it("renders one item per health entry with label and status word", () => {
    render(
      <HealthBar
        items={[
          { id: "alerts", label: "Alertas", status: "ok", detail: "sem alertas" },
          { id: "incidents", label: "Incidentes", status: "critical", detail: "2 em aberto" },
        ]}
      />,
    );

    expect(screen.getByText("Alertas")).toBeInTheDocument();
    expect(screen.getByText("Incidentes")).toBeInTheDocument();
    expect(screen.getByText("Normal")).toBeInTheDocument();
    expect(screen.getByText("Crítico")).toBeInTheDocument();
    expect(screen.getByText("sem alertas")).toBeInTheDocument();
    expect(screen.getByText("2 em aberto")).toBeInTheDocument();
  });

  it("exposes the warning status word for assistive technology", () => {
    render(<HealthBar items={[{ id: "tasks", label: "Tarefas", status: "warning" }]} />);

    expect(screen.getByText("Atenção")).toBeInTheDocument();
  });

  it("supports the unknown status (sem dado — zero ≠ erro)", () => {
    render(<HealthBar items={[{ id: "devices", label: "Inventário", status: "unknown" }]} />);

    expect(screen.getByText("Sem dado")).toBeInTheDocument();
  });

  it("labels the group for assistive technology", () => {
    render(<HealthBar items={[]} />);

    expect(screen.getByRole("group", { name: "Estado geral da infraestrutura" })).toBeInTheDocument();
  });

  it("marks decorative dots as hidden from assistive technology", () => {
    const { container } = render(
      <HealthBar items={[{ id: "wan", label: "Internet", status: "ok" }]} />,
    );

    const dots = container.querySelectorAll(".uni-dash-health-dot");
    expect(dots.length).toBe(1);
    expect(dots[0]).toHaveAttribute("aria-hidden", "true");
  });
});

/**
 * F40-T001 — card de KPI clicável do dashboard (substitui o MiniStat inline).
 */
describe("DeviceHealthCard (F40-T001)", () => {
  it("renders label, value and sub", () => {
    render(
      <DeviceHealthCard
        icon="pi-server"
        label="Dispositivos"
        value="42"
        sub="no inventário"
      />,
    );

    expect(screen.getByText("Dispositivos")).toBeInTheDocument();
    expect(screen.getByText("42")).toBeInTheDocument();
    expect(screen.getByText("no inventário")).toBeInTheDocument();
  });

  it("navigates via onClick when provided", async () => {
    const onClick = vi.fn();
    render(
      <DeviceHealthCard icon="pi-bell" label="Alertas" value="3" onClick={onClick} />,
    );

    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: /alertas/i }));

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it("renders as a non-interactive element without onClick", () => {
    const { container } = render(
      <DeviceHealthCard icon="pi-users" label="Clientes" value="10" />,
    );

    expect(screen.queryByRole("button")).toBeNull();
    expect(container.querySelector(".uni-dash-stat")).toBeInTheDocument();
  });

  it("applies the tone modifier class when tone is given", () => {
    const { container } = render(
      <DeviceHealthCard
        icon="pi-exclamation-triangle"
        label="Incidentes"
        value="2"
        tone="critical"
      />,
    );

    expect(container.querySelector(".uni-dash-stat-icon.is-critical")).toBeInTheDocument();
  });
});
