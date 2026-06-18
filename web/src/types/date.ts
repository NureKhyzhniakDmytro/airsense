export interface DateRange {
  from: Date;
  to: Date;
}

export interface IntervalOption {
  name: string;
  value: string;
  label?: string;
}

export interface DateRangePresetOption {
  label: string;
  value: string;
  amount: number;
  unit: 'minute' | 'hour' | 'day';
}

export interface DateRangeProps {
  from: Date;
  to: Date;
  interval?: IntervalOption;
  intervalOptions?: IntervalOption[];
  presetOptions?: DateRangePresetOption[];
  fromLabel?: string;
  toLabel?: string;
}

export interface DateRangeEmits {
  (e: 'update:from', value: Date): void;
  (e: 'update:to', value: Date): void;
  (e: 'update:interval', value: IntervalOption): void;
} 
