import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PdiStructureDialog } from "./PdiStructureDialog";
import {
  createPdiCompetency,
  createPdiObjective,
  createPdiPhase,
  getPdiStructure,
  listPdiCompetencies,
  validatePdiPhase,
  type Pdi,
  type PdiStructure,
} from "@/services/api/pdi";
import { listUsers } from "@/services/api/adminUsers";

/**
 * F29-T001C-2 — PdiStructureDialog: **11º e último site do C-2**. A página
 * tem 3 estados de erro, um por form — `formError` (criação/edição de nó da
 * árvore), `catalogError` (CRUD de competência) e `rejectError` (rejeição de
 * fase) — e todos três iam para um `<FormError>` no pé do form.
 *
 * O que este teste tranca, decisão por decisão:
 *
 * 1. título vazio (objetivo) → campo "Título", uma única vez na tela,
 *    sem mutation;
 * 2. nome vazio (catálogo) → campo "Nova competência" — cujo **label é
 *    dinâmico** (`catalogForm?.id ? "Editar…" : "Nova competência"`), o
 *    atalho que uma validação pendurada no errado não teria;
 * 3. **período invertido → formulário**: "O fim da fase não pode ser anterior
 *    ao início" descreve DOIS campos; pendurar em um só apontaria metade do
 *    problema. Mesma regra da `AuditPage`, da `SlaPage` e da `PdiPage`;
 * 4. percentual fora de 0–100 → campo "Percentual (%)", sem culpar o título
 *    já preenchido;
 * 5. motivo vazio (rejeição) → campo "Motivo da rejeição";
 * 6. recusa do servidor → formulário, longe dos campos.
 *
 * Por que os dois testes de fase são necessários: são os dois únicos que
 * decidem **para onde vai um erro que não é de um campo só** (3) e um que
 * precisa existir estrutura de campo antes de acender (4). Um teste só não
 * provaria o outro — antes da migração as duas frases saíam do mesmo
 * `<FormError>`, indistinguíveis.
 */
vi.mock("@/services/api/pdi", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/pdi")>();
  return {
    ...actual,
    getPdiStructure: vi.fn(),
    listPdiCompetencies: vi.fn(),
    createPdiObjective: vi.fn(),
    createPdiPhase: vi.fn(),
    createPdiCompetency: vi.fn(),
    validatePdiPhase: vi.fn(),
  };
});

/**
 * `listUsers` entra no mesmo `Promise.all` do carregamento quando `isAdmin` —
 * sem mock a chamada de rede cai, o `Promise.all` inteiro rejeita e a árvore
 * nem chega a renderizar (`+ Fase` / `Rejeitar` não existem).
 */
vi.mock("@/services/api/adminUsers", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/adminUsers")>();
  return { ...actual, listUsers: vi.fn() };
});

/**
 * `SelectField` é wrapper do `Dropdown` do PrimeReact — não abre no jsdom.
 * Mesmo precedente de `KnowledgePage.test.tsx`/`DevicesPage`/`PdiPage`/
 * `PdiAccompanimentDialog.test.tsx`.
 */
vi.mock("@/components/ui/SelectField", () => ({
  SelectField: (props: {
    value: string;
    onChange: (value: string) => void;
    options: { label: string; value: string }[];
    disabled?: boolean;
    "aria-label"?: string;
  }) => (
    <select
      value={props.value}
      disabled={props.disabled}
      aria-label={props["aria-label"]}
      onChange={(event) => props.onChange(event.target.value)}
    >
      {props.options.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  ),
}));

const getPdiStructureMock = vi.mocked(getPdiStructure);
const listPdiCompetenciesMock = vi.mocked(listPdiCompetencies);
const createPdiObjectiveMock = vi.mocked(createPdiObjective);
const createPdiPhaseMock = vi.mocked(createPdiPhase);
const createPdiCompetencyMock = vi.mocked(createPdiCompetency);
const validatePdiPhaseMock = vi.mocked(validatePdiPhase);
const listUsersMock = vi.mocked(listUsers);

const plan: Pdi = {
  userId: "9f1a1111-2222-3333-4444-555566667777",
  ownerName: "Carla",
  ownerEmail: "carla@exemplo.com",
  strategy: "Assumir o monitoramento de rede",
  targetDate: "2026-12-31",
  status: "Active",
  managerId: null,
  managerName: null,
  periodStart: null,
  periodEnd: null,
  updateDateTime: "2026-10-01T10:00:00Z",
};

/**
 * Estrutura com objetivo → estratégia → fase `PendingValidation`: é o menor
 * fixture que deixa os 3 níveis da árvore à vista (`+ Fase`, `Rejeitar`),
 * além do "Novo objetivo" e das "Competências" do topo.
 */
const estrutura: PdiStructure = {
  userId: plan.userId,
  objectives: [
    {
      id: "obj-1",
      title: "Reduzir incidentes de rede",
      description: null,
      competencyId: null,
      competencyName: null,
      currentSituation: null,
      desiredSituation: null,
      expectedResult: null,
      indicator: null,
      targetDate: null,
      order: 1,
      strategies: [
        {
          id: "str-1",
          title: "Monitorar enlaces",
          description: null,
          order: 1,
          phases: [
            {
              id: "ph-1",
              title: "Implantar monitoramento",
              description: null,
              responsibleId: null,
              responsibleName: null,
              startDate: null,
              endDate: null,
              completionCriteria: null,
              status: "PendingValidation",
              percent: 100,
              order: 1,
              activities: [],
            },
          ],
        },
      ],
    },
  ],
};

beforeEach(() => {
  vi.clearAllMocks();
  // `clearAllMocks` não limpa implementação — zera de propósito para um
  // `mockRejectedValue` de teste não vazar para o próximo.
  getPdiStructureMock.mockReset();
  listPdiCompetenciesMock.mockReset();
  createPdiObjectiveMock.mockReset();
  createPdiPhaseMock.mockReset();
  createPdiCompetencyMock.mockReset();
  validatePdiPhaseMock.mockReset();
  listUsersMock.mockReset();
  getPdiStructureMock.mockResolvedValue(estrutura);
  listPdiCompetenciesMock.mockResolvedValue([]);
  listUsersMock.mockResolvedValue([]);
});

function renderDialog() {
  return render(
    <PdiStructureDialog visible onHide={vi.fn()} plan={plan} isAdmin />,
  );
}

/** O `<form>` dentro de um dialog, pelo botão primário dele. */
function formOf(header: RegExp, submitLabel: string): HTMLElement {
  const dialog = screen.getByRole("dialog", { name: header });
  const wrapper = within(dialog)
    .getByRole("button", { name: submitLabel })
    .closest("form");
  if (!wrapper) throw new Error(`formulario ${String(header)} nao encontrado`);
  return wrapper;
}

/**
 * O `<label>` do `FormField` — é ali que a validação tem que acender.
 *
 * Casa pelo **texto inicial** do label e não por `getByLabelText`: quando o
 * erro acende, a mensagem entra DENTRO do mesmo `<label>` e o `textContent`
 * deixa de ser "Título", então o match exato do RTL quebra. Mesmo
 * procedimento da `PdiPage`, da `KnowledgePage` e do `PdiAccompanimentDialog`.
 */
function fieldIn(container: HTMLElement, labelText: string): HTMLElement {
  const labels = Array.from(container.querySelectorAll("label"));
  const found = labels.find((label) => (label.textContent ?? "").trim().startsWith(labelText));
  if (!found) throw new Error(`campo ${labelText} nao encontrado no container`);
  return found;
}

function controlIn(container: HTMLElement, labelText: string): HTMLElement {
  const control = fieldIn(container, labelText).querySelector("input, textarea, select");
  if (!control) throw new Error(`controle do campo ${labelText} nao encontrado`);
  return control as HTMLElement;
}

/** Dispara um `<input type="date">` — `user.type` não serve para data. */
function changeValue(control: HTMLElement, value: string) {
  fireEvent.change(control, { target: { value } });
}

describe("PdiStructureDialog — C-2: cada validação no campo dela", () => {
  it("should put the empty-title failure under Título, and only there", async () => {
    renderDialog();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: /Novo objetivo/ }));
    const form = formOf(/^Novo objetivo$/, "Salvar");

    fireEvent.submit(form);

    const mensagem = "O título é obrigatório.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(fieldIn(form, "Título")).getByText(mensagem)).toBeInTheDocument();
    // Uma única vez na tela inteira: o <FormError> não repete o do campo.
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(createPdiObjectiveMock).not.toHaveBeenCalled();
  });

  it("should put the empty-name failure under Nova competência in the catalog", async () => {
    renderDialog();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Competências" }));
    const dialog = await screen.findByRole("dialog", { name: "Competências do catálogo" });
    const form = within(dialog)
      .getByRole("button", { name: "Salvar" })
      .closest("form") as HTMLElement;

    // `submitCatalog` começa com `if (!catalogForm) return;` e o estado nasce
    // `null`: com o diálogo recém-aberto, "Salvar" não chega na validação
    // (silencioso — registrado como achado). A validação só é alcançável
    // depois que o usuário toca num campo — digita e apaga o nome.
    await user.type(within(fieldIn(form, "Nova competência")).getByRole("textbox"), "Liderança");
    await user.clear(within(fieldIn(form, "Nova competência")).getByRole("textbox"));

    fireEvent.submit(form);

    // O label é dinâmico ("Nova competência" na criação, "Editar competência"
    // na edição) — é ele que o erro precisa achar, não um texto fixo.
    const mensagem = "O nome da competência é obrigatório.";
    expect(await within(dialog).findByText(mensagem)).toBeInTheDocument();
    expect(within(fieldIn(form, "Nova competência")).getByText(mensagem)).toBeInTheDocument();
    expect(within(dialog).getAllByText(mensagem)).toHaveLength(1);
    expect(createPdiCompetencyMock).not.toHaveBeenCalled();
  });

  it("should keep the inverted-period failure at form level, away from Início and Fim", async () => {
    renderDialog();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "+ Fase" }));
    const form = formOf(/^Nova fase$/, "Salvar");

    await user.type(within(fieldIn(form, "Título")).getByRole("textbox"), "Implantar monitoramento");
    changeValue(controlIn(form, "Início"), "2026-10-20");
    changeValue(controlIn(form, "Fim"), "2026-10-10");

    fireEvent.submit(form);

    const mensagem = "O fim da fase não pode ser anterior ao início.";
    expect(await within(form).findByText(mensagem)).toBeInTheDocument();
    expect(within(form).getAllByText(mensagem)).toHaveLength(1);
    // Descreve DOIS campos — pendurar em um só apontaria metade do problema.
    expect(within(fieldIn(form, "Início")).queryByText(/não pode ser anterior/)).toBeNull();
    expect(within(fieldIn(form, "Fim")).queryByText(/não pode ser anterior/)).toBeNull();
    expect(within(fieldIn(form, "Título")).queryByText(/não pode ser anterior/)).toBeNull();
    expect(createPdiPhaseMock).not.toHaveBeenCalled();
  });

  it("should put the out-of-range failure under Percentual (%), without blaming Título", async () => {
    renderDialog();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "+ Fase" }));
    const form = formOf(/^Nova fase$/, "Salvar");

    await user.type(within(fieldIn(form, "Título")).getByRole("textbox"), "Implantar monitoramento");
    changeValue(controlIn(form, "Percentual (%)"), "150");

    fireEvent.submit(form);

    const mensagem = "O percentual deve estar entre 0 e 100.";
    expect(await within(form).findByText(mensagem)).toBeInTheDocument();
    expect(within(fieldIn(form, "Percentual (%)")).getByText(mensagem)).toBeInTheDocument();
    expect(within(fieldIn(form, "Título")).queryByText(/percentual/)).toBeNull();
    expect(within(form).getAllByText(mensagem)).toHaveLength(1);
    expect(createPdiPhaseMock).not.toHaveBeenCalled();
  });

  it("should put the empty-reason failure under Motivo da rejeição", async () => {
    renderDialog();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Rejeitar" }));
    const dialog = await screen.findByRole("dialog", { name: /^Rejeitar fase/ });
    const form = within(dialog)
      .getByRole("button", { name: "Rejeitar" })
      .closest("form") as HTMLElement;

    fireEvent.submit(form);

    const mensagem = "O motivo da rejeição é obrigatório.";
    expect(await within(dialog).findByText(mensagem)).toBeInTheDocument();
    expect(within(fieldIn(form, "Motivo da rejeição")).getByText(mensagem)).toBeInTheDocument();
    expect(within(dialog).getAllByText(mensagem)).toHaveLength(1);
    expect(validatePdiPhaseMock).not.toHaveBeenCalled();
  });

  it("should show a server refusal at form level, away from Título", async () => {
    createPdiObjectiveMock.mockRejectedValue(new Error("Você já tem um objetivo com esse título."));
    renderDialog();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: /Novo objetivo/ }));
    const form = formOf(/^Novo objetivo$/, "Salvar");

    await user.type(within(fieldIn(form, "Título")).getByRole("textbox"), "Reduzir incidentes");
    fireEvent.submit(form);

    const mensagem = "Você já tem um objetivo com esse título.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(within(fieldIn(form, "Título")).queryByText(/já tem um objetivo/)).toBeNull();
    expect(createPdiObjectiveMock).toHaveBeenCalledTimes(1);
  });
});
