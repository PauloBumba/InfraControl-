import { useState } from "react";
import { users, roles, rolePermissionsMatrix } from "@/data/dashboardData";

const tabs = [
  { id: "users", label: "Usuários", icon: "pi pi-users" },
  { id: "roles", label: "Roles", icon: "pi pi-id-card" },
  { id: "matrix", label: "Permissões", icon: "pi pi-lock" },
];

const statusConfig: Record<string, { label: string; bg: string; color: string }> = {
  active: { label: "Ativo", bg: "#dcfce7", color: "#16a34a" },
  inactive: { label: "Inativo", bg: "#f1f5f9", color: "#64748b" },
  pending: { label: "Pendente", bg: "#fef3c7", color: "#d97706" },
};

const roleColors: Record<string, { bg: string; color: string }> = {
  Admin: { bg: "#fee2e2", color: "#dc2626" },
  User: { bg: "#dbeafe", color: "#2563eb" },
  Viewer: { bg: "#f1f5f9", color: "#64748b" },
};

export function UsersPage() {
  const [activeTab, setActiveTab] = useState("users");
  const [search, setSearch] = useState("");

  const filteredUsers = users.filter(
    (u) =>
      u.name.toLowerCase().includes(search.toLowerCase()) ||
      u.email.toLowerCase().includes(search.toLowerCase()) ||
      u.role.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Usuários & Roles</h1>
          <p style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
            {users.length} usuários · {roles.length} roles
          </p>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 6, background: "#f1f5f9", borderRadius: 6, padding: "6px 12px" }}>
            <i className="pi pi-search" style={{ fontSize: 12, color: "#94a3b8" }} />
            <input
              type="text"
              placeholder="Pesquisar usuários..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              style={{ border: "none", background: "none", outline: "none", fontSize: 13, width: 180 }}
            />
          </div>
          <button
            style={{
              display: "flex",
              alignItems: "center",
              gap: 6,
              padding: "6px 12px",
              borderRadius: 6,
              background: "#3b82f6",
              color: "white",
              fontSize: 13,
              fontWeight: 500,
              border: "none",
            }}
          >
            <i className="pi pi-user-plus" style={{ fontSize: 12 }} /> Convidar
          </button>
        </div>
      </div>

      <div style={{ display: "flex", gap: 4, borderBottom: "1px solid #e2e8f0" }}>
        {tabs.map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            style={{
              display: "flex",
              alignItems: "center",
              gap: 6,
              padding: "8px 14px",
              fontSize: 13,
              fontWeight: 500,
              color: activeTab === tab.id ? "#3b82f6" : "#64748b",
              background: "none",
              border: "none",
              borderBottom: activeTab === tab.id ? "2px solid #3b82f6" : "2px solid transparent",
              marginBottom: -1,
              cursor: "pointer",
            }}
          >
            <i className={tab.icon} style={{ fontSize: 12 }} />
            {tab.label}
          </button>
        ))}
      </div>

      {activeTab === "users" && (
        <div className="uni-panel">
          <table className="uni-clients-table">
            <thead>
              <tr>
                <th>Usuário</th>
                <th>Role</th>
                <th>Status</th>
                <th>Sessões</th>
                <th>Último login</th>
                <th>Criado em</th>
                <th style={{ width: 80 }}>Ações</th>
              </tr>
            </thead>
            <tbody>
              {filteredUsers.map((user) => {
                const status = statusConfig[user.status] ?? { label: "—", bg: "#f1f5f9", color: "#64748b" };
                const roleColor = roleColors[user.role] ?? { bg: "#dbeafe", color: "#2563eb" };
                const initials = user.name
                  .split(" ")
                  .map((p) => p[0])
                  .slice(0, 2)
                  .join("")
                  .toUpperCase();
                return (
                  <tr key={user.id}>
                    <td>
                      <div className="uni-client-table-name">
                        <div
                          className="uni-client-table-icon"
                          style={{ background: "#eff6ff", color: "#3b82f6", fontWeight: 600, fontSize: 12 }}
                        >
                          {initials}
                        </div>
                        <div>
                          <div style={{ fontWeight: 500 }}>{user.name}</div>
                          <div style={{ fontSize: 11, color: "#94a3b8" }}>{user.email}</div>
                        </div>
                      </div>
                    </td>
                    <td>
                      <span
                        style={{
                          fontSize: 11,
                          fontWeight: 500,
                          padding: "2px 8px",
                          borderRadius: 4,
                          background: roleColor.bg,
                          color: roleColor.color,
                        }}
                      >
                        {user.role}
                      </span>
                    </td>
                    <td>
                      <span
                        style={{
                          fontSize: 11,
                          fontWeight: 500,
                          padding: "2px 8px",
                          borderRadius: 4,
                          background: status.bg,
                          color: status.color,
                        }}
                      >
                        {status.label}
                      </span>
                    </td>
                    <td style={{ fontSize: 12, color: "#64748b" }}>{user.sessions}</td>
                    <td style={{ fontSize: 12, color: "#64748b" }}>{user.lastLogin}</td>
                    <td style={{ fontSize: 12, color: "#64748b" }}>{user.createdAt}</td>
                    <td>
                      <div style={{ display: "flex", gap: 4 }}>
                        <button
                          title="Editar"
                          style={{
                            width: 28,
                            height: 28,
                            borderRadius: 6,
                            border: "1px solid #e2e8f0",
                            background: "white",
                            color: "#64748b",
                            cursor: "pointer",
                            fontSize: 12,
                          }}
                        >
                          <i className="pi pi-pencil" />
                        </button>
                        <button
                          title="Mais opções"
                          style={{
                            width: 28,
                            height: 28,
                            borderRadius: 6,
                            border: "1px solid #e2e8f0",
                            background: "white",
                            color: "#64748b",
                            cursor: "pointer",
                            fontSize: 12,
                          }}
                        >
                          <i className="pi pi-ellipsis-v" />
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
              {filteredUsers.length === 0 && (
                <tr>
                  <td colSpan={7} style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13 }}>
                    Nenhum usuário encontrado
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {activeTab === "roles" && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))", gap: 16 }}>
          {roles.map((role) => {
            const roleColor = roleColors[role.name] ?? { bg: "#dbeafe", color: "#2563eb" };
            return (
              <div key={role.id} className="uni-panel" style={{ padding: 16 }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 8 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    <span
                      style={{
                        fontSize: 12,
                        fontWeight: 600,
                        padding: "3px 10px",
                        borderRadius: 999,
                        background: roleColor.bg,
                        color: roleColor.color,
                      }}
                    >
                      {role.name}
                    </span>
                    {role.system && (
                      <span style={{ fontSize: 10, color: "#94a3b8" }}>Sistema</span>
                    )}
                  </div>
                  <span style={{ fontSize: 12, color: "#64748b" }}>{role.users} usuários</span>
                </div>
                <p style={{ fontSize: 13, color: "#64748b", marginBottom: 12 }}>{role.description}</p>
                <div style={{ display: "flex", flexWrap: "wrap", gap: 4 }}>
                  {role.permissions.map((perm) => (
                    <span
                      key={perm}
                      style={{
                        fontSize: 10,
                        fontFamily: "monospace",
                        padding: "2px 6px",
                        borderRadius: 4,
                        background: "#f1f5f9",
                        color: "#475569",
                      }}
                    >
                      {perm}
                    </span>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {activeTab === "matrix" && (
        <div className="uni-panel">
          <table className="uni-clients-table">
            <thead>
              <tr>
                <th>Permissão</th>
                <th style={{ textAlign: "center", width: 100 }}>Admin</th>
                <th style={{ textAlign: "center", width: 100 }}>User</th>
                <th style={{ textAlign: "center", width: 100 }}>Viewer</th>
              </tr>
            </thead>
            <tbody>
              {rolePermissionsMatrix.map((row) => (
                <tr key={row.permission}>
                  <td style={{ fontFamily: "monospace", fontSize: 12 }}>{row.permission}</td>
                  <td style={{ textAlign: "center" }}>
                    {row.admin ? (
                      <i className="pi pi-check" style={{ color: "#16a34a", fontSize: 14 }} />
                    ) : (
                      <i className="pi pi-times" style={{ color: "#cbd5e1", fontSize: 14 }} />
                    )}
                  </td>
                  <td style={{ textAlign: "center" }}>
                    {row.user ? (
                      <i className="pi pi-check" style={{ color: "#16a34a", fontSize: 14 }} />
                    ) : (
                      <i className="pi pi-times" style={{ color: "#cbd5e1", fontSize: 14 }} />
                    )}
                  </td>
                  <td style={{ textAlign: "center" }}>
                    {row.viewer ? (
                      <i className="pi pi-check" style={{ color: "#16a34a", fontSize: 14 }} />
                    ) : (
                      <i className="pi pi-times" style={{ color: "#cbd5e1", fontSize: 14 }} />
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
