import { createContext, useContext, useState, type ReactNode } from "react";

export type MenuMode = "static" | "overlay";

export interface LayoutConfig {
  menuMode: MenuMode;
  inputStyle: "outlined" | "filled";
  ripple: boolean;
  darkMode: boolean;
}

export interface LayoutState {
  overlayMenuActive: boolean;
  staticMenuMobileActive: boolean;
  staticMenuDesktopInactive: boolean;
  profileSidebarVisible: boolean;
  configSidebarVisible: boolean;
}

interface LayoutContextValue {
  layoutConfig: LayoutConfig;
  layoutState: LayoutState;
  setLayoutConfig: (config: Partial<LayoutConfig>) => void;
  setLayoutState: (state: Partial<LayoutState> | ((prev: LayoutState) => LayoutState)) => void;
}

const LayoutContext = createContext<LayoutContextValue | undefined>(undefined);

export function LayoutProvider({ children }: { children: ReactNode }) {
  const [layoutConfig, setLayoutConfig] = useState<LayoutConfig>({
    menuMode: "static",
    inputStyle: "outlined",
    ripple: false,
    darkMode: false,
  });

  const [layoutState, setLayoutState] = useState<LayoutState>({
    overlayMenuActive: false,
    staticMenuMobileActive: false,
    staticMenuDesktopInactive: false,
    profileSidebarVisible: false,
    configSidebarVisible: false,
  });

  const handleSetLayoutConfig = (config: Partial<LayoutConfig>) => {
    setLayoutConfig((prev) => ({ ...prev, ...config }));
  };

  const handleSetLayoutState = (
    state: Partial<LayoutState> | ((prev: LayoutState) => LayoutState),
  ) => {
    setLayoutState((prev) => {
      if (typeof state === "function") {
        return state(prev);
      }
      return { ...prev, ...state };
    });
  };

  return (
    <LayoutContext.Provider
      value={{
        layoutConfig,
        layoutState,
        setLayoutConfig: handleSetLayoutConfig,
        setLayoutState: handleSetLayoutState,
      }}
    >
      {children}
    </LayoutContext.Provider>
  );
}

export function useLayoutContext(): LayoutContextValue {
  const context = useContext(LayoutContext);
  if (!context) {
    throw new Error("useLayoutContext must be used within LayoutProvider");
  }
  return context;
}
