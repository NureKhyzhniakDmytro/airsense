import { ref } from 'vue';
import type { SeriesData } from '@/types/sensor';
import type { ChartConfig } from '@/types/chart';

export const chartPalette = {
  primary: '#0F766E',
  surface: '#FBFCFD',
  muted: '#68737E',
  border: '#D0D7DE',
  danger: '#C24135',
  onDanger: '#FFFFFF',
} as const;

export function useChartConfig() {
  const series = ref<SeriesData[]>([
    {
      name: "",
      data: [],
    },
  ]);

  const chartOptions = ref<ChartConfig>({
    chart: {
      type: 'area' as const,
      toolbar: { show: false },
      zoom: { enabled: false },
      animations: {
        enabled: true,
        easing: 'easeinout',
        speed: 220,
      },
      fontFamily: 'Work Sans, Inter, sans-serif',
    },
    dataLabels: {
      enabled: false
    },
    stroke: {
      curve: 'smooth',
      width: 2,
      colors: [chartPalette.primary]
    },
    fill: {
      type: 'gradient',
      gradient: {
        shadeIntensity: 1,
        opacityFrom: 0.7,
        opacityTo: 0.2,
        stops: [0, 90, 100],
        colorStops: [
          { offset: 0, color: chartPalette.primary, opacity: 0.24 },
          { offset: 100, color: chartPalette.surface, opacity: 0 }
        ]
      }
    },
    xaxis: {
      type: 'datetime',
      labels: {
        style: { 
          fontSize: '12px',
          colors: chartPalette.muted
        },
        datetimeFormatter: {
          year: 'yyyy',
          month: "MMM 'yy",
          day: 'dd MMM',
          hour: 'HH:mm'
        }
      },
      tooltip: {
        enabled: false
      },
      axisBorder: {
        show: true,
        color: chartPalette.border
      },
      axisTicks: {
        show: true,
        color: chartPalette.border
      }
    },
    yaxis: {
      min: 0,
      max: 100,
      tickAmount: 5,
      labels: {
        style: {
          fontSize: '12px',
          colors: chartPalette.muted
        },
        formatter: (val: number) => val.toFixed(1)
      },
      axisBorder: {
        show: true,
        color: chartPalette.border
      }
    },
    markers: {
      size: 4,
      colors: [chartPalette.surface],
      strokeColors: chartPalette.primary,
      strokeWidth: 2,
      hover: { size: 6 },
    },
    grid: {
      borderColor: chartPalette.border,
      strokeDashArray: 2,
      padding: {
        top: 0,
        right: 0,
        bottom: 0,
        left: 0
      }
    },
    tooltip: {
      theme: 'light',
      x: {
        format: 'dd MMM HH:mm'
      },
      y: {
        formatter: (val: number) => val.toFixed(1)
      },
      marker: {
        show: false
      }
    },
    legend: {
      show: false
    }
  });

  return {
    series,
    chartOptions
  };
} 
