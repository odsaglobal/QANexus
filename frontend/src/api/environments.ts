import { apiClient } from '../lib/apiClient';
import type { CreateEnvironmentRequest, EnvironmentModel, EnvironmentType, EnvironmentVariable } from './types';

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

export interface UpdateEnvironmentRequest {
  id: string;
  projectId: string;
  name: string;
  type: EnvironmentType;
  baseUrl: string;
  isDefault: boolean;
}

export async function updateEnvironment(
  payload: UpdateEnvironmentRequest,
): Promise<EnvironmentModel> {
  const { data } = await apiClient.put<EnvironmentModel>(
    `/projects/${payload.projectId}/environments/${payload.id}`,
    payload,
  );
  return data;
}

export async function deleteEnvironment(projectId: string, id: string): Promise<void> {
  await apiClient.delete(`/projects/${projectId}/environments/${id}`);
}

export async function getEnvironmentVariables(
  projectId: string,
  environmentId: string,
): Promise<EnvironmentVariable[]> {
  const { data } = await apiClient.get<EnvironmentVariable[]>(
    `/projects/${projectId}/environments/${environmentId}/variables`,
  );
  return data;
}

export async function updateEnvironmentVariables(
  projectId: string,
  environmentId: string,
  variables: EnvironmentVariable[],
): Promise<EnvironmentVariable[]> {
  const { data } = await apiClient.put<EnvironmentVariable[]>(
    `/projects/${projectId}/environments/${environmentId}/variables`,
    { projectId, environmentId, variables },
  );
  return data;
}
