import { tasks } from "@/data/dashboardData";

const columns = [
  { id: "todo", label: "A Fazer", color: "#6b7280" },
  { id: "in_progress", label: "Em Progresso", color: "#3b82f6" },
  { id: "done", label: "Concluído", color: "#22c55e" },
];

const priorityColors = { high: "#dc2626", medium: "#f59e0b", low: "#94a3b8" };
const priorityLabels = { high: "Alta", medium: "Média", low: "Baixa" };

export function TasksPage() {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700 }}>Tarefas</h1>
          <p style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>{tasks.length} tarefas</p>
        </div>
        <button style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 14px", borderRadius: 6, background: "#3b82f6", color: "white", fontSize: 12, fontWeight: 500 }}>
          <i className="pi pi-plus" style={{ fontSize: 11 }} /> Nova Tarefa
        </button>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 12, alignItems: "start" }}>
        {columns.map((col) => {
          const colTasks = tasks.filter(t => t.status === col.id);
          return (
            <div key={col.id}>
              <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 10, padding: "8px 12px", background: "white", borderRadius: 6, border: "1px solid #e5e7eb" }}>
                <div style={{ width: 8, height: 8, borderRadius: "50%", background: col.color }} />
                <span style={{ fontSize: 12, fontWeight: 600 }}>{col.label}</span>
                <span style={{ marginLeft: "auto", fontSize: 10, fontWeight: 600, padding: "1px 6px", borderRadius: 10, background: "#f3f4f6", color: "#6b7280" }}>{colTasks.length}</span>
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {colTasks.map((task) => (
                  <div key={task.id} style={{ background: "white", borderRadius: 8, border: "1px solid #e5e7eb", padding: 12, cursor: "pointer", transition: "all 0.12s" }}>
                    <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 6 }}>
                      <span style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: `${priorityColors[task.priority]}15`, color: priorityColors[task.priority] }}>
                        {priorityLabels[task.priority]}
                      </span>
                      <span style={{ fontSize: 10, color: "#9ca3af" }}>{task.dueDate}</span>
                    </div>
                    <div style={{ fontSize: 12, fontWeight: 500, marginBottom: 6 }}>{task.title}</div>
                    <div style={{ fontSize: 10, color: "#6b7280", marginBottom: 6 }}>
                      <i className="pi pi-server" style={{ fontSize: 9, marginRight: 3 }} /> {task.device}
                    </div>
                    <div style={{ display: "flex", gap: 4, flexWrap: "wrap" }}>
                      {task.tags.map((tag) => (
                        <span key={tag} style={{ fontSize: 9, padding: "1px 5px", borderRadius: 3, background: "#f3f4f6", color: "#6b7280" }}>{tag}</span>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
