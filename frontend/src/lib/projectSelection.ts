export const lastProjectStorageKey = 'atip.selectedProjectId';

export function getStoredProjectId(): string {
  if (typeof window === 'undefined') return '';
  return window.localStorage.getItem(lastProjectStorageKey) ?? '';
}

export function setStoredProjectId(projectId: string): void {
  if (typeof window === 'undefined') return;
  window.localStorage.setItem(lastProjectStorageKey, projectId);
}

export function clearStoredProjectId(): void {
  if (typeof window === 'undefined') return;
  window.localStorage.removeItem(lastProjectStorageKey);
}
