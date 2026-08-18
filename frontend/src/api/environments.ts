import { apiClient } from '../lib/apiClient';
import type { CreateEnvironmentRequest, EnvironmentModel } from './types';

export async function listEnvironments(projectId: string): Promise<EnvironmentModel[]> {
  const { data } = await apiClient.get<EnvironmentModel[]>(
    `/projects/${projectId}/environments`,
  );
  return data;
}

export async function createEnvironment(
  payload: CreateEnvironmentRequest,
): Promise<EnvironmentModel> {
  const { data } = await apiClient.post<EnvironmentModel>(
    `/projects/${payload.projectId}/environments`,
    payload,
  );
  return data;
}

export async function deleteEnvironment(projectId: string, id: string): Promise<void> {
  await apiClient.delete(`/projects/${projectId}/environments/${id}`);
}
