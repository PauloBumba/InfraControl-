import { ActivityPanel, AlertsPanel, MetricsGrid, SystemsPanel } from "@/components/dashboard/DashboardSections";
import { ConnectivityDots } from "@/components/dashboard/ConnectivityDots";
import { GatewayCard } from "@/components/dashboard/GatewayCard";
import { TrafficChart } from "@/components/dashboard/TrafficChart";

export function DashboardPage() {
  return <div className="page-content">
    <section className="page-heading"><div><p className="eyebrow">SEGUNDA-FEIRA, 21 DE SETEMBRO DE 2026</p><h1>Bom dia, Paulo <span>👋</span></h1><p className="heading-subtitle">Aqui está o resumo da sua infraestrutura hoje.</p></div><button className="primary-button"><i className="pi pi-plus" /> Nova tarefa</button></section>
    <ConnectivityDots />
    <MetricsGrid />
    <TrafficChart />
    <section className="content-grid">
      <GatewayCard
        name="UDM Pro Max"
        model="Dream Machine Pro Max"
        ip="192.168.1.1"
        status="online"
        uptime="45d 12h 30m"
        cpu={23}
        memory={67}
        temperature={42}
        wan={{ up: 450, down: 920 }}
      />
      <SystemsPanel />
    </section>
    <section className="bottom-grid"><AlertsPanel /><ActivityPanel /></section>
  </div>;
}
