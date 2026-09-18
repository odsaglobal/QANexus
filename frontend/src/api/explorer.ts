import { apiClient } from '../lib/apiClient';
import { useAuthStore } from '../store/authStore';
import type {
  DiscoveredPage,
  ExplorationSession,
  SessionStepResult,
  StartExplorationRequest,
} from './types';

// ----- Sessions -----

export async function listExplorationSessions(
  projectId: string,
  opts?: { activeOnly?: boolean },
): Promise<ExplorationSession[]> {
  const { data } = await apiClient.get<ExplorationSession[]>(
    `/projects/${projectId}/explorer/sessions`,
    { params: opts?.activeOnly ? { activeOnly: true } : undefined },
  );
  return data;
}

export async function getExplorationSession(
  projectId: string,
  sessionId: string,
): Promise<ExplorationSession> {
  const { data } = await apiClient.get<ExplorationSession>(
    `/projects/${projectId}/explorer/sessions/${sessionId}`,
  );
  return data;
}

export async function startExploration(payload: StartExplorationRequest): Promise<ExplorationSession> {
  const { data } = await apiClient.post<ExplorationSession>(
    `/projects/${payload.projectId}/explorer/sessions`,
    payload,
  );
  return data;
}

export async function cancelExploration(projectId: string, sessionId: string): Promise<void> {
  await apiClient.post(`/projects/${projectId}/explorer/sessions/${sessionId}/cancel`);
}

/** Full persisted step-by-step execution trace for a session (screenshots + validation detail),
 *  across every scenario it ran — works for completed sessions, not just live ones. */
export async function listSessionSteps(
  projectId: string,
  sessionId: string,
): Promise<SessionStepResult[]> {
  const { data } = await apiClient.get<SessionStepResult[]>(
    `/projects/${projectId}/explorer/sessions/${sessionId}/steps`,
  );
  return data;
}

// ----- Pages -----

export async function listDiscoveredPages(
  projectId: string,
  sessionId: string,
): Promise<DiscoveredPage[]> {
  const { data } = await apiClient.get<DiscoveredPage[]>(
    `/projects/${projectId}/explorer/sessions/${sessionId}/pages`,
  );
  return data;
}

// ----- Elements -----

/** Returns the full URL to serve a stored file (screenshot, DOM snapshot).
 *  The token is appended as a query param because <img>/<a> requests can't send a bearer header. */
export function fileUrl(path: string): string {
  const token = useAuthStore.getState().accessToken;
  const base = `/api/v1/files/${path}`;
  return token ? `${base}?access_token=${encodeURIComponent(token)}` : base;
}
