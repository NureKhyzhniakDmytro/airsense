import type { Device, Sensor } from "@/types/sensor";

export interface RoomLiveSensorEvent {
  type: "sensor";
  room_id: number;
  sensor_id?: number | null;
  sensor_serial_number?: string | null;
  parameter?: string | null;
  value?: number | null;
  sent_at?: number | null;
}

export interface RoomLiveDeviceEvent {
  type: "device";
  room_id: number;
  device_id?: number | null;
  device_serial_number?: string | null;
  fan_speed?: number | null;
  active_at?: number | null;
  source?: string | null;
}

export interface RoomLiveSnapshot {
  room_id: number;
  sensors: Sensor[];
  devices: Device[];
  generated_at: number;
}

export interface RoomLiveHandlers {
  snapshot?: (snapshot: RoomLiveSnapshot) => void;
  sensor?: (event: RoomLiveSensorEvent) => void;
  device?: (event: RoomLiveDeviceEvent) => void;
  open?: () => void;
  error?: (error: unknown) => void;
}
