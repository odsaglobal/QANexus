import { apiClient } from '../lib/apiClient';
import type { ExplorationSession, TestSuite } from './types';

export interface CreateTestSuitePayload {
  projectId: string;
  name: string;
  description?: string;
  scenarioIds: string[];
}

export interface UpdateTestSuitePayload {
  id: string;
  projectId: string;
  name: string;
  description?: string;
  scenarioIds: string[];
}

export async function listTestSuites(projectId: string): Promise<TestSuite[]> {
  const { data } = await apiClient.get<TestSuite[]>(`/projects/${projectId}/suites`);
  return data;
}

export async function getTestSuite(projectId: string, id: string): Promise<TestSuite> {
  const { data } = await apiClient.get<TestSuite>(`/projects/${projectId}/suites/${id}`);
  return data;
}

export async function createTestSuite(payload: CreateTestSuitePayload): Promise<TestSuite> {
  const { projectId, ...body } = payload;
  const { data } = await apiClient.post<TestSuite>(`/projects/${projectId}/suites`, {
    projectId,
    ...body,
  });
  return data;
}

export async function updateTestSuite(payload: UpdateTestSuitePayload): Promise<TestSuite> {
  const { projectId, id, ...body } = payload;
  const { data } = await apiClient.put<TestSuite>(`/projects/${projectId}/suites/${id}`, {
    id,
    projectId,
    ...body,
  });
  return data;
}

export async function deleteTestSuite(projectId: string, id: string): Promise<void> {
  await apiClient.delete(`/projects/${projectId}/suites/${id}`);
}

/** Narrows a suite run. Each facet matches ANY of its values; the facets combine with AND. */
export interface SuiteRunFilter {
  tags?: string[];
  types?: string[];
  priorities?: string[];
  /** Explicitly checked scenarios, AND-ed with the facets. Omit to run every match. */
  scenarioIds?: string[];
}

export async function runTestSuite(
  projectId: string,
  suiteId: string,
  environmentId?: string,
  filter?: SuiteRunFilter,
): Promise<ExplorationSession> {
  const { data } = await apiClient.post<ExplorationSession>(
    `/projects/${projectId}/suites/${suiteId}/run`,
    {
      environmentId,
      tags: filter?.tags?.length ? filter.tags : undefined,
      types: filter?.types?.length ? filter.types : undefined,
      priorities: filter?.priorities?.length ? filter.priorities : undefined,
      scenarioIds: filter?.scenarioIds?.length ? filter.scenarioIds : undefined,
    },
  );
  return data;
}
