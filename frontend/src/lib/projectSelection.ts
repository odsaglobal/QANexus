export const lastProjectStorageKey = 'atip.selectedProjectId';
export const selectedEnvironmentStorageKey = 'atip.selectedEnvironmentId';

export function getStoredProjectId(): string {
  if (typeof window === 'undefined') return '';
  return window.localStorage.getItem(lastProjectStorageKey) ?? '';
}

/** The environment currently selected in the top-bar dropdown (shared across the app). */
export function getSelectedEnvironmentId(): string {
  if (typeof window === 'undefined') return '';
  return window.localStorage.getItem(selectedEnvironmentStorageKey) ?? '';
}

export function setStoredProjectId(projectId: string): void {
  if (typeof window === 'undefined') return;
  window.localStorage.setItem(lastProjectStorageKey, projectId);
}

export function clearStoredProjectId(): void {
  if (typeof window === 'undefined') return;
  window.localStorage.removeItem(lastProjectStorageKey);
}
