import { apiClient } from '../lib/apiClient';

export interface TestFile {
  name: string;
  sizeBytes: number;
  updatedAtUtc: string;
}

export async function listTestFiles(projectId: string): Promise<TestFile[]> {
  const { data } = await apiClient.get<TestFile[]>(`/projects/${projectId}/test-files`);
  return data;
}

export async function uploadTestFile(projectId: string, file: File): Promise<TestFile> {
  const form = new FormData();
  form.append('file', file);
  const { data } = await apiClient.post<TestFile>(`/projects/${projectId}/test-files`, form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return data;
}

export async function deleteTestFile(projectId: string, name: string): Promise<void> {
  await apiClient.delete(`/projects/${projectId}/test-files/${encodeURIComponent(name)}`);
}
