import { beforeEach, describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { Provider } from "react-redux";
import { PublicMarketingRoute } from "./PublicMarketingRoute";
import { store } from "@/app/store";
import { sessionCheckFailed, sessionChecked } from "@/app/store/slices/authSlice";

/**
 * F26-T002 — "qualquer usuário logado que acesse [a landing] é redirecionado
 * ao dashboard (padrão /login)".
 *
 * O guard fica isolado do router de verdade porque o `AppRouter` importa
 * todas as páginas (lazy) — testá-lo aqui é rápido e cobre a regra.
 */
function renderRoute(authenticated: boolean) {
  if (authenticated) {
    store.dispatch(
      sessionChecked({
        id: "3f1a1111-2222-3333-4444-555566667777",
        email: "ana@exemplo.com",
        displayName: "Ana",
        roles: ["Viewer"],
      }),
    );
  } else {
    store.dispatch(sessionCheckFailed("sem sessão"));
  }

  return render(
    <Provider store={store}>
      <MemoryRouter initialEntries={["/home"]}>
        <Routes>
          <Route path="/" element={<div>dashboard</div>} />
          <Route
            path="/home"
            element={
              <PublicMarketingRoute>
                <div>landing</div>
              </PublicMarketingRoute>
            }
          />
        </Routes>
      </MemoryRouter>
    </Provider>,
  );
}

beforeEach(() => {
  localStorage.clear();
});

describe("PublicMarketingRoute (F26-T002)", () => {
  it("should redirect an authenticated user to the dashboard", () => {
    renderRoute(true);

    expect(screen.getByText("dashboard")).toBeInTheDocument();
    expect(screen.queryByText("landing")).toBeNull();
  });

  it("should render the landing for an anonymous visitor", () => {
    renderRoute(false);

    expect(screen.getByText("landing")).toBeInTheDocument();
    expect(screen.queryByText("dashboard")).toBeNull();
  });
});
