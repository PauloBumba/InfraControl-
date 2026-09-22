import { lazy, Suspense } from "react";
import { Routes, Route, useLocation, useNavigate } from "react-router-dom";
import { useAppDispatch, useAppSelector } from "@/app/store/hooks";
import { setActiveItem } from "@/app/store/slices/navigationSlice";
import { AppLayout } from "@/components/layout/AppLayout";
import { navigation, secondaryNavigation } from "@/data/dashboardData";

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
const RemotePage = lazy(() => import("@/pages/RemotePage").then(m => ({ default: m.RemotePage })));
const TasksPage = lazy(() => import("@/pages/TasksPage").then(m => ({ default: m.TasksPage })));
const KnowledgePage = lazy(() => import("@/pages/KnowledgePage").then(m => ({ default: m.KnowledgePage })));
const DeviceDetailPage = lazy(() => import("@/pages/DeviceDetailPage").then(m => ({ default: m.DeviceDetailPage })));

const allNav = [...navigation, ...secondaryNavigation];

const routeLabelMap: Record<string, string> = Object.fromEntries(
  allNav.map(item => [item.route, item.label])
);
routeLabelMap["/"] = "Visão Geral";

const labelRouteMap: Record<string, string> = Object.fromEntries(
  allNav.map(item => [item.label, item.route])
);

function PageLoader() {
  return (
    <div className="uni-page-loader">
      <i className="pi pi-spin pi-spinner" />
      <span>Carregando...</span>
    </div>
  );
}

function AppContent() {
  const location = useLocation();
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  const activeItemRedux = useAppSelector((s) => s.navigation.activeItem);

  const activeLabel = location.pathname.startsWith("/devices/")
    ? "Detalhes do Dispositivo"
    : routeLabelMap[location.pathname] || activeItemRedux || "Visão Geral";

  const handleNavigate = (label: string) => {
    const route = labelRouteMap[label] || "/";
    dispatch(setActiveItem(label));
    navigate(route);
  };

  return (
    <AppLayout activeItem={activeLabel} onNavigate={handleNavigate}>
      <Suspense fallback={<PageLoader />}>
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/network" element={<NetworkPage />} />
          <Route path="/wifi" element={<WiFiPage />} />
          <Route path="/topology" element={<TopologyPage />} />
          <Route path="/devices" element={<DevicesPage />} />
          <Route path="/devices/:id" element={<DeviceDetailPage />} />
          <Route path="/clients" element={<ClientsPage />} />
          <Route path="/insights" element={<InsightsPage />} />
          <Route path="/alerts" element={<AlertsPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/health" element={<HealthPage />} />
          <Route path="/logs" element={<LogsPage />} />
          <Route path="/remote" element={<RemotePage />} />
          <Route path="/tasks" element={<TasksPage />} />
          <Route path="/knowledge" element={<KnowledgePage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </Suspense>
    </AppLayout>
  );
}

export function AppRouter() {
  return <AppContent />;
}
