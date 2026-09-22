import { knowledgeArticles } from "@/data/dashboardData";

export function KnowledgePage() {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Base de Conhecimento</h1>
          <p style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>{knowledgeArticles.length} artigos</p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 6, background: "#f3f4f6", borderRadius: 6, padding: "5px 10px" }}>
            <i className="pi pi-search" style={{ fontSize: 12, color: "#9ca3af" }} />
            <input type="text" placeholder="Buscar artigos..." style={{ border: "none", background: "none", outline: "none", fontSize: 12, width: 160 }} />
          </div>
          <button style={{ display: "flex", alignItems: "center", gap: 6, padding: "5px 12px", borderRadius: 6, background: "#0088ff", color: "white", fontSize: 12, fontWeight: 500 }}>
            <i className="pi pi-plus" style={{ fontSize: 10 }} /> Novo Artigo
          </button>
        </div>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))", gap: 12 }}>
        {knowledgeArticles.map((article) => (
          <div key={article.id} style={{ background: "white", borderRadius: 8, border: "1px solid #e5e7eb", padding: 16, cursor: "pointer", transition: "all 0.12s" }}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 8 }}>
              <span style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: "#eff6ff", color: "#3b82f6" }}>
                {article.category}
              </span>
              <span style={{ fontSize: 10, color: "#9ca3af" }}>{article.lastUpdate}</span>
            </div>
            <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 6 }}>{article.title}</div>
            <div style={{ fontSize: 12, color: "#6b7280", marginBottom: 10, lineHeight: 1.4 }}>{article.content}</div>
            <div style={{ display: "flex", gap: 4, flexWrap: "wrap" }}>
              {article.tags.map((tag) => (
                <span key={tag} style={{ fontSize: 9, padding: "1px 5px", borderRadius: 3, background: "#f3f4f6", color: "#6b7280" }}>{tag}</span>
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
