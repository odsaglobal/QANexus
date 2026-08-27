import { apiClient } from '../lib/apiClient';

export interface ApiKey {
  id: string;
  name: string;
  prefix: string;
  lastUsedAtUtc?: string | null;
  expiresAtUtc?: string | null;
  revokedAtUtc?: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CreatedApiKey {
  key: ApiKey;
  plainText: string;
}

export async function listApiKeys(): Promise<ApiKey[]> {
  const { data } = await apiClient.get<ApiKey[]>('/api-keys');
  return data;
}

export async function createApiKey(name: string): Promise<CreatedApiKey> {
  const { data } = await apiClient.post<CreatedApiKey>('/api-keys', { name });
  return data;
}

export async function revokeApiKey(id: string): Promise<void> {
  await apiClient.delete(`/api-keys/${id}`);
}
