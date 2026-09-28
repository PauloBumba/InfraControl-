import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import path from "path";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");

  return {
    plugins: [react()],
    resolve: {
      alias: {
        "@": path.resolve(__dirname, "./src"),
      },
    },
    build: {
      rollupOptions: {
        output: {
          manualChunks: {
            vendor: ["react", "react-dom", "react-router-dom"],
            redux: ["@reduxjs/toolkit", "react-redux"],
          },
        },
      },
    },
    server: {
      port: 3000,
      // O cookie de sessão é Secure (HttpOnly+Secure+SameSite=Strict, F1-T014),
      // então o backend precisa ser acessado por HTTPS mesmo em dev — caso
      // contrário o navegador não guarda o cookie e o login não persiste.
      // Requer `dotnet dev-certs https --trust` e rodar o perfil "https" do
      // backend (dotnet run --launch-profile https). O alvo é sobrescrevível
      // por VITE_DEV_API_TARGET.
      proxy: {
        "/api": {
          target: env.VITE_DEV_API_TARGET || "https://localhost:7237",
          changeOrigin: true,
          secure: false, // aceita o certificado de dev self-signed
        },
        "/hubs": {
          target: env.VITE_DEV_API_TARGET || "https://localhost:7237",
          changeOrigin: true,
          ws: true,
          secure: false,
        },
      },
    },
  };
});
