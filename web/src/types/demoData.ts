export interface DemoEnvironment {
  id: number;
  name: string;
  icon: string;
}

export interface DemoMetrics {
  room_count: number;
  sensor_count: number;
  device_count: number;
  sensor_data_rows: number;
  device_data_rows: number;
  latest_telemetry_at?: string | null;
  latest_device_at?: string | null;
}

export interface DemoRoomStatus {
  room_id: number;
  room_name: string;
  room_icon: string;
  sensor_count: number;
  sensor_serial_number?: string | null;
  sensor_serial_numbers?: string | null;
  device_count: number;
  device_serial_number?: string | null;
  device_serial_numbers?: string | null;
  scenario: string;
  ventilation_power_override?: number | null;
  occupancy_override?: number | null;
  co2?: number | null;
  temperature?: number | null;
  humidity?: number | null;
  occupancy?: number | null;
  ventilation_power?: number | null;
  last_activity_at?: string | null;
}

export interface DemoDataStatus {
  environment?: DemoEnvironment | null;
  metrics: DemoMetrics;
  rooms: DemoRoomStatus[];
  scenarios: string[];
}

export interface DemoBootstrapPayload {
  room_count: number;
}

export interface DemoBackfillPayload {
  hours: number;
  interval_minutes: number;
  scenario?: string | null;
}

export interface DemoRoomReadingsPayload {
  co2?: number | null;
  temperature?: number | null;
  humidity?: number | null;
  occupancy?: number | null;
  ventilation_power?: number | null;
}

export interface DemoRoomCreatePayload {
  name?: string | null;
  icon?: string | null;
  sensor_count?: number | null;
  device_count?: number | null;
  readings?: DemoRoomReadingsPayload | null;
}

export interface DemoRoomUpdatePayload {
  name: string;
  icon?: string | null;
}

export interface DemoRoomAssetsPayload {
  sensor_count?: number | null;
  device_count?: number | null;
}

export interface DemoRoomProfilePayload {
  scenario: string;
  ventilation_power_override?: number | null;
  occupancy_override?: number | null;
}

export interface DemoBackfillResult {
  inserted_sensor_rows: number;
  inserted_device_rows: number;
  from: string;
  to: string;
}

export interface DemoResetResult {
  deleted_sensor_rows: number;
  deleted_device_rows: number;
}
