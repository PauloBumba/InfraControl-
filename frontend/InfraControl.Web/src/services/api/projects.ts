import { api } from "./client";

export interface ProjectMemberItem {
  userId: string;
  role: string;
}

export interface ProjectItem {
  id: string;
  code: string;
  title: string;
  description: string;
  justification: string | null;
  priority: string;
  status: string;
  leaderId: string;
  managerId: string | null;
  adminApproverId: string | null;
  approvalNotes: string | null;
  approvedAt: string | null;
  plannedStartDate: string | null;
  plannedEndDate: string | null;
  actualStartDate: string | null;
  actualEndDate: string | null;
  members: ProjectMemberItem[];
  createdById: string | null;
}

export interface ProjectPhaseItem {
  id: string;
  projectId: string;
  title: string;
  description: string | null;
  order: number;
  responsibleId: string | null;
  plannedStartDate: string | null;
  plannedEndDate: string | null;
  actualStartDate: string | null;
  actualEndDate: string | null;
  completionCriteria: string | null;
  status: string;
  percent: number;
}

export interface ProjectInvitationResult {
  invitationId: string;
  accountExists: boolean;
  requiresActivation: boolean;
}

export interface PagedProjects {
  items: ProjectItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ProjectFilters {
  term?: string;
  status?: string;
  priority?: string;
  page?: number;
  pageSize?: number;
  leaderId?: string;
}

export async function listProjects(filters: ProjectFilters = {}): Promise<PagedProjects> {
  const params = new URLSearchParams();
  if (filters.term) params.set("term", filters.term);
  if (filters.status) params.set("status", filters.status);
  if (filters.priority) params.set("priority", filters.priority);
  if (filters.leaderId) params.set("leaderId", filters.leaderId);
  if (filters.page) params.set("page", String(filters.page));
  if (filters.pageSize) params.set("pageSize", String(filters.pageSize));

  const query = params.toString();
  const data = await api.get<PagedProjects>(`/projects${query ? `?${query}` : ""}`);
  return data ?? { items: [], totalCount: 0, page: 1, pageSize: 20 };
}

export async function getProject(id: string): Promise<ProjectItem | null> {
  return api.get<ProjectItem>(`/projects/${id}`);
}

export async function createProject(input: {
  code: string;
  title: string;
  description: string;
  leaderId: string;
  priority?: string;
  justification?: string | null;
  managerId?: string | null;
  plannedStartDate?: string | null;
  plannedEndDate?: string | null;
}): Promise<ProjectItem | null> {
  return api.post<ProjectItem>("/projects", {
    code: input.code,
    title: input.title,
    description: input.description,
    leaderId: input.leaderId,
    priority: input.priority || "Medium",
    justification: input.justification ?? null,
    managerId: input.managerId ?? null,
    plannedStartDate: input.plannedStartDate ?? null,
    plannedEndDate: input.plannedEndDate ?? null,
  });
}

export async function updateProject(
  id: string,
  input: {
    title: string;
    description: string;
    justification?: string | null;
    priority?: string;
    leaderId?: string;
    managerId?: string | null;
    plannedStartDate?: string | null;
    plannedEndDate?: string | null;
  },
): Promise<ProjectItem | null> {
  return api.put<ProjectItem>(`/projects/${id}`, {
    title: input.title,
    description: input.description,
    justification: input.justification ?? null,
    priority: input.priority || undefined,
    leaderId: input.leaderId || undefined,
    managerId: input.managerId ?? null,
    plannedStartDate: input.plannedStartDate ?? null,
    plannedEndDate: input.plannedEndDate ?? null,
  });
}

export async function listProjectPhases(projectId: string): Promise<ProjectPhaseItem[]> {
  const data = await api.get<ProjectPhaseItem[]>(`/projects/${projectId}/phases`);
  return data ?? [];
}

export async function submitProjectForApproval(projectId: string): Promise<ProjectItem | null> {
  return api.post<ProjectItem>(`/projects/${projectId}/submit-approval`);
}

export async function approveProject(projectId: string, approvalNotes: string): Promise<ProjectItem | null> {
  return api.post<ProjectItem>(`/projects/${projectId}/approve`, { approvalNotes });
}

export async function createProjectInvitation(
  projectId: string,
  input: { email: string; role: number },
): Promise<ProjectInvitationResult | null> {
  return api.post<ProjectInvitationResult>(`/projects/${projectId}/invitations`, {
    email: input.email,
    role: input.role,
  });
}

export async function generateProjectKnowledge(projectId: string): Promise<unknown> {
  return api.post<unknown>(`/projects/${projectId}/generate-knowledge`);
}
