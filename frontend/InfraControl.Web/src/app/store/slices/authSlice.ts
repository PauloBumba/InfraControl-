import { createSlice, type PayloadAction } from "@reduxjs/toolkit";

export interface AuthUser {
  id: string;
  email: string;
  displayName: string;
  /**
   * F38 — nome completo (`User.FullName`, opcional). Vem do `/auth/me` como
   * claim `fullName`; opcional porque (1) o campo é nullable e (2) o cache
   * persistido de sessões anteriores a este deploy não o tem — a UI trata a
   * ausência como "em branco", nunca como erro.
   */
  fullName?: string | null;
  /** Roles canônicas (ADR-016): "Admin" | "Operator" | "Viewer". */
  roles: string[];
}

/**
 * `checking`     → ainda não sei (boot: revalidando com o backend)
 * `authenticated`→ o backend confirmou a sessão no último /auth/me ou login
 * `anonymous`    → sem sessão válida
 * `unavailable`  → o backend não respondeu (rede/500): NÃO é logout
 */
export type AuthStatus = "checking" | "authenticated" | "anonymous" | "unavailable";

export interface AuthState {
  status: AuthStatus;
  /**
   * Cache de EXIBIÇÃO (nome, e-mail, roles). Persistido apenas
   * para não piscar a tela no F5 — nunca como fonte de autorização EFETIVA:
   * quem autoriza é o backend (cookie HttpOnly + policies); o F14-T009 usa
   * as roles só para conformidade da UI (esconder menu/rota/botão). Nenhum
   * token, refresh token ou credencial entra neste estado.
   */
  user: AuthUser | null;
  /** Estado transitório: nunca persistido (some no F5). */
  pending: boolean;
  error: string | null;
}

const initialState: AuthState = {
  status: "checking",
  user: null,
  pending: false,
  error: null,
};

const authSlice = createSlice({
  name: "auth",
  initialState,
  reducers: {
    /** Boot da aplicação: o backend é a única fonte da verdade. */
    sessionCheckStarted(state) {
      state.status = "checking";
      state.error = null;
    },
    sessionChecked(state, action: PayloadAction<AuthUser | null>) {
      state.status = action.payload ? "authenticated" : "anonymous";
      state.pending = false;
      state.error = null;
      if (action.payload) {
        state.user = action.payload;
      } else {
        state.user = null;
      }
    },
    sessionCheckFailed(state, action: PayloadAction<string>) {
      state.status = "unavailable";
      state.pending = false;
      state.error = action.payload;
    },
    loginStarted(state) {
      state.pending = true;
      state.error = null;
    },
    loginSucceeded(state, action: PayloadAction<AuthUser>) {
      state.status = "authenticated";
      state.user = action.payload;
      state.pending = false;
      state.error = null;
    },
    loginFailed(state, action: PayloadAction<string>) {
      state.status = "anonymous";
      state.user = null;
      state.pending = false;
      state.error = action.payload;
    },
    signedOut(state) {
      state.status = "anonymous";
      state.user = null;
      state.pending = false;
      state.error = null;
    },
    clearAuthError(state) {
      state.error = null;
    },
  },
});

export const {
  sessionCheckStarted,
  sessionChecked,
  sessionCheckFailed,
  loginStarted,
  loginSucceeded,
  loginFailed,
  signedOut,
  clearAuthError,
} = authSlice.actions;

export const authReducer = authSlice.reducer;
