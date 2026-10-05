/**
 * Casca comum para tabelas de dados UniFi. Centraliza o painel, tamanho e
 * largura do PrimeReact; cada página fornece apenas colunas e comportamento
 * específico (paginação lazy, ações e templates de célula).
 */
import { DataTable, type DataTableProps, type DataTableValueArray } from "primereact/datatable";
import type { ReactNode } from "react";

export function DataTablePanel<TValue extends DataTableValueArray>({
  children,
  tableStyle,
  panel = true,
  ...props
}: DataTableProps<TValue> & { children: ReactNode; panel?: boolean }) {
  const table = (
    <DataTable
        {...props}
        size={props.size ?? "small"}
        tableStyle={{ width: "100%", ...tableStyle }}
      >
        {children}
    </DataTable>
  );

  return panel ? <div className="uni-panel">{table}</div> : table;
}
