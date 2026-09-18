import { apiClient } from '../lib/apiClient';
import type { AiInsights } from './types';

export async function getAiInsights(projectId?: string): Promise<AiInsights> {
  const { data } = await apiClient.get<AiInsights>('/insights', {
    params: projectId ? { projectId } : undefined,
  });
  return data;
}
