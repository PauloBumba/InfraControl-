import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAppSelector } from "@/app/store/hooks";
import { Button } from "@/components/ui/Button";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusChip } from "@/components/ui/StatusChip";
import {
  approveProject,
  createProjectInvitation,
  listProjectPhases,
  submitProjectForApproval,
  type ProjectItem,
  type ProjectPhaseItem,
} from "@/services/api/projects";
import { getProject } from "@/services/api/projects";

const PROJECT_STATUS_LABEL: Record<string, string> = {
  Draft: "Rascunho",
  Planned: "Planejado",
  InProgress: "Em andamento",
  PendingFinalApproval: "Aguardando homologação",
  Completed: "Concluído",
  Cancelled: "Cancelado",
  OnHold: "Em espera",
};

const PROJECT_STATUS_COLOR: Record<string, { background: string; color: string }> = {
  Draft: { background: "#f1f5f9", color: "#475569" },
  Planned: { background: "#e0e7ff", color: "#4338ca" },
  InProgress: { background: "#dbeafe", color: "#1d4ed8" },
  PendingFinalApproval: { background: "#fef3c7", color: "#b45309" },
  Completed: { background: "#dcfce7", color: "#15803d" },
  Cancelled: { background: "#fee2e2", color: "#b91c1c" },
  OnHold: { background: "#f3e8ff", color: "#7c3aed" },
};

const PROJECT_PRIORITY_LABEL: Record<string, string> = {
  Low: "Baixa",
  Medium: "Média",
  High: "Alta",
  Critical: "Crítica",
};

const PROJECT_PRIORITY_COLOR: Record<string, { background: string; color: string }> = {
  Low: { background: "#ecfdf5", color: "#047857" },
  Medium: { background: "#fff7ed", color: "#c2410c" },
  High: { background: "#fef2f2", color: "#b91c1c" },
  Critical: { background: "#fee2e2", color: "#7f1d1d" },
};

const PROJECT_MEMBER_ROLE_LABEL: Record<string, string> = {
  Leader: "Líder",
  Member: "Membro",
  Reviewer: "Revisor",
};

function formatDate(value: string | null): string {
  if (!value) return "—";
  const [year, month, day] = value.split("-");
  if (year && month && day) return `${day}/${month}/${year}`;
  return value;
}

export function ProjectDetailPage() {
  const { id } = useParams();
  const user = useAppSelector((state) => state.auth.user);
  const isAdmin = (user?.roles ?? []).includes("Admin");

  const [project, setProject] = useState<ProjectItem | null>(null);
  const [phases, setPhases] = useState<ProjectPhaseItem[]>([]);
  const [note, setNote] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [invitationEmail, setInvitationEmail] = useState("");
  const [invitationRole, setInvitationRole] = useState("1");
  const [invitationSending, setInvitationSending] = useState(false);
  const [invitationNotice, setInvitationNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const canManageTeam = isAdmin || (!!user?.id && user.id === project?.createdById);

  const refresh = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const [current, phaseList] = await Promise.all([getProject(id), listProjectPhases(id)]);
      setProject(current);
      setPhases(phaseList);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível carregar o projeto.");
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const handleSubmitApproval = async () => {
    if (!id) return;
    setSaving(true);
    setError(null);
    try {
      await submitProjectForApproval(id);
      await refresh();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível enviar o projeto para homologação.");
    } finally {
      setSaving(false);
    }
  };

  const handleApprove = async () => {
    if (!id) return;
    setSaving(true);
    setError(null);
    try {
      await approveProject(id, note.trim() || "Projeto homologado conforme evidências e critérios de aceite.");
      setNote("");
      await refresh();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível homologar o projeto.");
    } finally {
      setSaving(false);
    }
  };

  const handleInvite = async () => {
    if (!id) return;
    setInvitationSending(true);
    setError(null);
    setInvitationNotice(null);
    try {
      const result = await createProjectInvitation(id, {
        email: invitationEmail.trim(),
        role: Number(invitationRole),
      });
      if (!result)
        throw new Error("O servidor não confirmou a criação do convite.");

      setInvitationEmail("");
      setInvitationNotice(
        result.requiresActivation
          ? result.accountExists
            ? "Convite enviado. A pessoa deverá ativar a conta pelo link; depois disso, será adicionada à equipe."
            : "Este e-mail ainda não possui conta. Enviamos o convite de ativação; a pessoa entrará na equipe após concluir o cadastro."
          : "Convite enviado para o e-mail cadastrado. A pessoa deve entrar no sistema e aceitar o convite.",
      );
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível convidar o membro.");
    } finally {
      setInvitationSending(false);
    }
  };

  if (loading) {
    return <div style={{ padding: 24, color: "#64748b" }}>Carregando projeto…</div>;
  }

  if (!project) {
    // "Não encontrado" é uma RESPOSTA do servidor; não conseguir perguntar é
    // outra coisa (F29-T004). Sem esta separação, uma falha de rede aparecia
    // como se o id estivesse errado — e o usuário saía procurando um projeto
    // que nunca existiu.
    return (
      <div style={{ display: "grid", gap: 12 }}>
        <PageHeader
          title={error ? "Não foi possível carregar o projeto" : "Projeto não encontrado"}
        />
        {error && <ErrorBanner message={error} onRetry={() => void refresh()} />}
        <Link to="/projects">
          <Button variant="ghost" icon="pi pi-arrow-left">Voltar para Projetos</Button>
        </Link>
      </div>
    );
  }

  const statusColor = PROJECT_STATUS_COLOR[project.status] ?? { background: "#f1f5f9", color: "#475569" };
  const priorityColor = PROJECT_PRIORITY_COLOR[project.priority] ?? { background: "#f8fafc", color: "#334155" };
  const canSubmit = project.status !== "Completed" && project.status !== "Cancelled";

  return (
    <div style={{ display: "grid", gap: 20 }}>
      <PageHeader
        title={`${project.code} · ${project.title}`}
        subtitle="Visão geral do projeto, fases, compromissos e aprovação final."
        actions={
          <Link to="/projects">
            <Button variant="ghost" icon="pi pi-arrow-left">Voltar</Button>
          </Link>
        }
      />

      {error && <ErrorBanner message={error} onRetry={() => void refresh()} />}

      <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 20, display: "grid", gap: 16 }}>
        <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
          <StatusChip label={PROJECT_STATUS_LABEL[project.status] ?? project.status} background={statusColor.background} color={statusColor.color} />
          <StatusChip label={PROJECT_PRIORITY_LABEL[project.priority] ?? project.priority} background={priorityColor.background} color={priorityColor.color} />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 12 }}>
          <div><strong>Planejado:</strong> {formatDate(project.plannedStartDate)} → {formatDate(project.plannedEndDate)}</div>
          <div><strong>Execução:</strong> {formatDate(project.actualStartDate)} → {formatDate(project.actualEndDate)}</div>
          <div><strong>Líder:</strong> {project.leaderId}</div>
          <div><strong>Gestor:</strong> {project.managerId ?? "—"}</div>
        </div>

        <div style={{ display: "grid", gap: 12 }}>
          <div>
            <div style={{ fontSize: 12, textTransform: "uppercase", letterSpacing: 0.5, color: "#64748b", marginBottom: 6 }}>Descrição</div>
            <div style={{ color: "#334155", lineHeight: 1.6 }}>{project.description || "Sem descrição registrada."}</div>
          </div>
          {project.justification && (
            <div>
              <div style={{ fontSize: 12, textTransform: "uppercase", letterSpacing: 0.5, color: "#64748b", marginBottom: 6 }}>Justificativa</div>
              <div style={{ color: "#334155", lineHeight: 1.6 }}>{project.justification}</div>
            </div>
          )}
        </div>

        <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
          {canSubmit && (
            <Button variant="primary" icon="pi pi-send" onClick={() => void handleSubmitApproval()} disabled={saving}>
              {saving ? "Enviando…" : "Enviar para homologação"}
            </Button>
          )}
          {project.status === "PendingFinalApproval" && isAdmin && (
            <Button variant="primary" icon="pi pi-check" onClick={() => void handleApprove()} disabled={saving}>
              {saving ? "Homologando…" : "Homologar projeto"}
            </Button>
          )}
        </div>
      </div>

      <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 20, display: "grid", gap: 12 }}>
        <div style={{ fontWeight: 700, fontSize: 18 }}>Equipe do projeto</div>
        {invitationNotice && (
          <div role="status" style={{ padding: 12, borderRadius: 8, background: "#ecfdf5", color: "#047857" }}>
            {invitationNotice}
          </div>
        )}
        {project.members?.length ? (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: 12 }}>
            {project.members.map((member) => (
              <div key={member.userId} style={{ padding: 12, border: "1px solid #e2e8f0", borderRadius: 8, background: "#f8fafc" }}>
                <div style={{ fontWeight: 700 }}>{member.userId}</div>
                <div style={{ color: "#64748b", fontSize: 12, marginTop: 4 }}>
                  {PROJECT_MEMBER_ROLE_LABEL[member.role] ?? member.role}
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div style={{ color: "#64748b" }}>Nenhum membro adicionado ao projeto.</div>
        )}
        {canManageTeam && (
          <form
            onSubmit={(event) => {
              event.preventDefault();
              void handleInvite();
            }}
            style={{ display: "flex", flexWrap: "wrap", alignItems: "end", gap: 10, paddingTop: 8, borderTop: "1px solid #e2e8f0" }}
          >
            <div style={{ flex: "1 1 220px" }}>
              <label htmlFor="project-invitation-email" style={{ display: "block", fontSize: 12, color: "#475569", marginBottom: 6 }}>
                E-mail do participante
              </label>
              <input
                id="project-invitation-email"
                type="email"
                required
                value={invitationEmail}
                onChange={(event) => setInvitationEmail(event.target.value)}
                style={{ width: "100%", padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
              />
            </div>
            <div style={{ flex: "0 1 180px" }}>
              <label htmlFor="project-invitation-role" style={{ display: "block", fontSize: 12, color: "#475569", marginBottom: 6 }}>
                Papel na equipe
              </label>
              <select
                id="project-invitation-role"
                value={invitationRole}
                onChange={(event) => setInvitationRole(event.target.value)}
                style={{ width: "100%", padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
              >
                <option value="0">Líder</option>
                <option value="1">Membro</option>
                <option value="2">Revisor</option>
              </select>
            </div>
            <Button type="submit" variant="primary" icon="pi pi-send" disabled={invitationSending}>
              {invitationSending ? "Enviando…" : "Convidar"}
            </Button>
          </form>
        )}
      </div>

      {project.status === "PendingFinalApproval" && isAdmin && (
        <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 20, display: "grid", gap: 12 }}>
          <div style={{ fontWeight: 700 }}>Parecer de aprovação</div>
          <textarea
            value={note}
            onChange={(event) => setNote(event.target.value)}
            rows={5}
            placeholder="Descreva o parecer final da homologação do projeto."
            style={{ width: "100%", resize: "vertical", padding: 10, borderRadius: 8, border: "1px solid #cbd5e1" }}
          />
        </div>
      )}

      <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 20, display: "grid", gap: 16 }}>
        <div style={{ fontWeight: 700, fontSize: 18 }}>Fases</div>
        {phases.length === 0 ? (
          <div style={{ color: "#64748b" }}>Nenhuma fase cadastrada para este projeto.</div>
        ) : (
          phases.map((phase) => (
            <div key={phase.id} style={{ border: "1px solid #e2e8f0", borderRadius: 10, padding: 14, display: "grid", gap: 8 }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                <strong>{phase.order}. {phase.title}</strong>
                <StatusChip label={phase.status} background="#e2e8f0" color="#334155" />
              </div>
              <div style={{ color: "#475569", fontSize: 13 }}>{phase.description || "Sem descrição."}</div>
              <div style={{ color: "#64748b", fontSize: 12 }}>
                {formatDate(phase.plannedStartDate)} → {formatDate(phase.plannedEndDate)} · {phase.percent}% concluído
              </div>
              <div style={{ width: "100%", background: "#e2e8f0", borderRadius: 999, overflow: "hidden" }}>
                <div style={{ width: `${Math.min(100, Math.max(0, phase.percent))}%`, height: 8, background: "#2563eb" }} />
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
