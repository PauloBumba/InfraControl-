import { api } from "./client";

export interface InvitationItem {
  id: string;
  email: string;
  status: "Pending" | "Accepted" | "Rejected" | "Expired" | "Cancelled" | "Activated";
  expiresAt: string;
  createdAt: string;
  createdBy: string;
  roles: string[];
  decidedAt: string | null;
  projectId?: string | null;
  projectCode?: string | null;
  projectTitle?: string | null;
  projectRole?: string | null;
}

export interface PagedInvitations {
  items: InvitationItem[];
  totalCount: number;
  skip: number;
  take: number;
}

export async function listInvitations(params: {
  status?: string;
  email?: string;
  skip?: number;
  take?: number;
}): Promise<PagedInvitations> {
  const queryParams = new URLSearchParams();
  if (params.status) queryParams.append("status", params.status);
  if (params.email) queryParams.append("email", params.email);
  if (params.skip !== undefined) queryParams.append("skip", params.skip.toString());
  if (params.take !== undefined) queryParams.append("take", params.take.toString());

  const query = queryParams.toString();
  const path = query ? `/admin/invitations?${query}` : "/admin/invitations";

  const result = await api.get<PagedInvitations>(path);
  if (!result) {
    return { items: [], totalCount: 0, skip: 0, take: 50 };
  }
  return result;
}

export async function getMyInvitations(): Promise<InvitationItem[]> {
  const result = await api.get<InvitationItem[]>("/invitations/me");
  return result || [];
}

export async function acceptInvitation(invitationId: string): Promise<void> {
  await api.post(`/invitations/${invitationId}/accept`);
}

export async function rejectInvitation(invitationId: string): Promise<void> {
  await api.post(`/invitations/${invitationId}/reject`);
}

/** Convite por id (GET /admin/invitations/{id}; Administer). */
export async function getInvitation(invitationId: string): Promise<InvitationItem | null> {
  return api.get<InvitationItem>(`/admin/invitations/${invitationId}`);
}

/**
 * Atualiza as funções do convite (PUT /admin/invitations/{id}; Administer).
 * Só pendente no backend (409 pt-BR caso contrário); lista não vazia (400).
 */
export async function updateInvitationRoles(
  invitationId: string,
  roles: string[],
): Promise<InvitationItem | null> {
  return api.put<InvitationItem>(`/admin/invitations/${invitationId}`, { roles });
}

/** Cancela convite (DELETE /admin/invitations/{id}; Administer; só pendente). */
export async function cancelInvitation(invitationId: string): Promise<null> {
  return api.delete<null>(`/admin/invitations/${invitationId}`);
}
