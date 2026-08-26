import { apiClient } from '../lib/apiClient';
import type { TestDataRow, TestDataSet } from './types';

export interface SaveTestDataSetPayload {
  projectId: string;
  environmentId: string;
  name: string;
  description?: string;
  columns: string[];
  rows: TestDataRow[];
}

export async function listTestDataSets(
  projectId: string,
  environmentId: string,
): Promise<TestDataSet[]> {
  const { data } = await apiClient.get<TestDataSet[]>(
    `/projects/${projectId}/environments/${environmentId}/test-data`,
  );
  return data;
}

export async function createTestDataSet(payload: SaveTestDataSetPayload): Promise<TestDataSet> {
  const { projectId, environmentId, ...body } = payload;
  const { data } = await apiClient.post<TestDataSet>(
    `/projects/${projectId}/environments/${environmentId}/test-data`,
    { projectId, environmentId, ...body },
  );
  return data;
}

export async function updateTestDataSet(
  id: string,
  payload: SaveTestDataSetPayload,
): Promise<TestDataSet> {
  const { projectId, environmentId, ...body } = payload;
  const { data } = await apiClient.put<TestDataSet>(
    `/projects/${projectId}/environments/${environmentId}/test-data/${id}`,
    { id, projectId, environmentId, ...body },
  );
  return data;
}

export async function deleteTestDataSet(
  projectId: string,
  environmentId: string,
  id: string,
): Promise<void> {
  await apiClient.delete(`/projects/${projectId}/environments/${environmentId}/test-data/${id}`);
}
