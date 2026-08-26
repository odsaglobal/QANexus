import { apiClient } from '../lib/apiClient';
import type { ExplorationSession, JiraIssue, KnowledgeGraph, Scenario, ScenarioRun } from './types';

export interface UpdateScenarioStepInput {
  action: string;
  expectedResult?: string;
}

export interface UpdateScenarioPayload {
  id: string;
  title: string;
  type: string;
  priority: string;
  risk: string;
  preconditions?: string;
  expectedResult?: string;
  jiraKey?: string;
  tags?: string[];
  steps: UpdateScenarioStepInput[];
}

export async function getJiraIssue(projectId: string, issueKey: string): Promise<JiraIssue> {
  const { data } = await apiClient.get<JiraIssue>(
    `/projects/${projectId}/scenarios/jira/${encodeURIComponent(issueKey.trim())}`,
  );
  return data;
}

export interface SyncTestRailPayload {
  projectId: string;
  testRailProjectId: number;
  suiteId?: number;
  sectionId?: number;
}

export interface ManualScenarioStepInput {
  action: string;
  expectedResult?: string;
}

export interface CreateManualScenarioPayload {
  projectId: string;
  featureId: string;
  title: string;
  type?: string;
  priority?: string;
  risk?: string;
  preconditions?: string;
  expectedResult?: string;
  jiraKey?: string;
  tags?: string[];
  steps: ManualScenarioStepInput[];
}

export async function listScenarios(
  projectId: string,
  featureId?: string,
): Promise<Scenario[]> {
  const { data } = await apiClient.get<Scenario[]>(`/projects/${projectId}/scenarios`, {
    params: featureId ? { featureId } : undefined,
  });
  return data;
}

export async function generateScenarios(
  projectId: string,
  featureId: string,
): Promise<Scenario[]> {
  const { data } = await apiClient.post<Scenario[]>(
    `/projects/${projectId}/scenarios/generate`,
    null,
    { params: { featureId } },
  );
  return data;
}

export async function generateScenariosFromStory(
  projectId: string,
  story: string,
): Promise<Scenario[]> {
  const { data } = await apiClient.post<Scenario[]>(
    `/projects/${projectId}/scenarios/generate-from-story`,
    { projectId, story },
  );
  return data;
}

export async function importScenariosFromFile(
  projectId: string,
  featureId: string,
  file: File,
): Promise<Scenario[]> {
  const form = new FormData();
  form.append('featureId', featureId);
  form.append('file', file);

  const { data } = await apiClient.post<Scenario[]>(
    `/projects/${projectId}/scenarios/import`,
    form,
    { headers: { 'Content-Type': 'multipart/form-data' } },
  );

  return data;
}

export async function syncTestRailScenarios(payload: SyncTestRailPayload): Promise<Scenario[]> {
  const { projectId, ...body } = payload;
  const { data } = await apiClient.post<Scenario[]>(
    `/projects/${projectId}/scenarios/sync/testrail`,
    { projectId, ...body },
  );
  return data;
}

export async function createManualScenario(payload: CreateManualScenarioPayload): Promise<Scenario> {
  const { projectId, ...body } = payload;
  const { data } = await apiClient.post<Scenario>(
    `/projects/${projectId}/scenarios/manual`,
    { projectId, ...body },
  );
  return data;
}

export async function updateScenario(
  projectId: string,
  payload: UpdateScenarioPayload,
): Promise<Scenario> {
  const { data } = await apiClient.put<Scenario>(
    `/projects/${projectId}/scenarios/${payload.id}`,
    payload,
  );
  return data;
}

export async function deleteScenario(projectId: string, id: string): Promise<void> {
  await apiClient.delete(`/projects/${projectId}/scenarios/${id}`);
}

export async function exploreScenario(
  projectId: string,
  scenarioId: string,
  environmentId?: string,
): Promise<ExplorationSession> {
  const { data } = await apiClient.post<ExplorationSession>(
    `/projects/${projectId}/scenarios/${scenarioId}/explore`,
    { environmentId },
  );
  return data;
}

export async function runScenario(
  projectId: string,
  scenarioId: string,
  environmentId?: string,
): Promise<ExplorationSession> {
  const { data } = await apiClient.post<ExplorationSession>(
    `/projects/${projectId}/scenarios/${scenarioId}/run`,
    { environmentId },
  );
  return data;
}

export async function applyProposedSteps(projectId: string, scenarioId: string): Promise<Scenario> {
  const { data } = await apiClient.post<Scenario>(`/projects/${projectId}/scenarios/${scenarioId}/proposed-steps/apply`);
  return data;
}

export async function discardProposedSteps(projectId: string, scenarioId: string): Promise<Scenario> {
  const { data } = await apiClient.post<Scenario>(`/projects/${projectId}/scenarios/${scenarioId}/proposed-steps/discard`);
  return data;
}

export async function revertSteps(projectId: string, scenarioId: string): Promise<Scenario> {
  const { data } = await apiClient.post<Scenario>(`/projects/${projectId}/scenarios/${scenarioId}/steps/revert`);
  return data;
}

export async function getLatestScenarioRun(
  projectId: string,
  scenarioId: string,
): Promise<ScenarioRun | null> {
  const res = await apiClient.get<ScenarioRun>(
    `/projects/${projectId}/scenarios/${scenarioId}/run`,
    { validateStatus: (s) => s === 200 || s === 204 },
  );
  return res.status === 204 ? null : res.data;
}

export async function getKnowledgeGraph(projectId: string): Promise<KnowledgeGraph> {
  const { data } = await apiClient.get<KnowledgeGraph>(
    `/projects/${projectId}/knowledge-graph`,
  );
  return data;
}
