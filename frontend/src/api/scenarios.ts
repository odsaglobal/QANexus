import { apiClient } from '../lib/apiClient';
import type {
  ExplorationSession,
  JiraIssue,
  KnowledgeGraph,
  Scenario,
  ScenarioRun,
  TestPlatform,
} from './types';

export interface UpdateScenarioStepInput {
  action: string;
  expectedResult?: string;
  /** Defaults to Web when omitted. */
  platform?: TestPlatform;
  /** Engine verb for an authored (non-recorded) step, e.g. 'request' or 'query'. Required for non-Web steps. */
  kind?: string;
  target?: string;
  value?: string;
  options?: Record<string, string | null>;
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
  autoHealEnabled?: boolean;
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

export type ImportSuiteMode = 'None' | 'Existing' | 'New';

export interface ImportSuiteOptions {
  mode: ImportSuiteMode;
  /** Required when mode is 'Existing'. */
  suiteId?: string;
  /** Required when mode is 'New'. */
  newSuiteName?: string;
}

export interface ImportScenariosResult {
  scenarios: Scenario[];
  suiteId: string | null;
  suiteName: string | null;
  suiteCreated: boolean;
}

export async function importScenariosFromFile(
  projectId: string,
  featureId: string,
  file: File,
  suite: ImportSuiteOptions = { mode: 'None' },
): Promise<ImportScenariosResult> {
  const form = new FormData();
  form.append('featureId', featureId);
  form.append('file', file);
  form.append('suiteMode', suite.mode);
  if (suite.mode === 'Existing' && suite.suiteId) {
    form.append('suiteId', suite.suiteId);
  }
  if (suite.mode === 'New' && suite.newSuiteName) {
    form.append('newSuiteName', suite.newSuiteName);
  }

  const { data } = await apiClient.post<ImportScenariosResult>(
    `/projects/${projectId}/scenarios/import`,
    form,
    { headers: { 'Content-Type': 'multipart/form-data' } },
  );

  return data;
}

/**
 * Downloads the .xlsx import template and hands it to the browser as a file save.
 * Fetched through apiClient so the request carries auth, then released as an object URL.
 */
export async function downloadImportTemplate(projectId: string): Promise<void> {
  const { data } = await apiClient.get<Blob>(
    `/projects/${projectId}/scenarios/import-template`,
    { responseType: 'blob' },
  );

  const url = URL.createObjectURL(data);
  try {
    const link = document.createElement('a');
    link.href = url;
    link.download = 'atip-test-case-import-template.xlsx';
    document.body.appendChild(link);
    link.click();
    link.remove();
  } finally {
    URL.revokeObjectURL(url);
  }
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

/** Bulk soft-delete. POST, not DELETE, because the ids travel in the body. Returns how many were removed. */
export async function deleteScenarios(projectId: string, ids: string[]): Promise<number> {
  const { data } = await apiClient.post<{ deleted: number }>(
    `/projects/${projectId}/scenarios/bulk-delete`,
    { projectId, ids },
  );
  return data.deleted;
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
