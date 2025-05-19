import type { Pagination } from '@/types/api';

export interface Environment {
  id: number;
  name: string;
  role: string;
}

export interface EnvironmentsResponse {
  data: Environment[];
  pagination: Pagination;
}

export interface CreateEnvironmentPayload {
  name: string;
} 

export interface UpdateEnvironmentPayload {
  name: string;
}
