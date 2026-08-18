import { apiClient } from '../lib/apiClient';
import { useAuthStore } from '../store/authStore';
import type {
  DiscoveredElement,
  DiscoveredPage,
  ExplorationSession,
  PagedResult,
  StartExplorationRequest,
} from './types';

// ----- Sessions -----

export async function listExplorationSessions(projectId: string): Promise<ExplorationSession[]> {
  const { data } = await apiClient.get<ExplorationSession[]>(
    `/projects/${projectId}/explorer/sessions`,
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

export async function listDiscoveredElements(
  projectId: string,
  params?: { pageId?: string; page?: number; pageSize?: number; search?: string },
): Promise<PagedResult<DiscoveredElement>> {
  const { data } = await apiClient.get<PagedResult<DiscoveredElement>>(
    `/projects/${projectId}/explorer/elements`,
    { params },
  );
  return data;
}

/** Returns the full URL to serve a stored file (screenshot, DOM snapshot).
 *  The token is appended as a query param because <img>/<a> requests can't send a bearer header. */
export function fileUrl(path: string): string {
  const token = useAuthStore.getState().accessToken;
  const base = `/api/v1/files/${path}`;
  return token ? `${base}?access_token=${encodeURIComponent(token)}` : base;
}
