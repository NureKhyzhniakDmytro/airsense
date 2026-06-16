import { Pagination } from "@/types/api";
import type { Parameter } from '@/types/sensor';

export interface Room {
  id: number;
  name: string;
  parameters: Parameter[] | null;
  device_speed: number | null;
}

export interface RoomsResponse {
  data: Room[];
  pagination: Pagination;
}

export interface CreateRoomPayload {
  name: string;
}

export interface UpdateRoomPayload {
  name: string;
} 