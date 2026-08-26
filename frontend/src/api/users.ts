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
}

export async function listUsers(): Promise<WorkspaceUser[]> {
  const { data } = await apiClient.get<WorkspaceUser[]>('/users');
  return data;
}

export async function syncMyProfile(profile: { email?: string; name?: string }): Promise<WorkspaceUser> {
  const { data } = await apiClient.post<WorkspaceUser>('/users/me/sync', profile);
  return data;
}
