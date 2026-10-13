import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useAppSelector } from "@/app/store/hooks";
import { Button } from "@/components/ui/Button";
import { ConfirmDialog, Dialog } from "@/components/ui/Dialog";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusChip } from "@/components/ui/StatusChip";
import {
  approveProject,
  createProjectInvitation,
  deleteProject,
  deleteProjectEvidence,
  deleteProjectImpediment,
  downloadProjectEvidence,
  getProject,
  listProjectEvidences,
  listProjectImpediments,
  listProjectPhases,
  reopenProjectImpediment,
  resolveProjectImpediment,
  reviewProjectEvidence,
  submitProjectForApproval,
  updateProject,
  updateProjectImpediment,
  createProjectImpediment,
  uploadProjectEvidence,
  type ProjectEvidenceItem,
  type ProjectImpedimentItem,
  type ProjectItem,
  type ProjectPhaseItem,
} from "@/services/api/projects";

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

const PRIORITY_OPTIONS = ["Low", "Medium", "High", "Critical"];

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

const IMPEDIMENT_CATEGORY_LABEL: Record<string, string> = {
  Technical: "Técnica",
  Vendor: "Fornecedor",
  Budget: "Orçamento",
  SecurityAccess: "Segurança/acesso",
  Other: "Outra",
};

const IMPEDIMENT_CATEGORY_OPTIONS = ["Technical", "Vendor", "Budget", "SecurityAccess", "Other"];

const IMPEDIMENT_SEVERITY_LABEL: Record<string, string> = {
  Low: "Baixa",
  Medium: "Média",
  High: "Alta",
  Critical: "Crítica",
};

const IMPEDIMENT_SEVERITY_OPTIONS = ["Low", "Medium", "High", "Critical"];

const IMPEDIMENT_SEVERITY_COLOR: Record<string, { background: string; color: string }> = {
  Low: { background: "#ecfdf5", color: "#047857" },
  Medium: { background: "#fff7ed", color: "#c2410c" },
  High: { background: "#fef2f2", color: "#b91c1c" },
  Critical: { background: "#fee2e2", color: "#7f1d1d" },
};

const EVIDENCE_TYPE_LABEL: Record<string, string> = {
  TestReport: "Relatório de teste",
  ConfigurationBackup: "Backup de configuração",
  ArchitectureDiagram: "Diagrama de arquitetura",
  AcceptanceMinutes: "Ata de homologação",
  Other: "Outro",
};

const EVIDENCE_TYPE_OPTIONS = [
  "TestReport",
  "ConfigurationBackup",
  "ArchitectureDiagram",
  "AcceptanceMinutes",
  "Other",
];

const EVIDENCE_STATUS_LABEL: Record<string, string> = {
  Pending: "Pendente",
  Validated: "Validada",
  Rejected: "Rejeitada",
};

const EVIDENCE_STATUS_COLOR: Record<string, { background: string; color: string }> = {
  Pending: { background: "#fef3c7", color: "#b45309" },
  Validated: { background: "#dcfce7", color: "#15803d" },
  Rejected: { background: "#fee2e2", color: "#b91c1c" },
};

const MAX_EVIDENCE_BYTES = 10 * 1024 * 1024;

function formatDate(value: string | null): string {
  if (!value) return "—";
  const [year, month, day] = value.split("-");
  if (year && month && day) return `${day}/${month}/${year}`;
  return value;
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} kB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

const EMPTY_IMP_FORM = {
  title: "",
  description: "",
  category: "Technical",
  severity: "Medium",
  mitigationPlan: "",
};

export function ProjectDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const user = useAppSelector((state) => state.auth.user);
  const roles = user?.roles ?? [];
  const canOperate = roles.includes("Operator") || roles.includes("Admin");
  const isAdmin = roles.includes("Admin");

  const [project, setProject] = useState<ProjectItem | null>(null);
  const [phases, setPhases] = useState<ProjectPhaseItem[]>([]);
  const [impediments, setImpediments] = useState<ProjectImpedimentItem[]>([]);
  const [evidences, setEvidences] = useState<ProjectEvidenceItem[]>([]);
  const [note, setNote] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [invitationEmail, setInvitationEmail] = useState("");
  const [invitationRole, setInvitationRole] = useState("1");
  const [invitationSending, setInvitationSending] = useState(false);
  const [invitationNotice, setInvitationNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const canManageTeam = isAdmin || (!!user?.id && user.id === project?.createdById);

  // Edição/remoção do projeto (U/D na tela de detalhe).
  const [editOpen, setEditOpen] = useState(false);
  const [editSaving, setEditSaving] = useState(false);
  const [editError, setEditError] = useState<string | null>(null);
  const [editForm, setEditForm] = useState({
    title: "",
    description: "",
    priority: "Medium",
    justification: "",
    plannedStartDate: "",
    plannedEndDate: "",
  });
  const [deleteOpen, setDeleteOpen] = useState(false);

  // Impedimentos.
  const [impDialog, setImpDialog] = useState<"create" | "edit" | null>(null);
  const [impEditing, setImpEditing] = useState<ProjectImpedimentItem | null>(null);
  const [impForm, setImpForm] = useState(EMPTY_IMP_FORM);
  const [impSaving, setImpSaving] = useState(false);
  const [impError, setImpError] = useState<string | null>(null);
  const [impDelete, setImpDelete] = useState<ProjectImpedimentItem | null>(null);
  const [impToggling, setImpToggling] = useState<string | null>(null);

  // Evidências.
  const [evType, setEvType] = useState("TestReport");
  const [evDescription, setEvDescription] = useState("");
  const [evFile, setEvFile] = useState<File | null>(null);
  const [evUploading, setEvUploading] = useState(false);
  const [evError, setEvError] = useState<string | null>(null);
  const [evDelete, setEvDelete] = useState<ProjectEvidenceItem | null>(null);
  const [evReject, setEvReject] = useState<ProjectEvidenceItem | null>(null);
  const [evRejectReason, setEvRejectReason] = useState("");
  const [evBusy, setEvBusy] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const current = await getProject(id);
      setProject(current);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível carregar o projeto.");
      setLoading(false);
      return;
    }
    try {
      const [phaseList, impList, evList] = await Promise.all([
        listProjectPhases(id),
        listProjectImpediments(id),
        listProjectEvidences(id),
      ]);
      setPhases(phaseList);
      setImpediments(impList);
      setEvidences(evList);
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Não foi possível carregar os dados complementares do projeto.",
      );
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

  // ---- Edição/remoção do projeto ----

  function openEditProject() {
    if (!project) return;
    setEditForm({
      title: project.title,
      description: project.description,
      priority: project.priority,
      justification: project.justification ?? "",
      plannedStartDate: project.plannedStartDate ?? "",
      plannedEndDate: project.plannedEndDate ?? "",
    });
    setEditError(null);
    setEditOpen(true);
  }

  async function saveEditProject() {
    if (!id) return;
    if (!editForm.title.trim() || !editForm.description.trim()) {
      setEditError("Título e descrição são obrigatórios.");
      return;
    }
    setEditSaving(true);
    setEditError(null);
    try {
      await updateProject(id, {
        title: editForm.title.trim(),
        description: editForm.description.trim(),
        priority: editForm.priority,
        justification: editForm.justification.trim() || null,
        plannedStartDate: editForm.plannedStartDate || null,
        plannedEndDate: editForm.plannedEndDate || null,
      });
      setEditOpen(false);
      setNotice("Projeto atualizado.");
      await refresh();
    } catch (caught) {
      setEditError(caught instanceof Error ? caught.message : "Não foi possível salvar o projeto.");
    } finally {
      setEditSaving(false);
    }
  }

  async function confirmDeleteProject() {
    if (!id || !project) return;
    setSaving(true);
    setError(null);
    try {
      await deleteProject(id);
      navigate("/projects");
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível remover o projeto.");
      setSaving(false);
    }
  }

  // ---- Impedimentos ----

  function openImpCreate() {
    setImpEditing(null);
    setImpForm(EMPTY_IMP_FORM);
    setImpError(null);
    setImpDialog("create");
  }

  function openImpEdit(item: ProjectImpedimentItem) {
    setImpEditing(item);
    setImpForm({
      title: item.title,
      description: item.description,
      category: item.category,
      severity: item.severity,
      mitigationPlan: item.mitigationPlan ?? "",
    });
    setImpError(null);
    setImpDialog("edit");
  }

  async function saveImpediment() {
    if (!id) return;
    if (!impForm.title.trim() || !impForm.description.trim()) {
      setImpError("Título e descrição são obrigatórios.");
      return;
    }
    setImpSaving(true);
    setImpError(null);
    try {
      const input = {
        title: impForm.title.trim(),
        description: impForm.description.trim(),
        category: impForm.category,
        severity: impForm.severity,
        mitigationPlan: impForm.mitigationPlan.trim() || null,
      };
      if (impDialog === "edit" && impEditing) {
        await updateProjectImpediment(id, impEditing.id, input);
        setNotice("Impedimento atualizado.");
      } else {
        await createProjectImpediment(id, input);
        setNotice("Impedimento registrado.");
      }
      setImpDialog(null);
      setImpEditing(null);
      await refresh();
    } catch (caught) {
      setImpError(caught instanceof Error ? caught.message : "Não foi possível salvar o impedimento.");
    } finally {
      setImpSaving(false);
    }
  }

  async function confirmDeleteImpediment() {
    if (!id || !impDelete) return;
    setImpSaving(true);
    setImpError(null);
    try {
      await deleteProjectImpediment(id, impDelete.id);
      setNotice("Impedimento removido.");
      setImpDelete(null);
      await refresh();
    } catch (caught) {
      setImpError(caught instanceof Error ? caught.message : "Não foi possível remover o impedimento.");
    } finally {
      setImpSaving(false);
    }
  }

  async function toggleImpediment(item: ProjectImpedimentItem) {
    if (!id) return;
    setImpToggling(item.id);
    setImpError(null);
    try {
      if (item.isResolved) {
        await reopenProjectImpediment(id, item.id);
        setNotice("Impedimento reaberto.");
      } else {
        await resolveProjectImpediment(id, item.id);
        setNotice("Impedimento marcado como resolvido.");
      }
      await refresh();
    } catch (caught) {
      setImpError(caught instanceof Error ? caught.message : "Não foi possível atualizar o impedimento.");
    } finally {
      setImpToggling(null);
    }
  }

  // ---- Evidências ----

  async function uploadEvidence() {
    if (!id) return;
    if (!evFile) {
      setEvError("Selecione um arquivo para anexar.");
      return;
    }
    if (evFile.size > MAX_EVIDENCE_BYTES) {
      setEvError("O arquivo excede o tamanho máximo de 10 MB.");
      return;
    }
    setEvUploading(true);
    setEvError(null);
    try {
      await uploadProjectEvidence({
        projectId: id,
        type: evType,
        description: evDescription.trim() || null,
        file: evFile,
      });
      setEvFile(null);
      setEvDescription("");
      setNotice("Evidência anexada.");
      await refresh();
    } catch (caught) {
      setEvError(caught instanceof Error ? caught.message : "Não foi possível anexar a evidência.");
    } finally {
      setEvUploading(false);
    }
  }

  async function confirmDeleteEvidence() {
    if (!id || !evDelete) return;
    setEvBusy(evDelete.id);
    setEvError(null);
    try {
      await deleteProjectEvidence(id, evDelete.id);
      setNotice("Evidência removida.");
      setEvDelete(null);
      await refresh();
    } catch (caught) {
      setEvError(caught instanceof Error ? caught.message : "Não foi possível remover a evidência.");
    } finally {
      setEvBusy(null);
    }
  }

  async function validateEvidence(item: ProjectEvidenceItem) {
    if (!id) return;
    setEvBusy(item.id);
    setEvError(null);
    try {
      await reviewProjectEvidence(id, item.id, { decision: "Validated" });
      setNotice("Evidência validada.");
      await refresh();
    } catch (caught) {
      setEvError(caught instanceof Error ? caught.message : "Não foi possível validar a evidência.");
    } finally {
      setEvBusy(null);
    }
  }

  async function confirmRejectEvidence() {
    if (!id || !evReject) return;
    if (!evRejectReason.trim()) {
      setEvError("O motivo da rejeição é obrigatório.");
      return;
    }
    setEvBusy(evReject.id);
    setEvError(null);
    try {
      await reviewProjectEvidence(id, evReject.id, {
        decision: "Rejected",
        rejectionReason: evRejectReason.trim(),
      });
      setNotice("Evidência rejeitada.");
      setEvReject(null);
      setEvRejectReason("");
      await refresh();
    } catch (caught) {
      setEvError(caught instanceof Error ? caught.message : "Não foi possível rejeitar a evidência.");
    } finally {
      setEvBusy(null);
    }
  }

  async function downloadEvidence(item: ProjectEvidenceItem) {
    if (!id) return;
    setEvBusy(item.id);
    setEvError(null);
    try {
      await downloadProjectEvidence(id, item.id, item.fileName);
    } catch (caught) {
      setEvError(caught instanceof Error ? caught.message : "Não foi possível baixar a evidência.");
    } finally {
      setEvBusy(null);
    }
  }

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
          <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
            {canOperate && (
              <>
                <Button variant="ghost" icon="pi pi-pencil" onClick={openEditProject}>
                  Editar
                </Button>
                <Button variant="danger" icon="pi pi-trash" onClick={() => setDeleteOpen(true)}>
                  Remover
                </Button>
              </>
            )}
            <Link to="/projects">
              <Button variant="ghost" icon="pi pi-arrow-left">Voltar</Button>
            </Link>
          </div>
        }
      />

      {error && <ErrorBanner message={error} onRetry={() => void refresh()} />}
      {notice && (
        <div role="status" style={{ padding: 12, borderRadius: 8, background: "#ecfdf5", color: "#047857" }}>
          {notice}
        </div>
      )}

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

      <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 20, display: "grid", gap: 12 }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
          <div style={{ fontWeight: 700, fontSize: 18 }}>Impedimentos</div>
          {canOperate && (
            <Button variant="primary" icon="pi pi-plus" onClick={openImpCreate}>
              Registrar impedimento
            </Button>
          )}
        </div>
        {impError && (
          <div role="alert" style={{ padding: 10, borderRadius: 8, background: "#fef2f2", color: "#991b1b", fontSize: 12 }}>
            {impError}
          </div>
        )}
        {impediments.length === 0 ? (
          <div style={{ color: "#64748b" }}>Nenhum impedimento registrado.</div>
        ) : (
          <div style={{ display: "grid", gap: 10 }}>
            {impediments.map((item) => {
              const sev = IMPEDIMENT_SEVERITY_COLOR[item.severity] ?? { background: "#f1f5f9", color: "#475569" };
              return (
                <div key={item.id} style={{ border: "1px solid #e2e8f0", borderRadius: 10, padding: 14, display: "grid", gap: 8 }}>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                    <strong>{item.title}</strong>
                    <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                      <StatusChip label={IMPEDIMENT_SEVERITY_LABEL[item.severity] ?? item.severity} background={sev.background} color={sev.color} />
                      <StatusChip
                        label={item.isResolved ? "Resolvido" : "Aberto"}
                        background={item.isResolved ? "#dcfce7" : "#fef3c7"}
                        color={item.isResolved ? "#15803d" : "#b45309"}
                      />
                    </div>
                  </div>
                  <div style={{ color: "#475569", fontSize: 13 }}>{item.description}</div>
                  <div style={{ color: "#64748b", fontSize: 12 }}>
                    Categoria: {IMPEDIMENT_CATEGORY_LABEL[item.category] ?? item.category}
                    {item.mitigationPlan ? ` · Mitigação: ${item.mitigationPlan}` : ""}
                  </div>
                  {canOperate && (
                    <div style={{ display: "flex", flexWrap: "wrap", gap: 8, paddingTop: 6, borderTop: "1px solid #f1f5f9" }}>
                      <Button variant="ghost" icon="pi pi-pencil" onClick={() => openImpEdit(item)}>
                        Editar
                      </Button>
                      <Button
                        variant="ghost"
                        icon={item.isResolved ? "pi pi-undo" : "pi pi-check"}
                        onClick={() => void toggleImpediment(item)}
                        disabled={impToggling === item.id}
                      >
                        {item.isResolved ? "Reabrir" : "Marcar resolvido"}
                      </Button>
                      <Button variant="danger" icon="pi pi-trash" onClick={() => setImpDelete(item)}>
                        Remover
                      </Button>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>

      <div style={{ background: "#fff", border: "1px solid #e2e8f0", borderRadius: 12, padding: 20, display: "grid", gap: 12 }}>
        <div style={{ fontWeight: 700, fontSize: 18 }}>Evidências</div>
        {evError && (
          <div role="alert" style={{ padding: 10, borderRadius: 8, background: "#fef2f2", color: "#991b1b", fontSize: 12 }}>
            {evError}
          </div>
        )}
        {canOperate && (
          <div style={{ display: "flex", flexWrap: "wrap", alignItems: "end", gap: 10, paddingBottom: 8, borderBottom: "1px solid #e2e8f0" }}>
            <div style={{ flex: "0 1 220px" }}>
              <label htmlFor="evidence-type" style={{ display: "block", fontSize: 12, color: "#475569", marginBottom: 6 }}>
                Tipo
              </label>
              <select
                id="evidence-type"
                value={evType}
                onChange={(event) => setEvType(event.target.value)}
                style={{ width: "100%", padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
              >
                {EVIDENCE_TYPE_OPTIONS.map((option) => (
                  <option key={option} value={option}>
                    {EVIDENCE_TYPE_LABEL[option] ?? option}
                  </option>
                ))}
              </select>
            </div>
            <div style={{ flex: "1 1 220px" }}>
              <label htmlFor="evidence-description" style={{ display: "block", fontSize: 12, color: "#475569", marginBottom: 6 }}>
                Descrição
              </label>
              <input
                id="evidence-description"
                value={evDescription}
                onChange={(event) => setEvDescription(event.target.value)}
                style={{ width: "100%", padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
              />
            </div>
            <div style={{ flex: "1 1 220px" }}>
              <label htmlFor="evidence-file" style={{ display: "block", fontSize: 12, color: "#475569", marginBottom: 6 }}>
                Arquivo (até 10 MB)
              </label>
              <input
                id="evidence-file"
                type="file"
                onChange={(event) => setEvFile(event.target.files?.[0] ?? null)}
                style={{ fontSize: 12 }}
              />
            </div>
            <Button variant="primary" icon="pi pi-upload" onClick={() => void uploadEvidence()} disabled={evUploading}>
              {evUploading ? "Anexando…" : "Anexar"}
            </Button>
          </div>
        )}
        {evidences.length === 0 ? (
          <div style={{ color: "#64748b" }}>Nenhuma evidência anexada.</div>
        ) : (
          <div style={{ display: "grid", gap: 10 }}>
            {evidences.map((item) => {
              const st = EVIDENCE_STATUS_COLOR[item.status] ?? { background: "#f1f5f9", color: "#475569" };
              return (
                <div key={item.id} style={{ border: "1px solid #e2e8f0", borderRadius: 10, padding: 14, display: "grid", gap: 8 }}>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                    <strong>{item.fileName}</strong>
                    <StatusChip label={EVIDENCE_STATUS_LABEL[item.status] ?? item.status} background={st.background} color={st.color} />
                  </div>
                  <div style={{ color: "#475569", fontSize: 13 }}>
                    {EVIDENCE_TYPE_LABEL[item.type] ?? item.type} · {formatBytes(item.fileSize)}
                    {item.description ? ` · ${item.description}` : ""}
                  </div>
                  {item.status === "Rejected" && item.rejectionReason && (
                    <div style={{ color: "#b91c1c", fontSize: 12 }}>Motivo: {item.rejectionReason}</div>
                  )}
                  {canOperate && (
                    <div style={{ display: "flex", flexWrap: "wrap", gap: 8, paddingTop: 6, borderTop: "1px solid #f1f5f9" }}>
                      <Button
                        variant="ghost"
                        icon="pi pi-download"
                        onClick={() => void downloadEvidence(item)}
                        disabled={evBusy === item.id}
                      >
                        Baixar
                      </Button>
                      {item.status === "Pending" && (
                        <>
                          <Button
                            variant="ghost"
                            icon="pi pi-check"
                            onClick={() => void validateEvidence(item)}
                            disabled={evBusy === item.id}
                          >
                            Validar
                          </Button>
                          <Button
                            variant="ghost"
                            icon="pi pi-times"
                            onClick={() => {
                              setEvRejectReason("");
                              setEvReject(item);
                            }}
                            disabled={evBusy === item.id}
                          >
                            Rejeitar
                          </Button>
                        </>
                      )}
                      <Button variant="danger" icon="pi pi-trash" onClick={() => setEvDelete(item)} disabled={evBusy === item.id}>
                        Remover
                      </Button>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>

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

      {/* ---- Diálogo: editar projeto ---- */}
      <Dialog
        visible={editOpen}
        onHide={() => setEditOpen(false)}
        header="Editar projeto"
        width={520}
        footer={
          <div className="uni-modal-actions">
            <Button variant="ghost" onClick={() => setEditOpen(false)}>Cancelar</Button>
            <Button variant="primary" onClick={() => void saveEditProject()} disabled={editSaving}>
              {editSaving ? "Salvando…" : "Salvar"}
            </Button>
          </div>
        }
      >
        <div style={{ display: "grid", gap: 12 }}>
          <label style={{ display: "grid", gap: 4 }}>
            <span style={{ fontSize: 12, color: "#475569" }}>Título *</span>
            <input
              value={editForm.title}
              onChange={(event) => setEditForm((prev) => ({ ...prev, title: event.target.value }))}
              style={{ padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
            />
          </label>
          <label style={{ display: "grid", gap: 4 }}>
            <span style={{ fontSize: 12, color: "#475569" }}>Descrição *</span>
            <textarea
              value={editForm.description}
              onChange={(event) => setEditForm((prev) => ({ ...prev, description: event.target.value }))}
              rows={3}
              style={{ padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1", resize: "vertical" }}
            />
          </label>
          <label style={{ display: "grid", gap: 4 }}>
            <span style={{ fontSize: 12, color: "#475569" }}>Prioridade</span>
            <select
              value={editForm.priority}
              onChange={(event) => setEditForm((prev) => ({ ...prev, priority: event.target.value }))}
              style={{ padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
            >
              {PRIORITY_OPTIONS.map((option) => (
                <option key={option} value={option}>
                  {PROJECT_PRIORITY_LABEL[option] ?? option}
                </option>
              ))}
            </select>
          </label>
          <label style={{ display: "grid", gap: 4 }}>
            <span style={{ fontSize: 12, color: "#475569" }}>Justificativa</span>
            <textarea
              value={editForm.justification}
              onChange={(event) => setEditForm((prev) => ({ ...prev, justification: event.target.value }))}
              rows={2}
              style={{ padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1", resize: "vertical" }}
            />
          </label>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
            <label style={{ display: "grid", gap: 4 }}>
              <span style={{ fontSize: 12, color: "#475569" }}>Início planejado</span>
              <input
                type="date"
                value={editForm.plannedStartDate}
                onChange={(event) => setEditForm((prev) => ({ ...prev, plannedStartDate: event.target.value }))}
                style={{ padding: "8px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
              />
            </label>
            <label style={{ display: "grid", gap: 4 }}>
              <span style={{ fontSize: 12, color: "#475569" }}>Fim planejado</span>
              <input
                type="date"
                value={editForm.plannedEndDate}
                onChange={(event) => setEditForm((prev) => ({ ...prev, plannedEndDate: event.target.value }))}
                style={{ padding: "8px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
              />
            </label>
          </div>
          {editError && (
            <div role="alert" style={{ padding: 10, borderRadius: 8, background: "#fef2f2", color: "#991b1b", fontSize: 12 }}>
              {editError}
            </div>
          )}
        </div>
      </Dialog>

      {/* ---- Diálogo: impedimento ---- */}
      <Dialog
        visible={impDialog !== null}
        onHide={() => {
          setImpDialog(null);
          setImpEditing(null);
        }}
        header={impDialog === "edit" ? "Editar impedimento" : "Registrar impedimento"}
        width={520}
        footer={
          <div className="uni-modal-actions">
            <Button
              variant="ghost"
              onClick={() => {
                setImpDialog(null);
                setImpEditing(null);
              }}
            >
              Cancelar
            </Button>
            <Button variant="primary" onClick={() => void saveImpediment()} disabled={impSaving}>
              {impSaving ? "Salvando…" : "Salvar"}
            </Button>
          </div>
        }
      >
        <div style={{ display: "grid", gap: 12 }}>
          <label style={{ display: "grid", gap: 4 }}>
            <span style={{ fontSize: 12, color: "#475569" }}>Título *</span>
            <input
              value={impForm.title}
              onChange={(event) => setImpForm((prev) => ({ ...prev, title: event.target.value }))}
              style={{ padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
            />
          </label>
          <label style={{ display: "grid", gap: 4 }}>
            <span style={{ fontSize: 12, color: "#475569" }}>Descrição *</span>
            <textarea
              value={impForm.description}
              onChange={(event) => setImpForm((prev) => ({ ...prev, description: event.target.value }))}
              rows={3}
              style={{ padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1", resize: "vertical" }}
            />
          </label>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
            <label style={{ display: "grid", gap: 4 }}>
              <span style={{ fontSize: 12, color: "#475569" }}>Categoria</span>
              <select
                value={impForm.category}
                onChange={(event) => setImpForm((prev) => ({ ...prev, category: event.target.value }))}
                style={{ padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
              >
                {IMPEDIMENT_CATEGORY_OPTIONS.map((option) => (
                  <option key={option} value={option}>
                    {IMPEDIMENT_CATEGORY_LABEL[option] ?? option}
                  </option>
                ))}
              </select>
            </label>
            <label style={{ display: "grid", gap: 4 }}>
              <span style={{ fontSize: 12, color: "#475569" }}>Severidade</span>
              <select
                value={impForm.severity}
                onChange={(event) => setImpForm((prev) => ({ ...prev, severity: event.target.value }))}
                style={{ padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1" }}
              >
                {IMPEDIMENT_SEVERITY_OPTIONS.map((option) => (
                  <option key={option} value={option}>
                    {IMPEDIMENT_SEVERITY_LABEL[option] ?? option}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <label style={{ display: "grid", gap: 4 }}>
            <span style={{ fontSize: 12, color: "#475569" }}>Plano de mitigação</span>
            <textarea
              value={impForm.mitigationPlan}
              onChange={(event) => setImpForm((prev) => ({ ...prev, mitigationPlan: event.target.value }))}
              rows={2}
              style={{ padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1", resize: "vertical" }}
            />
          </label>
          {impError && (
            <div role="alert" style={{ padding: 10, borderRadius: 8, background: "#fef2f2", color: "#991b1b", fontSize: 12 }}>
              {impError}
            </div>
          )}
        </div>
      </Dialog>

      <ConfirmDialog
        visible={deleteOpen}
        onHide={() => setDeleteOpen(false)}
        onConfirm={() => void confirmDeleteProject()}
        header="Remover projeto"
        message={`Remover o projeto ${project.code} — ${project.title}? Esta ação não pode ser desfeita.`}
        confirmLabel="Remover"
        severity="danger"
      />

      <ConfirmDialog
        visible={impDelete !== null}
        onHide={() => setImpDelete(null)}
        onConfirm={() => void confirmDeleteImpediment()}
        header="Remover impedimento"
        message={`Remover o impedimento "${impDelete?.title ?? ""}"? Esta ação não pode ser desfeita.`}
        confirmLabel="Remover"
        severity="danger"
      />

      <ConfirmDialog
        visible={evDelete !== null}
        onHide={() => setEvDelete(null)}
        onConfirm={() => void confirmDeleteEvidence()}
        header="Remover evidência"
        message={`Remover a evidência "${evDelete?.fileName ?? ""}"? O arquivo também será excluído.`}
        confirmLabel="Remover"
        severity="danger"
      />

      {/* ---- Diálogo: rejeitar evidência ---- */}
      <Dialog
        visible={evReject !== null}
        onHide={() => setEvReject(null)}
        header="Rejeitar evidência"
        width={480}
        footer={
          <div className="uni-modal-actions">
            <Button variant="ghost" onClick={() => setEvReject(null)}>Cancelar</Button>
            <Button variant="danger" onClick={() => void confirmRejectEvidence()} disabled={evBusy === evReject?.id}>
              {evBusy === evReject?.id ? "Rejeitando…" : "Rejeitar"}
            </Button>
          </div>
        }
      >
        <div style={{ display: "grid", gap: 12 }}>
          <div style={{ color: "#475569", fontSize: 13 }}>
            Arquivo: <strong>{evReject?.fileName}</strong>
          </div>
          <label style={{ display: "grid", gap: 4 }}>
            <span style={{ fontSize: 12, color: "#475569" }}>Motivo da rejeição *</span>
            <textarea
              value={evRejectReason}
              onChange={(event) => setEvRejectReason(event.target.value)}
              rows={3}
              style={{ padding: "9px 10px", borderRadius: 8, border: "1px solid #cbd5e1", resize: "vertical" }}
            />
          </label>
        </div>
      </Dialog>
    </div>
  );
}
