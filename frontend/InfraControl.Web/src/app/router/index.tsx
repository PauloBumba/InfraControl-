import { lazy, Suspense } from "react";
import { Routes, Route, useLocation, useNavigate, Navigate } from "react-router-dom";
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
const UsersPage = lazy(() => import("@/pages/UsersPage").then(m => ({ default: m.UsersPage })));
const AuditPage = lazy(() => import("@/pages/AuditPage").then(m => ({ default: m.AuditPage })));
const IncidentsPage = lazy(() => import("@/pages/IncidentsPage").then(m => ({ default: m.IncidentsPage })));
const NotificationsPage = lazy(() => import("@/pages/NotificationsPage").then(m => ({ default: m.NotificationsPage })));
const LocationsPage = lazy(() => import("@/pages/LocationsPage").then(m => ({ default: m.LocationsPage })));
const SlaPage = lazy(() => import("@/pages/SlaPage").then(m => ({ default: m.SlaPage })));
const SwitchesPage = lazy(() => import("@/pages/SwitchesPage").then(m => ({ default: m.SwitchesPage })));
const LoginPage = lazy(() => import("@/pages/LoginPage").then(m => ({ default: m.LoginPage })));

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

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useAppSelector((state) => state.auth.isAuthenticated);

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
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

  // Login page não tem layout
  if (location.pathname === '/login') {
    return (
      <Suspense fallback={<PageLoader />}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
        </Routes>
      </Suspense>
    );
  }

  return (
    <AppLayout activeItem={activeLabel} onNavigate={handleNavigate}>
      <Suspense fallback={<PageLoader />}>
        <Routes>
          <Route path="/" element={
            <ProtectedRoute>
              <DashboardPage />
            </ProtectedRoute>
          } />
          <Route path="/network" element={
            <ProtectedRoute>
              <NetworkPage />
            </ProtectedRoute>
          } />
          <Route path="/wifi" element={
            <ProtectedRoute>
              <WiFiPage />
            </ProtectedRoute>
          } />
          <Route path="/topology" element={
            <ProtectedRoute>
              <TopologyPage />
            </ProtectedRoute>
          } />
          <Route path="/devices" element={
            <ProtectedRoute>
              <DevicesPage />
            </ProtectedRoute>
          } />
          <Route path="/devices/:id" element={
            <ProtectedRoute>
              <DeviceDetailPage />
            </ProtectedRoute>
          } />
          <Route path="/clients" element={
            <ProtectedRoute>
              <ClientsPage />
            </ProtectedRoute>
          } />
          <Route path="/insights" element={
            <ProtectedRoute>
              <InsightsPage />
            </ProtectedRoute>
          } />
          <Route path="/alerts" element={
            <ProtectedRoute>
              <AlertsPage />
            </ProtectedRoute>
          } />
          <Route path="/settings" element={
            <ProtectedRoute>
              <SettingsPage />
            </ProtectedRoute>
          } />
          <Route path="/health" element={
            <ProtectedRoute>
              <HealthPage />
            </ProtectedRoute>
          } />
          <Route path="/logs" element={
            <ProtectedRoute>
              <LogsPage />
            </ProtectedRoute>
          } />
          <Route path="/remote" element={
            <ProtectedRoute>
              <RemotePage />
            </ProtectedRoute>
          } />
          <Route path="/tasks" element={
            <ProtectedRoute>
              <TasksPage />
            </ProtectedRoute>
          } />
          <Route path="/knowledge" element={
            <ProtectedRoute>
              <KnowledgePage />
            </ProtectedRoute>
          } />
          <Route path="/users" element={
            <ProtectedRoute>
              <UsersPage />
            </ProtectedRoute>
          } />
          <Route path="/audit" element={
            <ProtectedRoute>
              <AuditPage />
            </ProtectedRoute>
          } />
          <Route path="/incidents" element={
            <ProtectedRoute>
              <IncidentsPage />
            </ProtectedRoute>
          } />
          <Route path="/notifications" element={
            <ProtectedRoute>
              <NotificationsPage />
            </ProtectedRoute>
          } />
          <Route path="/locations" element={
            <ProtectedRoute>
              <LocationsPage />
            </ProtectedRoute>
          } />
          <Route path="/sla" element={
            <ProtectedRoute>
              <SlaPage />
            </ProtectedRoute>
          } />
          <Route path="/switches" element={
            <ProtectedRoute>
              <SwitchesPage />
            </ProtectedRoute>
          } />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </Suspense>
    </AppLayout>
  );
}

export function AppRouter() {
  return <AppContent />;
}
