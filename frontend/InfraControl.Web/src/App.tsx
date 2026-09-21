import { useState } from "react";
import { AppLayout } from "@/components/layout/AppLayout";
import { DashboardPage } from "@/pages/DashboardPage";
import "./styles.css";

export function App() {
  const [activeItem, setActiveItem] = useState("Visão geral");

  return <AppLayout activeItem={activeItem} onNavigate={setActiveItem}>
    <DashboardPage />
  </AppLayout>;
}
