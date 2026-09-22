import { useLocation, useNavigate } from "react-router-dom";
import { useAppDispatch, useAppSelector } from "@/app/store/hooks";
import { setActiveItem } from "@/app/store/slices/navigationSlice";
import { AppLayout } from "@/components/layout/AppLayout";
import { DashboardPage } from "@/pages/DashboardPage";
import { NetworkPage } from "@/pages/NetworkPage";
import { WiFiPage } from "@/pages/WiFiPage";
import { TopologyPage } from "@/pages/TopologyPage";
import { DevicesPage } from "@/pages/DevicesPage";
import { ClientsPage } from "@/pages/ClientsPage";
import { InsightsPage } from "@/pages/InsightsPage";
import { AlertsPage } from "@/pages/AlertsPage";
import { SettingsPage } from "@/pages/SettingsPage";
import { NotFoundPage } from "@/pages/NotFoundPage";

const routePageMap: Record<string, React.ComponentType> = {
  "/": DashboardPage,
  "/network": NetworkPage,
  "/wifi": WiFiPage,
  "/topology": TopologyPage,
  "/devices": DevicesPage,
  "/clients": ClientsPage,
  "/insights": InsightsPage,
  "/alerts": AlertsPage,
  "/settings": SettingsPage,
};

const routeLabelMap: Record<string, string> = {
  "/": "Visão geral",
  "/network": "Rede",
  "/wifi": "WiFi",
  "/topology": "Topologia",
  "/devices": "Dispositivos",
  "/clients": "Clientes",
  "/insights": "Insights",
  "/alerts": "Alertas",
  "/settings": "Configurações",
};

const labelRouteMap: Record<string, string> = {
  "Visão geral": "/",
  "Inventário": "/network",
  "Dispositivos": "/devices",
  "Rede": "/network",
  "Monitoramento": "/insights",
  "Alertas": "/alerts",
  "Incidentes": "/alerts",
  "Tarefas": "/",
  "Conhecimento": "/insights",
  "Relatórios": "/insights",
  "Configurações": "/settings",
};

export function App() {
  const location = useLocation();
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  const activeItemRedux = useAppSelector((s) => s.navigation.activeItem);

  const activeLabel = routeLabelMap[location.pathname] || activeItemRedux;
  const PageComponent = routePageMap[location.pathname] || NotFoundPage;

  const handleNavigate = (label: string) => {
    const route = labelRouteMap[label] || "/";
    dispatch(setActiveItem(label));
    navigate(route);
  };

  return (
    <AppLayout activeItem={activeLabel} onNavigate={handleNavigate}>
      <PageComponent />
    </AppLayout>
  );
}
