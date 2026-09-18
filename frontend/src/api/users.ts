import { apiClient } from '../lib/apiClient';

export interface WorkspaceUser {
  id: string;
  email: string;
  displayName: string;
  role: string;
  isActive: boolean;
  isFederated: boolean;
  lastLoginAtUtc?: string | null;
  createdAtUtc: string;
  tenantId?: string;
  tenantName?: string | null;
  tenantSlug?: string | null;
  tenantIsOnboarded?: boolean;
}

export async function listUsers(): Promise<WorkspaceUser[]> {
  const { data } = await apiClient.get<WorkspaceUser[]>('/users');
  return data;
}

export async function getMyProfile(): Promise<WorkspaceUser> {
  const { data } = await apiClient.get<WorkspaceUser>('/users/me');
  return data;
}

export async function syncMyProfile(profile: { email?: string; name?: string }): Promise<WorkspaceUser> {
  const { data } = await apiClient.post<WorkspaceUser>('/users/me/sync', profile);
  return data;
}

/** Updates the signed-in user's display name. */
export async function updateMyDisplayName(name: string): Promise<WorkspaceUser> {
  const { data } = await apiClient.put<WorkspaceUser>('/users/me', { displayName: name });
  return data;
}

/** Changes a workspace member's system role (Member ↔ TenantAdmin). TenantAdmin only. */
export async function changeUserSystemRole(userId: string, role: string): Promise<WorkspaceUser> {
  const { data } = await apiClient.put<WorkspaceUser>(`/users/${userId}/role`, { role });
  return data;
}
