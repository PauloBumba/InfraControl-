import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PdiAccompanimentDialog } from "./PdiAccompanimentDialog";
import {
  createPdiImpediment,
  createPdiProgress,
  getPdiAccompaniment,
  type Pdi,
  type PdiPhaseNode,
} from "@/services/api/pdi";

/**
 * F29-T001C-2 — PdiAccompanimentDialog: o maior site do recorte e o de maior
 * densidade de validação — **15 `setFormError` espalhados por 6 modos de
 * form** (`progress`, `checkin`, `impediment`, `feedback`, `evidence`,
 * `review`), todos caindo num único `<FormError>` no pé, sem dizer qual campo.
 *
 * Por que este dialog é o caso de maior risco: os modos **reusam os mesmos
 * nomes de estado** — `form.date` serve para check-in, impedimento E feedback;
 * `form.type` para impedimento, feedback E evidência; `form.description` para
 * impedimento E revisão. Chaves de erro erradas apontariam para o campo de
 * outro modo.
 *
 * O que este teste tranca (um caso por propriedade):
 *
 * 1. `progress`/comentário → campo "Comentário / relato", sem culpar o
 *    "Percentual", uma única vez na tela;
 * 2. `progress`/percentual → **o segundo campo do MESMO form**: o erro tem que
 *    andar de campo, não grudar no primeiro;
 * 3. `impediment` → preencher o tipo faz o erro **pular** para "Descrição do
 *    problema" e o do tipo some;
 * 4. recusa do servidor → no formulário, longe dos campos.
 *
 * Por que as chaves simples (`date`, `type`, `description`) não colidem: os
 * blocos de render são mutuamente exclusivos (`{form.kind === "X" && …}`),
 * `{form && (…)}` esconde a lista enquanto o form está aberto e `kind` só é
 * definido nos `open*Form`, que também zeram o erro.
 */
vi.mock("@/services/api/pdi", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/services/api/pdi")>();
  return {
    ...actual,
    getPdiAccompaniment: vi.fn(),
    createPdiProgress: vi.fn(),
    createPdiImpediment: vi.fn(),
  };
});

/**
 * `SelectField` é wrapper do `Dropdown` do PrimeReact — não abre no jsdom.
 * Mesmo precedente de `KnowledgePage.test.tsx`/`DevicesPage`/`PdiPage`.
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

const getPdiAccompanimentMock = vi.mocked(getPdiAccompaniment);
const createPdiProgressMock = vi.mocked(createPdiProgress);
const createPdiImpedimentMock = vi.mocked(createPdiImpediment);

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

const phase: PdiPhaseNode = {
  id: "ph-1",
  title: "Implantação do switch",
  description: null,
  responsibleId: null,
  responsibleName: null,
  startDate: null,
  endDate: null,
  completionCriteria: null,
  status: "NotStarted" as PdiPhaseNode["status"],
  percent: 0,
  order: 1,
  activities: [],
};

beforeEach(() => {
  vi.clearAllMocks();
  // `clearAllMocks` não limpa implementação — zera de propósito para um
  // `mockRejectedValue` de teste não vazar para o próximo.
  createPdiProgressMock.mockReset();
  createPdiImpedimentMock.mockReset();
  getPdiAccompanimentMock.mockResolvedValue({
    userId: plan.userId,
    progresses: [],
    checkIns: [],
    impediments: [],
    feedbacks: [],
    evidences: [],
  });
});

function renderDialog() {
  return render(
    <PdiAccompanimentDialog
      visible
      onHide={vi.fn()}
      plan={plan}
      phase={phase}
      isAdmin
      currentUserId="u-1"
    />,
  );
}

/** Abre a aba e dispara o botão de criação dela. */
async function openForm(
  user: ReturnType<typeof userEvent.setup>,
  tabLabel: string,
  createLabel: string,
) {
  await user.click(await screen.findByRole("button", { name: tabLabel }));
  await user.click(await screen.findByRole("button", { name: createLabel }));
}

/** O `<form>` do dialog de edição, pelo botão primário "Salvar". */
function formOf(header: string): HTMLElement {
  const dialog = screen.getByRole("dialog", { name: header });
  const wrapper = within(dialog)
    .getByRole("button", { name: "Salvar" })
    .closest("form");
  if (!wrapper) throw new Error(`formulario ${header} nao encontrado`);
  return wrapper;
}

/**
 * O `<label>` do `FormField` — é ali que a validação tem que acender.
 *
 * Casa pelo **texto inicial** do label e não por `getByLabelText`: quando o
 * erro acende, a mensagem entra DENTRO do mesmo `<label>` e o `textContent`
 * deixa de ser "Comentário / relato", então o match exato do RTL quebra.
 * Mesmo procedimento da `PdiPage` e da `KnowledgePage`.
 */
function fieldIn(container: HTMLElement, labelText: string): HTMLElement {
  const labels = Array.from(container.querySelectorAll("label"));
  const found = labels.find((label) => (label.textContent ?? "").trim().startsWith(labelText));
  if (!found) throw new Error(`campo ${labelText} nao encontrado no container`);
  return found;
}

function controlOf(container: HTMLElement, labelText: string): HTMLElement {
  const control = fieldIn(container, labelText).querySelector("input, textarea, select");
  if (!control) throw new Error(`controle do campo ${labelText} nao encontrado`);
  return control as HTMLElement;
}

describe("PdiAccompanimentDialog — C-2: cada validação no campo dela", () => {
  it("should put the missing-comment failure under Comentário / relato, not under Percentual", async () => {
    renderDialog();
    const user = userEvent.setup();
    await openForm(user, "Progresso (0)", "+ Progresso");
    const form = formOf("Novo progresso");

    fireEvent.submit(form);

    const mensagem = "O comentário do progresso é obrigatório.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(fieldIn(form, "Comentário / relato")).getByText(mensagem)).toBeInTheDocument();
    expect(within(fieldIn(form, "Percentual da fase (%)")).queryByText(/comentário/)).toBeNull();
    // Uma única vez na tela inteira: o <FormError> não repete o do campo.
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(createPdiProgressMock).not.toHaveBeenCalled();
  });

  it("should move the second failure of the same form to Percentual, without blaming Comentário", async () => {
    renderDialog();
    const user = userEvent.setup();
    await openForm(user, "Progresso (0)", "+ Progresso");
    const form = formOf("Novo progresso");

    await user.type(within(form).getByRole("textbox"), "Concluiu a montagem.");
    fireEvent.change(controlOf(form, "Percentual da fase (%)"), {
      target: { value: "150" },
    });

    fireEvent.submit(form);

    const mensagem = "O percentual deve estar entre 0 e 100.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(fieldIn(form, "Percentual da fase (%)")).getByText(mensagem)).toBeInTheDocument();
    // O erro é do percentual: o comentário, já válido, não pode acender.
    expect(within(fieldIn(form, "Comentário / relato")).queryByText(/percentual/)).toBeNull();
    expect(screen.getAllByText(mensagem)).toHaveLength(1);
    expect(createPdiProgressMock).not.toHaveBeenCalled();
  });

  it("should move the impediment failure from Tipo to Descrição as each gets filled", async () => {
    renderDialog();
    const user = userEvent.setup();
    await openForm(user, "Impedimentos (0)", "+ Impedimento");
    const form = formOf("Novo impedimento");

    // A de data passa: o campo já vem com a data de hoje.
    fireEvent.submit(form);
    const tipoMensagem = "O tipo do impedimento é obrigatório.";
    expect(await within(form).findByText(tipoMensagem)).toBeInTheDocument();
    const tipoField = fieldIn(form, "Tipo (ex.: recurso indisponível)");
    expect(within(tipoField).getByText(tipoMensagem)).toBeInTheDocument();

    // Preenche o tipo e reenvia: o erro TEM que andar para a descrição…
    await user.type(within(tipoField).getByRole("textbox"), "recurso indisponível");
    fireEvent.submit(form);

    const descricaoMensagem = "A descrição do impedimento é obrigatória.";
    expect(await within(form).findByText(descricaoMensagem)).toBeInTheDocument();
    expect(
      within(fieldIn(form, "Descrição do problema")).getByText(descricaoMensagem),
    ).toBeInTheDocument();
    // …e o do tipo some junto com o campo corrigido.
    expect(within(tipoField).queryByText(tipoMensagem)).toBeNull();
    expect(within(form).getAllByText(descricaoMensagem)).toHaveLength(1);
    expect(createPdiImpedimentMock).not.toHaveBeenCalled();
  });

  it("should show a server refusal at form level, away from the fields", async () => {
    createPdiProgressMock.mockRejectedValue(new Error("Fase já concluída."));
    renderDialog();
    const user = userEvent.setup();
    await openForm(user, "Progresso (0)", "+ Progresso");
    const form = formOf("Novo progresso");

    await user.type(within(form).getByRole("textbox"), "Concluiu a montagem.");
    fireEvent.submit(form);

    const mensagem = "Fase já concluída.";
    expect(await screen.findByText(mensagem)).toBeInTheDocument();
    expect(within(form).getAllByText(mensagem)).toHaveLength(1);
    expect(within(fieldIn(form, "Comentário / relato")).queryByText(/Fase já concluída/)).toBeNull();
    expect(within(fieldIn(form, "Percentual da fase (%)")).queryByText(/Fase já concluída/)).toBeNull();
    expect(createPdiProgressMock).toHaveBeenCalledTimes(1);
  });
});
