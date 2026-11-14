import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { IntegrationsHubPage } from "./IntegrationsHubPage";
import { getUnifiConfig } from "@/services/api/unifi";
import { getZabbixConfig } from "@/services/api/zabbix";
import { getWhatsAppSettings } from "@/services/api/whatsapp";
import { getEmailConfig } from "@/services/api/email";

vi.mock("@/services/api/unifi", () => ({ getUnifiConfig: vi.fn() }));
vi.mock("@/services/api/zabbix", () => ({ getZabbixConfig: vi.fn() }));
vi.mock("@/services/api/whatsapp", () => ({ getWhatsAppSettings: vi.fn() }));
vi.mock("@/services/api/email", () => ({ getEmailConfig: vi.fn() }));

const unifiMock = vi.mocked(getUnifiConfig);
const zabbixMock = vi.mocked(getZabbixConfig);
const waMock = vi.mocked(getWhatsAppSettings);
const emailMock = vi.mocked(getEmailConfig);

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <IntegrationsHubPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("IntegrationsHubPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    unifiMock.mockResolvedValue({
      passwordSet: true,
      source: "database",
    } as Awaited<ReturnType<typeof getUnifiConfig>>);
    zabbixMock.mockResolvedValue({
      passwordSet: true,
      source: "environment",
      lastTestOk: true,
    } as Awaited<ReturnType<typeof getZabbixConfig>>);
    waMock.mockResolvedValue({
      apiKeySet: false,
      source: "environment",
    } as Awaited<ReturnType<typeof getWhatsAppSettings>>);
    emailMock.mockResolvedValue({
      host: "smtp.example.com",
      passwordSet: false,
      source: "database",
    } as Awaited<ReturnType<typeof getEmailConfig>>);
  });

  it("renders the four integration cards with links to their config pages", async () => {
    renderPage();
    expect(await screen.findByText("UniFi Network")).toBeInTheDocument();
    expect(screen.getByText("Zabbix")).toBeInTheDocument();
    expect(screen.getByText("WhatsApp")).toBeInTheDocument();
    expect(screen.getByText("Email / SMTP")).toBeInTheDocument();
    const links = screen.getAllByRole("link", { name: "Configurar" });
    expect(links.map((a) => a.getAttribute("href"))).toEqual([
      "/integrations/unifi",
      "/integrations/zabbix",
      "/integrations/whatsapp",
      "/integrations/email",
    ]);
  });

  it("shows configured status with source and unconfigured for missing credentials", async () => {
    renderPage();
    await waitFor(() =>
      expect(screen.getAllByText("configurada (banco)").length).toBeGreaterThan(0),
    );
    expect(screen.getAllByText("configurada (env)").length).toBeGreaterThan(0);
    expect(screen.getAllByText("não configurada").length).toBeGreaterThanOrEqual(1);
  });

  it("keeps other cards when one config endpoint fails", async () => {
    zabbixMock.mockRejectedValue(new Error("down"));
    renderPage();
    await waitFor(() =>
      expect(screen.getByText("status indisponível")).toBeInTheDocument(),
    );
    expect(screen.getByText("UniFi Network")).toBeInTheDocument();
    expect(screen.getByText("Email / SMTP")).toBeInTheDocument();
  });
});
