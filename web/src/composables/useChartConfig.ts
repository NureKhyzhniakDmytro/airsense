import { ref } from 'vue';
import type { SeriesData } from '@/types/sensor';

export function useChartConfig() {
  const series = ref<SeriesData[]>([
    {
      name: "",
      data: [],
    },
  ]);

  const chartOptions = ref({
    chart: {
      type: 'area' as const,
      toolbar: { show: false },
      zoom: { enabled: false },
      animations: {
        enabled: true,
        easing: 'easeinout',
        speed: 800,
      },
      fontFamily: 'Inter, sans-serif',
    },
    dataLabels: {
      enabled: false
    },
    stroke: {
      curve: 'smooth',
      width: 2,
      colors: ['#7C3AED']
    },
    fill: {
      type: 'gradient',
      gradient: {
        shadeIntensity: 1,
        opacityFrom: 0.7,
        opacityTo: 0.2,
        stops: [0, 90, 100],
        colorStops: [
          { offset: 0, color: "#7C3AED", opacity: 0.4 },
          { offset: 100, color: "#ffffff", opacity: 0 }
        ]
      }
    },
    xaxis: {
      type: 'datetime',
      labels: {
        style: { 
          fontSize: '12px',
          colors: '#6B7280'
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
        color: '#E5E7EB'
      },
      axisTicks: {
        show: true,
        color: '#E5E7EB'
      }
    },
    yaxis: {
      min: 0,
      max: 100,
      tickAmount: 5,
      labels: {
        style: {
          fontSize: '12px',
          colors: '#6B7280'
        },
        formatter: (val: number) => val.toFixed(1)
      },
      axisBorder: {
        show: true,
        color: '#E5E7EB'
      }
    },
    markers: {
      size: 4,
      colors: ['#fff'],
      strokeColors: '#7C3AED',
      strokeWidth: 2,
      hover: { size: 6 },
    },
    grid: {
      borderColor: '#E5E7EB',
      strokeDashArray: 4,
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