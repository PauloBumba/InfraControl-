import { createSlice, PayloadAction } from "@reduxjs/toolkit";

interface NavigationState {
  activeItem: string;
  recentTabs: string[];
}

const RECENT_TABS_KEY = "recentTabs";
const MAX_RECENT_TABS = 5;

function getInitialRecentTabs(): string[] {
  try {
    const saved = localStorage.getItem(RECENT_TABS_KEY);
    return saved ? JSON.parse(saved) : [];
  } catch {
    return [];
  }
}

const initialState: NavigationState = {
  activeItem: "Visão geral",
  recentTabs: getInitialRecentTabs(),
};

const navigationSlice = createSlice({
  name: "navigation",
  initialState,
  reducers: {
    setActiveItem(state, action: PayloadAction<string>) {
      state.activeItem = action.payload;
    },
    addToRecentTabs(state, action: PayloadAction<string>) {
      const item = action.payload;
      state.recentTabs = [item, ...state.recentTabs.filter((t) => t !== item)].slice(0, MAX_RECENT_TABS);
      localStorage.setItem(RECENT_TABS_KEY, JSON.stringify(state.recentTabs));
    },
    removeTab(state, action: PayloadAction<string>) {
      state.recentTabs = state.recentTabs.filter((t) => t !== action.payload);
      localStorage.setItem(RECENT_TABS_KEY, JSON.stringify(state.recentTabs));
    },
  },
});

export const { setActiveItem, addToRecentTabs, removeTab } = navigationSlice.actions;
export const navigationReducer = navigationSlice.reducer;
