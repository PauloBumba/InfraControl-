import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { Provider } from "react-redux";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createMemoryRouter, RouterProvider, useParams } from "react-router-dom";
import { TaskDetailPage } from "./TaskDetailPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import type { TaskItem } from "@/services/api/tasks";

/**
 * ADR-059 §3 (H5): a rota /tasks/:id usa `key={id}` (TaskDetailRoute no
 * router) — o remount zera o estado na troca pai↔subtarefa. Sem isso, a
 * task A ficava na tela sob a URL de B (loading=false) e um "Excluir"
 * confirmava e excluía A. Aqui se tranca: ao navegar para B, A DESAPARECE
 * antes de a resposta de B chegar.
 */
vi.mock("@/services/api/tasks", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/tasks")>();
  return {
    ...actual,
    getTask: vi.fn(),
    getTaskComments: vi.fn(),
    getTaskTimeline: vi.fn(),
    getTaskCriteria: vi.fn(),
    listSubtasks: vi.fn(),
    getTaskImpediments: vi.fn(),
    getTaskEvidence: vi.fn(),
    getTaskPhases: vi.fn(),
    getTaskSla: vi.fn(),
    getTaskSlaPauses: vi.fn(),
    deleteTask: vi.fn(),
  };
});

vi.mock("@/services/api/adminUsers", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/adminUsers")>();
  return { ...actual, listUsers: vi.fn().mockResolvedValue([]) };
});
vi.mock("@/services/api/inventory", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/inventory")>();
  return {
    ...actual,
    listDevices: vi.fn().mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 100 }),
  };
});
vi.mock("@/services/api/incidents", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/incidents")>();
  return {
    ...actual,
    listIncidents: vi.fn().mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 }),
  };
});
vi.mock("@/services/api/sla", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/sla")>();
  return {
    ...actual,
    listSlaPolicies: vi.fn().mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 50 }),
  };
});
vi.mock("@/services/api/knowledge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/knowledge")>();
  return {
    ...actual,
    listArticles: vi.fn().mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 50 }),
  };
});
vi.mock("@/services/api/pdi", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/pdi")>();
  return { ...actual, listPdis: vi.fn().mockResolvedValue([]) };
});

import {
  getTask,
  getTaskComments,
  getTaskCriteria,
  getTaskEvidence,
  getTaskImpediments,
  getTaskPhases,
  getTaskSla,
  getTaskSlaPauses,
  getTaskTimeline,
  listSubtasks,
} from "@/services/api/tasks";

const getTaskMock = vi.mocked(getTask);

function umaTask(id: string, title: string): TaskItem {
  return {
    id,
    title,
    description: null,
    kind: "Manual",
    state: "Open",
    openedAt: "2026-10-01T10:00:00Z",
    assigneeCount: 0,
    assigneeIds: [],
    deviceCount: 0,
    deviceIds: [],
    incidentCount: 0,
    incidentIds: [],
    articleCount: 0,
    articleIds: [],
    pdiCount: 0,
    pdiUserIds: [],
    priority: "Medium",
    dueDate: null,
    responsibleId: null,
    responsibleName: null,
    plannedStart: null,
    plannedEnd: null,
    result: null,
    parentTaskId: null,
    parentTitle: null,
    subtaskCount: 0,
    progress: null,
    createdById: null,
    createdByName: null,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  store.dispatch(
    sessionChecked({
      id: "3f1a1111-2222-3333-4444-555566667777",
      email: "ana@exemplo.com",
      displayName: "Ana",
      roles: ["Operator"],
    }),
  );
  vi.mocked(getTaskComments).mockResolvedValue([]);
  vi.mocked(getTaskTimeline).mockResolvedValue([]);
  vi.mocked(getTaskCriteria).mockResolvedValue([]);
  vi.mocked(listSubtasks).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 50 });
  vi.mocked(getTaskImpediments).mockResolvedValue([]);
  vi.mocked(getTaskEvidence).mockResolvedValue([]);
  vi.mocked(getTaskPhases).mockResolvedValue([]);
  vi.mocked(getTaskSla).mockResolvedValue(null);
  vi.mocked(getTaskSlaPauses).mockResolvedValue([]);
});

/** Espelha o TaskDetailRoute do router de produção: key={id}. */
function Routed() {
  const { id } = useParams();
  return <TaskDetailPage key={id} />;
}

describe("TaskDetailPage — remount no troca de :id (H5)", () => {
  it("does not keep task A on screen while task B is still loading", async () => {
    const taskA = umaTask("task-a", "Task A");
    const taskB = umaTask("task-b", "Task B");

    let resolveB!: (value: TaskItem) => void;
    const slowB = new Promise<TaskItem>((resolve) => {
      resolveB = resolve;
    });
    getTaskMock.mockImplementation((id: string) =>
      id === "task-a" ? Promise.resolve(taskA) : slowB,
    );

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    const router = createMemoryRouter(
      [{ path: "/tasks/:id", element: <Routed /> }],
      { initialEntries: ["/tasks/task-a"] },
    );

    render(
      <Provider store={store}>
        <QueryClientProvider client={queryClient}>
          <RouterProvider router={router} />
        </QueryClientProvider>
      </Provider>,
    );

    expect(await screen.findByText("Task A")).toBeInTheDocument();

    await router.navigate("/tasks/task-b");

    // Remount: o estado de A não sobrevive — tela de loading, sem A.
    expect(screen.queryByText("Task A")).toBeNull();
    expect(screen.getByText("Carregando…")).toBeInTheDocument();

    resolveB(taskB);
    expect(await screen.findByText("Task B")).toBeInTheDocument();
    expect(screen.queryByText("Task A")).toBeNull();
  });
});
