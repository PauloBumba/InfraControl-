import { DataTable } from "primereact/datatable";
import { Column } from "primereact/column";
import { Tag } from "primereact/tag";

interface Client {
  id: string;
  name: string;
  mac: string;
  ip: string;
  device: string;
  usage: string;
  signal: "strong" | "medium" | "weak";
}

const clients: Client[] = [
  { id: "1", name: "iPhone 15 Pro", mac: "AA:BB:CC:DD:EE:01", ip: "192.168.1.101", device: "UDM Pro Max", usage: "2.4 GB", signal: "strong" },
  { id: "2", name: "MacBook Pro", mac: "AA:BB:CC:DD:EE:02", ip: "192.168.1.102", device: "UDM Pro Max", usage: "5.1 GB", signal: "strong" },
  { id: "3", name: "Samsung TV", mac: "AA:BB:CC:DD:EE:03", ip: "192.168.1.103", device: "AP-AC-PRO", usage: "8.7 GB", signal: "medium" },
  { id: "4", name: "PlayStation 5", mac: "AA:BB:CC:DD:EE:04", ip: "192.168.1.104", device: "UDM Pro Max", usage: "12.3 GB", signal: "strong" },
  { id: "5", name: "Work Laptop", mac: "AA:BB:CC:DD:EE:05", ip: "192.168.1.105", device: "US-24-250W", usage: "1.8 GB", signal: "strong" },
  { id: "6", name: "iPad Air", mac: "AA:BB:CC:DD:EE:06", ip: "192.168.1.106", device: "AP-AC-PRO", usage: "0.9 GB", signal: "medium" },
  { id: "7", name: "Smart Thermostat", mac: "AA:BB:CC:DD:EE:07", ip: "192.168.1.107", device: "AP-AC-LR", usage: "0.1 GB", signal: "weak" },
  { id: "8", name: "Ring Camera", mac: "AA:BB:CC:DD:EE:08", ip: "192.168.1.108", device: "AP-AC-PRO", usage: "15.2 GB", signal: "strong" },
];

function signalBody(rowData: Client) {
  const severityMap = { strong: "success", medium: "warning", weak: "danger" } as const;
  const labelMap = { strong: "Forte", medium: "Médio", weak: "Fraco" };
  return <Tag value={labelMap[rowData.signal]} severity={severityMap[rowData.signal]} />;
}

export function TopClients() {
  return (
    <article className="panel">
      <div className="panel-heading">
        <div>
          <h2>Top Clients</h2>
          <p>Higher bandwidth consumers</p>
        </div>
        <button className="text-button">
          View All <i className="pi pi-arrow-right" />
        </button>
      </div>
      <DataTable
        value={clients}
        rows={5}
        rowsPerPageOptions={[5, 10]}
        paginator
        size="small"
        stripedRows
        tableStyle={{ minWidth: "100%" }}
      >
        <Column field="name" header="Device" />
        <Column field="ip" header="IP" />
        <Column field="device" header="Access Point" />
        <Column field="usage" header="Usage" />
        <Column body={signalBody} header="Signal" />
      </DataTable>
    </article>
  );
}
