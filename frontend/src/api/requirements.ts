import { apiClient } from '../lib/apiClient';
import type { Requirement, RequirementDetail } from './types';

export interface ManualFeature {
  requirementId: string;
  moduleId: string;
  featureId: string;
  featureName: string;
  moduleName: string;
}

export async function createManualFeature(
  projectId: string,
  payload: { featureName: string; moduleName?: string; description?: string },
): Promise<ManualFeature> {
  const { data } = await apiClient.post<ManualFeature>(
    `/projects/${projectId}/requirements/manual-feature`,
    { projectId, ...payload },
  );
  return data;
}

export async function listRequirements(projectId: string): Promise<Requirement[]> {
  const { data } = await apiClient.get<Requirement[]>(
    `/projects/${projectId}/requirements`,
  );
  return data;
}

export async function getRequirement(
  projectId: string,
  id: string,
): Promise<RequirementDetail> {
  const { data } = await apiClient.get<RequirementDetail>(
    `/projects/${projectId}/requirements/${id}`,
  );
  return data;
}

export async function uploadRequirement(
  projectId: string,
  name: string,
  file: File,
): Promise<Requirement> {
  const form = new FormData();
  form.append('name', name);
  form.append('file', file);
  const { data } = await apiClient.post<Requirement>(
    `/projects/${projectId}/requirements`,
    form,
    { headers: { 'Content-Type': 'multipart/form-data' } },
  );
  return data;
}

export async function analyzeRequirement(
  projectId: string,
  id: string,
): Promise<RequirementDetail> {
  const { data } = await apiClient.post<RequirementDetail>(
    `/projects/${projectId}/requirements/${id}/analyze`,
  );
  return data;
}

export async function deleteRequirement(projectId: string, id: string): Promise<void> {
  await apiClient.delete(`/projects/${projectId}/requirements/${id}`);
}
