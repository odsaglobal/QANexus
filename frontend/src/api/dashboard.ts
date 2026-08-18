import { apiClient } from '../lib/apiClient';
import type { DashboardSummary } from './types';

export async function getDashboardSummary(): Promise<DashboardSummary> {
  const { data } = await apiClient.get<DashboardSummary>('/dashboard/summary');
  return data;
}
