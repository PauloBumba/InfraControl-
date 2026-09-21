import { ActivityPanel, AlertsPanel, AvailabilityPanel, MetricsGrid, SystemsPanel } from "@/components/dashboard/DashboardSections";

export function DashboardPage() {
  return <div className="page-content">
    <section className="page-heading"><div><p className="eyebrow">SEGUNDA-FEIRA, 21 DE SETEMBRO DE 2026</p><h1>Bom dia, Paulo <span>👋</span></h1><p className="heading-subtitle">Aqui está o resumo da sua infraestrutura hoje.</p></div><button className="primary-button"><i className="pi pi-plus" /> Nova tarefa</button></section>
    <MetricsGrid />
    <section className="content-grid"><AvailabilityPanel /><SystemsPanel /></section>
    <section className="bottom-grid"><AlertsPanel /><ActivityPanel /></section>
  </div>;
}
