export interface HistoryEntry {
  value: number;
  timestamp: number;
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

export const INTERVAL_OPTIONS: ChartOptions[] = [
  { name: "Minute", value: "minute" },
  { name: "Hour", value: "hour" },
  { name: "Day", value: "day" },
];

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