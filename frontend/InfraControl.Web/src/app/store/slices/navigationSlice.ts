import { createSlice, PayloadAction } from "@reduxjs/toolkit";
import { resolveStoredRoute } from "@/app/router/routeLabels";

interface NavigationState {
  /**
   * Abas recentes — guardam a **ROTA**, nunca o rótulo (F29-T005). Rótulo é
   * apresentação e existe em duplicidade no menu; rota é identidade única e
   * é o que a aba precisa para navegar.
   */
  recentTabs: string[];
}

const RECENT_TABS_KEY = "recentTabs";
const MAX_RECENT_TABS = 5;

function getInitialRecentTabs(): string[] {
  try {
    const saved = localStorage.getItem(RECENT_TABS_KEY);
    if (!saved) return [];
    const parsed: unknown = JSON.parse(saved);
    if (!Array.isArray(parsed)) return [];
    // Migração silenciosa do formato antigo (rótulo) e descarte do que não
    // resolve mais — as abas voltam a ser preenchidas conforme o navega.
    return parsed
      .filter((entry): entry is string => typeof entry === "string")
      .map(resolveStoredRoute)
      .filter((route): route is string => route !== null)
      .slice(0, MAX_RECENT_TABS);
  } catch {
    return [];
  }
}

function persist(tabs: string[]) {
  localStorage.setItem(RECENT_TABS_KEY, JSON.stringify(tabs));
}

const initialState: NavigationState = {
  recentTabs: getInitialRecentTabs(),
};

const navigationSlice = createSlice({
  name: "navigation",
  initialState,
  reducers: {
    addToRecentTabs(state, action: PayloadAction<string>) {
      const route = action.payload;
      state.recentTabs = [route, ...state.recentTabs.filter((t) => t !== route)].slice(
        0,
        MAX_RECENT_TABS,
      );
      persist(state.recentTabs);
    },
    removeTab(state, action: PayloadAction<string>) {
      state.recentTabs = state.recentTabs.filter((t) => t !== action.payload);
      persist(state.recentTabs);
    },
  },
});

export const { addToRecentTabs, removeTab } = navigationSlice.actions;
export const navigationReducer = navigationSlice.reducer;
