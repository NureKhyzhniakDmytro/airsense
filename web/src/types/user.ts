import { Pagination } from "@/types/api";

export interface User {
  id: number;
  email: string;
  role: string;
  name: string;
}

export interface RegisterPayload {
  notification_token: string;
}

export interface UsersResponse {
  data: User[];
  pagination: Pagination;
} 