import { apiClient } from '../lib/apiClient';

export interface Tenant {
  id: string;
  name: string;
  slug: string;
  isOnboarded: boolean;
}

/** Renames the caller's workspace (and marks it onboarded). TenantAdmin only. */
export async function updateTenant(name: string): Promise<Tenant> {
  const { data } = await apiClient.put<Tenant>('/tenant', { name });
  return data;
}
