import type { Pagination } from "@/types/api";

export interface UserNotification {
  id: number;
  title: string;
  body: string;
  severity: string;
  data?: Record<string, string> | null;
  created_at: number;
  read_at?: number | null;
  is_read: boolean;
}

export interface NotificationsResponse {
  data: UserNotification[];
  pagination: Pagination;
}

export interface UnreadNotificationsResponse {
  unread_count: number;
}
