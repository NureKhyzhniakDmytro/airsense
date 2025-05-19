import type { Pagination } from "@/types/api";
import { IntervalOption } from "@/types/date";

export interface Parameter {
  name: string;
  value: number | null;
  unit: string;
  min_value: number;
  max_value: number;
  critical_value: number;
}

export interface Sensor {
  id: number;
  type_name: string;
  serial_number: string;
  types: string[];
  parameters?: Parameter[];
}

export interface Device {
  id: number;
  serial_number: string;
  fan_speed: number;
  active_at: number;
}

export interface HistoryEntry {
  value: number;
  timestamp: number;
}

export interface HistoryParams {
  from: number;
  to: number;
  interval: string;
}

export interface Param {
  name: string;
  label: string;
  unit: string;
}

export interface ExtendedParam extends Param {
  min_value?: number;
  max_value?: number;
  critical_value?: number;
}

export interface DevicesResponse {
  data: Device[];
  pagination: Pagination;
}

export interface SensorsResponse {
  data: Sensor[];
  pagination: Pagination;
}

export const INTERVAL_OPTIONS = [
  { name: '1m', value: 'minute' },
  { name: '1h', value: 'hour' },
  { name: '1d', value: 'day' },
] as IntervalOption[];

export interface CurvePoint {
  value: number;
  fan_speed: number;
}

export interface CurveData {
  points: CurvePoint[];
  critical_value?: number;
}

export interface SeriesData {
  name: string;
  data: Array<{
    x: number;
    y: number;
  }>;
}

export interface ChartOptions {
  name: string;
  value: string;
}

export interface ChartLabel {
  name: string;
  serial_number: string;
}

export const PARAMETER_LABELS: Record<string, string> = {
  temperature: "Temperature",
  humidity: "Humidity",
  pressure: "Pressure",
  co2: "CO₂",
  device_speed: "Ventilation Speed",
};

export const PARAMETER_ICONS: Record<string, string> = {
  temperature: "thermometer",
  humidity: "water_drop",
  pressure: "compress",
  co2: "co2",
};

export const PARAMETER_STYLES: Record<string, string> = {
  temperature: 'bg-red-100 text-red-700',
  humidity: 'bg-blue-100 text-blue-700',
  pressure: 'bg-green-100 text-green-700',
  co2: 'bg-yellow-100 text-yellow-700',
  device_speed: 'bg-purple-100 text-purple-700',
}; 