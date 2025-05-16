export interface ChartPoint {
  x: number;
  y: number;
}

export interface ChartSeries {
  name: string;
  data: ChartPoint[];
}

export interface ChartEvent {
  clientX: number;
  clientY: number;
}

export interface ChartContext {
  w: {
    globals: {
      xAxisScale: {
        niceMin: number;
        niceMax: number;
      };
    };
  };
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

export interface ChartConfig {
  chart: {
    type: 'area' | 'line' | 'bar';
    toolbar: { show: boolean };
    zoom: { enabled: boolean };
    animations: {
      enabled: boolean;
      easing: string;
      speed: number;
    };
    fontFamily: string;
    events?: {
      dataPointSelection?: (event: ChartEvent, chartContext: ChartContext, config: { dataPointIndex: number }) => void;
      click?: (event: ChartEvent, chartContext: ChartContext, config: { dataPointIndex: number | undefined }) => void;
    };
  };
  dataLabels: {
    enabled: boolean;
  };
  stroke: {
    curve: string;
    width: number;
    colors: string[];
  };
  fill: {
    type: string;
    gradient: {
      shadeIntensity: number;
      opacityFrom: number;
      opacityTo: number;
      stops: number[];
      colorStops: Array<{
        offset: number;
        color: string;
        opacity: number;
      }>;
    };
  };
  xaxis: {
    type: string;
    min?: number;
    max?: number;
    categories?: string[];
    labels: {
      style: {
        fontSize: string;
        colors: string;
      };
      formatter?: (val: number) => string;
      datetimeFormatter?: {
        year: string;
        month: string;
        day: string;
        hour: string;
      };
    };
    tooltip: {
      enabled: boolean;
    };
    axisBorder: {
      show: boolean;
      color: string;
    };
    axisTicks: {
      show: boolean;
      color: string;
    };
  };
  yaxis: {
    min: number;
    max: number;
    tickAmount: number;
    labels: {
      style: {
        fontSize: string;
        colors: string;
      };
      formatter: (val: number) => string;
    };
    axisBorder: {
      show: boolean;
      color: string;
    };
  };
  markers: {
    size: number;
    colors: string[];
    strokeColors: string;
    strokeWidth: number;
    hover: { size: number };
  };
  grid: {
    borderColor: string;
    strokeDashArray: number;
    padding: {
      top: number;
      right: number;
      bottom: number;
      left: number;
    };
  };
  tooltip: {
    theme: string;
    x: {
      format: string;
    };
    y: {
      formatter: (val: number) => string;
    };
    marker: {
      show: boolean;
    };
  };
  legend: {
    show: boolean;
  };
} 