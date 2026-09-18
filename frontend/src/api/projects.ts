import { apiClient } from '../lib/apiClient';
import type {
  CreateProjectRequest,
  PagedResult,
  Project,
  UpdateProjectRequest,
} from './types';

export interface ListProjectsParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
  sortBy?: string;
  sortDescending?: boolean;
}

export async function listProjects(params: ListProjectsParams): Promise<PagedResult<Project>> {
  const { data } = await apiClient.get<PagedResult<Project>>('/projects', { params });
  return data;
}

export async function getProject(id: string): Promise<Project> {
  const { data } = await apiClient.get<Project>(`/projects/${id}`);
  return data;
}

export async function createProject(payload: CreateProjectRequest): Promise<Project> {
  const { data } = await apiClient.post<Project>('/projects', payload);
  return data;
}

export async function updateProject(payload: UpdateProjectRequest): Promise<Project> {
  const { data } = await apiClient.put<Project>(`/projects/${payload.id}`, payload);
  return data;
}

export async function deleteProject(id: string): Promise<void> {
  await apiClient.delete(`/projects/${id}`);
}

export interface ProjectMember {
  userId: string;
  email: string;
  displayName: string;
  role: string;
}

export async function listProjectMembers(projectId: string): Promise<ProjectMember[]> {
  const { data } = await apiClient.get<ProjectMember[]>(`/projects/${projectId}/members`);
  return data;
}

export async function addProjectMember(projectId: string, userId: string, role: string): Promise<ProjectMember> {
  const { data } = await apiClient.post<ProjectMember>(`/projects/${projectId}/members`, { userId, role });
  return data;
}

export async function updateProjectMemberRole(projectId: string, userId: string, role: string): Promise<ProjectMember> {
  const { data } = await apiClient.put<ProjectMember>(`/projects/${projectId}/members/${userId}`, { role });
  return data;
}

export async function removeProjectMember(projectId: string, userId: string): Promise<void> {
  await apiClient.delete(`/projects/${projectId}/members/${userId}`);
}
