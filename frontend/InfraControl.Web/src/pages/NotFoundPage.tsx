import { useNavigate } from "react-router-dom";

export function NotFoundPage() {
  const navigate = useNavigate();

  return (
    <div className="page-content" style={{ textAlign: "center", paddingTop: "100px" }}>
      <h1 style={{ fontSize: "var(--text-3xl)", marginBottom: "var(--space-4)" }}>404</h1>
      <p style={{ color: "var(--text-muted)", marginBottom: "var(--space-6)" }}>
        Página não encontrada.
      </p>
      <button className="primary-button" onClick={() => navigate("/")}>
        <i className="pi pi-home" /> Voltar ao Dashboard
      </button>
    </div>
  );
}
