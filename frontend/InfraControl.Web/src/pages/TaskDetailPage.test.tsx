import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createMemoryRouter, RouterProvider, useParams } from "react-router-dom";
import { TaskDetailPage } from "./TaskDetailPage";
import { store } from "@/app/store";
import { sessionChecked } from "@/app/store/slices/authSlice";
import type { TaskItem } from "@/services/api/tasks";

/**
 * ADR-061 §3 (H5): a rota /tasks/:id usa `key={id}` (TaskDetailRoute no
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
    updateTask: vi.fn(),
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
  updateTask,
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
const updateTaskMock = vi.mocked(updateTask);

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

    await act(async () => {
      await router.navigate("/tasks/task-b");
    });

    // Remount: o estado de A não sobrevive — tela de loading, sem A.
    expect(screen.queryByText("Task A")).toBeNull();
    expect(screen.getByText("Carregando…")).toBeInTheDocument();

    resolveB(taskB);
    expect(await screen.findByText("Task B")).toBeInTheDocument();
    expect(screen.queryByText("Task A")).toBeNull();
  });
});

/**
 * ADR-061 §3 (M2): `run()` só executa o `after` no sucesso — antes o
 * diálogo fechava e o texto digitado era apagado mesmo quando a mutação
 * falhava (banner de erro + digitação perdida).
 */
describe("TaskDetailPage — falha de mutação preserva o diálogo (M2)", () => {
  it("keeps the data dialog open with the typed title when save fails", async () => {
    getTaskMock.mockResolvedValue(umaTask("task-a", "Task A"));
    updateTaskMock.mockRejectedValue(new Error("recusado pelo servidor"));

    const user = userEvent.setup();
    const router = createMemoryRouter(
      [{ path: "/tasks/:id", element: <Routed /> }],
      { initialEntries: ["/tasks/task-a"] },
    );
    render(
      <Provider store={store}>
        <QueryClientProvider
          client={
            new QueryClient({
              defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
            })
          }
        >
          <RouterProvider router={router} />
        </QueryClientProvider>
      </Provider>,
    );

    await screen.findByText("Task A");
    // Dois "Editar" (Descrição/dados primeiro, Identificação depois).
    const editar = screen.getAllByRole("button", { name: /editar/i });
    await user.click(editar[0]!);

    const titulo = await screen.findByLabelText("Título");
    await user.clear(titulo);
    await user.type(titulo, "Novo título digitado");
    await user.click(screen.getByRole("button", { name: "Salvar" }));

    expect(await screen.findByText("recusado pelo servidor")).toBeInTheDocument();
    // Diálogo aberto + texto preservado (antes: fechava e limpava).
    expect(screen.getByText("Editar dados da tarefa")).toBeInTheDocument();
    expect(screen.getByLabelText("Título")).toHaveValue("Novo título digitado");
  });
});
