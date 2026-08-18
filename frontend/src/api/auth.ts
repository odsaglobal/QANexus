import { apiClient } from '../lib/apiClient';
import type { AuthResult, LoginRequest, RegisterRequest } from './types';

export async function register(payload: RegisterRequest): Promise<AuthResult> {
  const { data } = await apiClient.post<AuthResult>('/auth/register', payload);
  return data;
}

export async function login(payload: LoginRequest): Promise<AuthResult> {
  const { data } = await apiClient.post<AuthResult>('/auth/login', payload);
  return data;
}
