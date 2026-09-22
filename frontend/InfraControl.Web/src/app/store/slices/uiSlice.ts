import { createSlice, PayloadAction } from "@reduxjs/toolkit";

interface UiState {
  sidebarCollapsed: boolean;
  mobileOpen: boolean;
}

const SIDEBAR_STATE_KEY = "sidebarCollapsed";

function getInitialSidebarState(): boolean {
  try {
    return localStorage.getItem(SIDEBAR_STATE_KEY) === "true";
  } catch {
    return false;
  }
}

const initialState: UiState = {
  sidebarCollapsed: getInitialSidebarState(),
  mobileOpen: false,
};

const uiSlice = createSlice({
  name: "ui",
  initialState,
  reducers: {
    toggleSidebar(state) {
      state.sidebarCollapsed = !state.sidebarCollapsed;
      localStorage.setItem(SIDEBAR_STATE_KEY, String(state.sidebarCollapsed));
    },
    setSidebarCollapsed(state, action: PayloadAction<boolean>) {
      state.sidebarCollapsed = action.payload;
      localStorage.setItem(SIDEBAR_STATE_KEY, String(state.sidebarCollapsed));
    },
    setMobileOpen(state, action: PayloadAction<boolean>) {
      state.mobileOpen = action.payload;
    },
  },
});

export const { toggleSidebar, setSidebarCollapsed, setMobileOpen } = uiSlice.actions;
export const uiReducer = uiSlice.reducer;
