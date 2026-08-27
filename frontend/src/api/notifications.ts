import { apiClient } from '../lib/apiClient';

export interface AppNotification {
  id: string;
  title: string;
  message: string;
  level: 'info' | 'success' | 'warning' | 'error' | string;
  category: string;
  entityType?: string | null;
  entityId?: string | null;
  isRead: boolean;
  createdAtUtc: string;
}

export async function listNotifications(take = 30): Promise<AppNotification[]> {
  const { data } = await apiClient.get<AppNotification[]>('/notifications', { params: { take } });
  return data;
}

export async function getUnreadCount(): Promise<number> {
  const { data } = await apiClient.get<number>('/notifications/unread-count');
  return data;
}

export async function markNotificationRead(id: string): Promise<void> {
  await apiClient.post(`/notifications/${id}/read`);
}

export async function markAllNotificationsRead(): Promise<void> {
  await apiClient.post('/notifications/read-all');
}
