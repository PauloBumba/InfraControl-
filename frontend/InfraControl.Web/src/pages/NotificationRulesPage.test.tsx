import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { NotificationRulesPage } from "./NotificationRulesPage";
import * as api from "@/services/api/notificationRules";

vi.mock("@/services/api/notificationRules");

const RULES: api.NotificationEventRule[] = [
  {
    eventType: "IncidentCreatedDomainEvent",
    category: "Operational",
    description: "Incidente aberto no InfraControl.",
    enabled: true,
    channels: [],
  },
  {
    eventType: "TaskItemCreatedDomainEvent",
    category: "Tasks",
    description: "Tarefa criada no InfraControl.",
    enabled: false,
    channels: ["Email"],
  },
];

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <NotificationRulesPage />
    </QueryClientProvider>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("NotificationRulesPage", () => {
  it("lists catalog events with default fanout mode", async () => {
    vi.mocked(api.listNotificationRules).mockResolvedValue(RULES);

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("IncidentCreatedDomainEvent")).toBeInTheDocument();
    });
    expect(screen.getByText("TaskItemCreatedDomainEvent")).toBeInTheDocument();
    expect(screen.getByText("Fanout global")).toBeInTheDocument();
    expect(screen.getByText("Silenciado")).toBeInTheDocument();
  });

  it("saves toggled rules on submit", async () => {
    vi.mocked(api.listNotificationRules).mockResolvedValue(RULES);
    vi.mocked(api.saveNotificationRules).mockResolvedValue([
      { ...RULES[0]!, enabled: false },
      RULES[1]!,
    ]);

    renderPage();
    await waitFor(() => {
      expect(screen.getByText("IncidentCreatedDomainEvent")).toBeInTheDocument();
    });

    const checkbox = screen.getByLabelText(
      "Ativar IncidentCreatedDomainEvent"
    );
    await userEvent.click(checkbox);
    await userEvent.click(screen.getByRole("button", { name: /salvar regras/i }));

    await waitFor(() => {
      expect(api.saveNotificationRules).toHaveBeenCalledWith([
        { eventType: "IncidentCreatedDomainEvent", enabled: false, channels: null },
        { eventType: "TaskItemCreatedDomainEvent", enabled: false, channels: ["Email"] },
      ] as api.SaveNotificationEventRulePayload[]);
    });
  });
});
