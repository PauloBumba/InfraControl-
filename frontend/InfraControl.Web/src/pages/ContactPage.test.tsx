import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { ContactPage } from "./ContactPage";

/**
 * F26-T002 — página de contato.
 *
 * Decisão do usuário (2026-10-11): **canais honestos, sem formulário** —
 * não existe endpoint de contato no backend, então a página não mostra um
 * form que finge enviar.
 *
 * O endereço de suporte NÃO é inventado: vem de `VITE_SUPPORT_EMAIL`
 * (mesmo padrão opcional do `VITE_GOOGLE_CLIENT_ID`). Sem valor no build,
 * a página diz que a instância é self-hosted e manda falar com o
 * administrador — em vez de exibir um e-mail fictício.
 */
function renderPage() {
  return render(
    <MemoryRouter>
      <ContactPage />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.stubEnv("VITE_SUPPORT_EMAIL", "");
});

afterEach(() => {
  vi.unstubAllEnvs();
});

describe("ContactPage (F26-T002)", () => {
  it("should never render a contact form (there is no endpoint)", () => {
    renderPage();

    expect(screen.queryByRole("form")).toBeNull();
    expect(screen.queryByRole("button", { name: /enviar/i })).toBeNull();
  });

  it("should point an anonymous visitor to the login", () => {
    renderPage();

    expect(screen.getAllByRole("link", { name: /entrar/i })[0]).toHaveAttribute("href", "/login");
  });

  it("should say the instance is self-hosted when no support e-mail is configured", () => {
    renderPage();

    // "self-hosted" aparece no lead E no card de suporte.
    expect(screen.getAllByText(/self-hosted/i).length).toBeGreaterThan(0);
    expect(screen.queryByRole("link", { name: /@/ })).toBeNull();
  });

  it("should link the configured support address when VITE_SUPPORT_EMAIL is set", () => {
    vi.stubEnv("VITE_SUPPORT_EMAIL", "suporte@acme.com");
    renderPage();

    const link = screen.getByRole("link", { name: /suporte@acme\.com/ });
    expect(link).toHaveAttribute("href", "mailto:suporte@acme.com");
  });
});
