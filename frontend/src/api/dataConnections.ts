import { apiClient } from '../lib/apiClient';

export type DataProviderKind = 'PostgreSql' | 'SqlServer' | 'MySql';

export interface DataConnection {
  id: string;
  projectId: string;
  environmentId: string;
  name: string;
  provider: DataProviderKind;
  commandTimeoutSeconds: number;
  readOnly: boolean;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface CreateDataConnectionPayload {
  projectId: string;
  environmentId: string;
  name: string;
  provider: DataProviderKind;
  connectionString: string;
  commandTimeoutSeconds?: number;
  readOnly?: boolean;
}

export interface UpdateDataConnectionPayload {
  id: string;
  projectId: string;
  environmentId: string;
  name: string;
  provider: DataProviderKind;
  /** Omit to keep the stored secret — the API never returns it. */
  connectionString?: string;
  commandTimeoutSeconds?: number;
  readOnly?: boolean;
}

function baseUrl(projectId: string, environmentId: string): string {
  return `/projects/${projectId}/environments/${environmentId}/data-connections`;
}

export async function listDataConnections(
  projectId: string,
  environmentId: string,
): Promise<DataConnection[]> {
  const { data } = await apiClient.get<DataConnection[]>(baseUrl(projectId, environmentId));
  return data;
}

export async function createDataConnection(
  payload: CreateDataConnectionPayload,
): Promise<DataConnection> {
  const { data } = await apiClient.post<DataConnection>(
    baseUrl(payload.projectId, payload.environmentId),
    payload,
  );
  return data;
}

export async function updateDataConnection(
  payload: UpdateDataConnectionPayload,
): Promise<DataConnection> {
  const { data } = await apiClient.put<DataConnection>(
    `${baseUrl(payload.projectId, payload.environmentId)}/${payload.id}`,
    payload,
  );
  return data;
}

export async function deleteDataConnection(
  projectId: string,
  environmentId: string,
  id: string,
): Promise<void> {
  await apiClient.delete(`${baseUrl(projectId, environmentId)}/${id}`);
}
