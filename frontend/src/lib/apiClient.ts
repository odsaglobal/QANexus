import axios, { AxiosError } from 'axios';
import type { ProblemDetails } from '../api/types';
import { useAuthStore } from '../store/authStore';

/**
 * Single axios instance for the whole app. A request interceptor attaches the
 * bearer token; a response interceptor signs the user out on 401 so expired
 * sessions redirect to login cleanly.
 */
export const apiClient = axios.create({
  baseURL: '/api/v1',
  headers: { 'Content-Type': 'application/json' },
});

apiClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ProblemDetails>) => {
    if (error.response?.status === 401) {
      useAuthStore.getState().clear();
    }
    return Promise.reject(error);
  },
);

/** Extracts a human-readable message from an axios/ProblemDetails error. */
export function getErrorMessage(error: unknown): string {
  if (axios.isAxiosError<ProblemDetails>(error)) {
    const data = error.response?.data;
    if (data?.errors) {
      return Object.values(data.errors).flat().join(' ');
    }
    return data?.detail ?? data?.title ?? error.message;
  }
  return error instanceof Error ? error.message : 'An unexpected error occurred.';
}
