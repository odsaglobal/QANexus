import { apiClient } from '../lib/apiClient';

export interface SecretStoreStatus {
  provider: string;
  configured: boolean;
  reachable: boolean;
  authenticated: boolean;
  detail: string | null;
}

export interface SecretReferenceCheck {
  ok: boolean;
  error: string | null;
}

export async function getSecretStoreStatus(): Promise<SecretStoreStatus> {
  const { data } = await apiClient.get<SecretStoreStatus>('/secret-store/status');
  return data;
}

/** Checks a `path#field` reference resolves; the value itself is never returned. */
export async function verifySecretReference(reference: string): Promise<SecretReferenceCheck> {
  const { data } = await apiClient.post<SecretReferenceCheck>('/secret-store/verify', { reference });
  return data;
}
