import { ref } from 'vue';
import type { SeriesData } from '@/types/sensor';
import type { ChartConfig } from '@/types/chart';

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
      colors: ['#0F766E']
    },
    fill: {
      type: 'gradient',
      gradient: {
        shadeIntensity: 1,
        opacityFrom: 0.7,
        opacityTo: 0.2,
        stops: [0, 90, 100],
        colorStops: [
          { offset: 0, color: "#0F766E", opacity: 0.24 },
          { offset: 100, color: "#FBFCFD", opacity: 0 }
        ]
      }
    },
    xaxis: {
      type: 'datetime',
      labels: {
        style: { 
          fontSize: '12px',
          colors: '#68737E'
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
        color: '#D0D7DE'
      },
      axisTicks: {
        show: true,
        color: '#D0D7DE'
      }
    },
    yaxis: {
      min: 0,
      max: 100,
      tickAmount: 5,
      labels: {
        style: {
          fontSize: '12px',
          colors: '#68737E'
        },
        formatter: (val: number) => val.toFixed(1)
      },
      axisBorder: {
        show: true,
        color: '#D0D7DE'
      }
    },
    markers: {
      size: 4,
      colors: ['#FBFCFD'],
      strokeColors: '#0F766E',
      strokeWidth: 2,
      hover: { size: 6 },
    },
    grid: {
      borderColor: '#D0D7DE',
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
