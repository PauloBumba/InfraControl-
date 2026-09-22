import { lazy, Suspense } from "react";
import { Routes, Route } from "react-router-dom";

const DashboardPage = lazy(() => import("@/pages/DashboardPage").then(m => ({ default: m.DashboardPage })));
const NetworkPage = lazy(() => import("@/pages/NetworkPage").then(m => ({ default: m.NetworkPage })));
const WiFiPage = lazy(() => import("@/pages/WiFiPage").then(m => ({ default: m.WiFiPage })));
const TopologyPage = lazy(() => import("@/pages/TopologyPage").then(m => ({ default: m.TopologyPage })));
const DevicesPage = lazy(() => import("@/pages/DevicesPage").then(m => ({ default: m.DevicesPage })));
const ClientsPage = lazy(() => import("@/pages/ClientsPage").then(m => ({ default: m.ClientsPage })));
const InsightsPage = lazy(() => import("@/pages/InsightsPage").then(m => ({ default: m.InsightsPage })));
const AlertsPage = lazy(() => import("@/pages/AlertsPage").then(m => ({ default: m.AlertsPage })));
const SettingsPage = lazy(() => import("@/pages/SettingsPage").then(m => ({ default: m.SettingsPage })));
const HealthPage = lazy(() => import("@/pages/HealthPage").then(m => ({ default: m.HealthPage })));
const LogsPage = lazy(() => import("@/pages/LogsPage").then(m => ({ default: m.LogsPage })));
const NotFoundPage = lazy(() => import("@/pages/NotFoundPage").then(m => ({ default: m.NotFoundPage })));

function PageLoader() {
  return (
    <div className="page-content" style={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: "400px" }}>
      <div style={{ textAlign: "center" }}>
        <i className="pi pi-spin pi-spinner" style={{ fontSize: "24px", color: "var(--accent)" }} />
        <p style={{ color: "var(--text-muted)", marginTop: "var(--space-3)" }}>Carregando...</p>
      </div>
    </div>
  );
}

export function AppRouter() {
  return (
    <Suspense fallback={<PageLoader />}>
      <Routes>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/network" element={<NetworkPage />} />
        <Route path="/wifi" element={<WiFiPage />} />
        <Route path="/topology" element={<TopologyPage />} />
        <Route path="/devices" element={<DevicesPage />} />
        <Route path="/clients" element={<ClientsPage />} />
        <Route path="/insights" element={<InsightsPage />} />
        <Route path="/alerts" element={<AlertsPage />} />
        <Route path="/settings" element={<SettingsPage />} />
        <Route path="/health" element={<HealthPage />} />
        <Route path="/logs" element={<LogsPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </Suspense>
  );
}
