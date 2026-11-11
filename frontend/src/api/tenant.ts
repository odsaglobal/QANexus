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

export interface TenantSettings {
  failedTestRetries: number;
}

export async function getTenantSettings(): Promise<TenantSettings> {
  const { data } = await apiClient.get<TenantSettings>('/tenant/settings');
  return data;
}

/** TenantAdmin only. */
export async function updateTenantSettings(settings: TenantSettings): Promise<TenantSettings> {
  const { data } = await apiClient.put<TenantSettings>('/tenant/settings', settings);
  return data;
}
