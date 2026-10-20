import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Provider } from "react-redux";
import { PersistGate } from "redux-persist/integration/react";
import { PrimeReactProvider } from "primereact/api";
import { store, persistor } from "./app/store";
import { AppRouter } from "./app/router";
import { initTheme } from "./services/theme";
// Tema claro do PrimeReact (decisão do levantamento docs/28): o app é
// claro (painéis brancos uni-*) e o tema anterior `lara-dark-blue` não
// tinha consumo — nenhum componente PrimeReact existia; agora com o
// piloto do DataTable na AuditPage o tema precisa combinar com a UI.
import "primereact/resources/themes/lara-light-blue/theme.css";
import "primereact/resources/primereact.min.css";
import "primeflex/primeflex.css";
import "primeicons/primeicons.css";

// Custom styles + skin UniFi sobre o PrimeReact
import "./styles.css";
import "./styles/prime-overrides.css";

// Tema claro/escuro gravado (services/theme): aplica ANTES do render para
// não piscar o tema errado na carga.
initTheme();

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    {/* Provider do PrimeReact: sem ele o `context` chega undefined nos
        componentes e o listener de scroll/fora do `<Menu popup>` quebra
        (listener lê `context.hideOverlaysOnDocumentScrolling` sem guard). */}
    <PrimeReactProvider>
      <Provider store={store}>
        <PersistGate loading={null} persistor={persistor}>
          <QueryClientProvider client={queryClient}>
            <BrowserRouter>
              <AppRouter />
            </BrowserRouter>
          </QueryClientProvider>
        </PersistGate>
      </Provider>
    </PrimeReactProvider>
  </StrictMode>,
);
