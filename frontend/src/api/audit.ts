import { apiClient } from '../lib/apiClient';
import type { PagedResult } from './types';

export interface AuditLogEntry {
  id: string;
  userId?: string | null;
  userEmail?: string | null;
  action: string;
  category: string;
  entityType?: string | null;
  entityId?: string | null;
  summary: string;
  ipAddress?: string | null;
  timestampUtc: string;
}

export async function listAuditLogs(
  params: { page?: number; pageSize?: number; category?: string } = {},
): Promise<PagedResult<AuditLogEntry>> {
  const { data } = await apiClient.get<PagedResult<AuditLogEntry>>('/audit', { params });
  return data;
}
