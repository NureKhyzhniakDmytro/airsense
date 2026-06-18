import type { Pagination } from '@/types/api';

export interface Environment {
  id: number;
  name: string;
  role: string;
  icon: string;
  description?: string | null;
}

export interface EnvironmentsResponse {
  data: Environment[];
  pagination: Pagination;
}

export interface CreateEnvironmentPayload {
  name: string;
  icon: string;
} 

export interface UpdateEnvironmentPayload {
  name: string;
  icon: string;
}
