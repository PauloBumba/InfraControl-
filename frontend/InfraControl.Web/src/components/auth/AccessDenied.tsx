import { useNavigate } from "react-router-dom";

/**
 * F14-T009 — sessão válida, nível insuficiente (decisão do usuário:
 * tela 403 dedicada em vez de redirect silencioso). Renderizada dentro
 * do AppLayout: o menu lateral já esconde a rota, e daqui o usuário
 * volta para o início. Não revela o conteúdo da página.
 */
export function AccessDenied() {
  const navigate = useNavigate();

  return (
    <div
      className="uni-panel"
      style={{ padding: 48, textAlign: "center", display: "flex", flexDirection: "column", alignItems: "center", gap: 4 }}
    >
      <i className="pi pi-lock" style={{ fontSize: 34, color: "#9ca3af" }} />
      <h1 style={{ fontSize: 18, fontWeight: 700, marginTop: 10 }}>Acesso restrito</h1>
      <p style={{ fontSize: 13, color: "#6b7280", marginTop: 4, maxWidth: 420 }}>
        Você não tem permissão para acessar esta área. Se acredita que isso
        está errado, fale com um administrador.
      </p>
      <button
        onClick={() => navigate("/", { replace: true })}
        style={{
          marginTop: 14,
          padding: "8px 18px",
          borderRadius: 6,
          background: "#3b82f6",
          color: "white",
          fontSize: 13,
          fontWeight: 500,
          border: "none",
          cursor: "pointer",
        }}
      >
        <i className="pi pi-home" style={{ fontSize: 12, marginRight: 6 }} />
        Voltar ao início
      </button>
    </div>
  );
}
