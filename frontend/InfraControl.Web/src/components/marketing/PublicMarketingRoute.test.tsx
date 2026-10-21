import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { PublicMarketingRoute } from "./PublicMarketingRoute";

/**
 * F26-T002 — rotas públicas de vendas não dependem do backend.
 *
 * As páginas públicas (/, /recursos, /contato) devem funcionar mesmo sem
 * backend disponível. O componente agora é um pass-through simples
 * que sempre renderiza o conteúdo, independente do estado de autenticação.
 */
describe("PublicMarketingRoute (F26-T002)", () => {
  it("should render children without any authentication check", () => {
    render(
      <PublicMarketingRoute>
        <div>landing content</div>
      </PublicMarketingRoute>,
    );

    expect(screen.getByText("landing content")).toBeInTheDocument();
  });
});
