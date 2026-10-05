import type { ReactNode } from "react";
import { Toolbar } from "primereact/toolbar";

/** Barra compacta e responsiva para filtros e ações de listas. */
export function FilterBar({ children }: { children: ReactNode }) {
  return <Toolbar className="uni-filter-bar" start={children} />;
}
