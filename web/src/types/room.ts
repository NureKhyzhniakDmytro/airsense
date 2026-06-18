import type { Pagination } from "@/types/api";
import type { Parameter } from '@/types/sensor';

export interface Room {
  id: number;
  name: string;
  icon: string;
  parameters: Parameter[] | null;
  device_speed: number | null;
}

export interface RoomsResponse {
  data: Room[];
  pagination: Pagination;
}

export interface CreateRoomPayload {
  name: string;
  icon: string;
}

export interface UpdateRoomPayload {
  name: string;
  icon: string;
}

export type RoomLayoutItemType =
  | "sensor"
  | "vent"
  | "door"
  | "window"
  | "desk"
  | "equipment"
  | "zone"
  | "obstacle";

export type RoomLayoutGeometryType =
  | "rectangle"
  | "l_shape"
  | "t_shape"
  | "custom";

export interface RoomLayoutPoint {
  x: number;
  y: number;
}

export interface RoomLayoutGeometry {
  type: RoomLayoutGeometryType | string;
  points: RoomLayoutPoint[];
}

export interface RoomLayoutItem {
  id: string;
  type: RoomLayoutItemType | string;
  label?: string | null;
  sensor_id?: number | null;
  device_id?: number | null;
  serial_number?: string | null;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
}

export interface RoomLayout {
  width: number;
  height: number;
  unit: string;
  geometry: RoomLayoutGeometry;
  items: RoomLayoutItem[];
}
