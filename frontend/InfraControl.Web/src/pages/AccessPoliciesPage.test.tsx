import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AccessPoliciesPage } from "./AccessPoliciesPage";
import {
  getAccessPolicyMatrix,
  updateAccessPolicyGrants,
  type AccessPolicyMatrix,
} from "@/services/api/accessPolicies";

/**
 * `SelectField` é wrapper do `Dropdown` do PrimeReact — painel em portal,
 * não abre no jsdom (mesmo molde Devices/Knowledge/Pdi).
 */
vi.mock("@/components/ui/SelectField", () => ({
  SelectField: (props: {
    value: string;
    onChange: (v: string) => void;
    options: { label: string; value: string }[];
    placeholder?: string;
    disabled?: boolean;
    "aria-label"?: string;
  }) => (
    <select
      aria-label={props["aria-label"]}
      value={props.value}
      disabled={props.disabled}
      onChange={(e) => props.onChange(e.target.value)}
    >
      {props.options.map((o) => (
        <option key={o.value} value={o.value}>
          {o.label}
        </option>
      ))}
    </select>
  ),
}));

vi.mock("@/services/api/accessPolicies", () => ({
  getAccessPolicyMatrix: vi.fn(),
  updateAccessPolicyGrants: vi.fn(),
}));

const getMock = vi.mocked(getAccessPolicyMatrix);
const putMock = vi.mocked(updateAccessPolicyGrants);

const MATRIX: AccessPolicyMatrix = {
  modules: [
    {
      key: "inventory",
      label: "Inventário",
      roles: [
        { role: "Viewer", level: 1, isOverride: false },
        { role: "Operator", level: 2, isOverride: false },
        { role: "Admin", level: 3, isOverride: false },
      ],
    },
  ],
  customRoles: [],
};

function renderPage() {
  return render(<AccessPoliciesPage />);
}

/**
 * F50 layout — políticas de acesso no padrão da casa (PageHeader +
 * uni-panel + banners/LoadingBlock ui/), sem Card/Message/ProgressSpinner
 * solto nem raiz Tailwind.
 */
describe("AccessPoliciesPage — padrão da casa", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getMock.mockResolvedValue(MATRIX);
    putMock.mockImplementation(async (grants) => ({
      ...MATRIX,
      modules: MATRIX.modules.map((m) => ({
        ...m,
        roles: m.roles.map((r) => {
          const cell = grants.find((g) => g.moduleKey === m.key && g.role === r.role);
          return cell
            ? { ...r, level: cell.level ?? r.level, isOverride: cell.level != null }
            : r;
        }),
      })),
    }));
  });

  it("should render PageHeader with the house title", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "Políticas de acesso" }),
    ).toBeInTheDocument();
    expect(getMock).toHaveBeenCalledTimes(1);
  });

  it("should not use raw ProgressSpinner for loading", () => {
    getMock.mockReturnValue(new Promise(() => {}));
    const { container } = renderPage();

    expect(container.querySelector(".p-progress-spinner")).toBeNull();
    expect(screen.getByText("Carregando…")).toBeInTheDocument();
  });

  it("should save matrix changes via the API", async () => {
    renderPage();
    const user = userEvent.setup();

    await screen.findByRole("heading", { name: "Políticas de acesso" });
    const selects = await screen.findAllByRole("combobox");
    await user.selectOptions(selects[0]!, "0");

    await waitFor(() => expect(screen.getByRole("button", { name: /Salvar/ })).toBeEnabled());
    await user.click(screen.getByRole("button", { name: /Salvar/ }));

    await waitFor(() => expect(putMock).toHaveBeenCalledTimes(1));
    const grants = putMock.mock.calls[0]![0];
    expect(grants).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ moduleKey: "inventory", role: "Viewer", level: 0 }),
      ]),
    );
    expect(await screen.findByText("Políticas salvas.")).toBeInTheDocument();
  });
});
