<template>
  <div class="relative">
    <ApexCharts
      type="area" 
      height="350" 
      :options="chartOptions" 
      :series="series" 
    />
    <div v-if="!series[0].data.length" class="absolute inset-0 flex items-center justify-center bg-white bg-opacity-80">
      <span class="text-gray-500">{{ emptyMessage }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import ApexCharts from "vue3-apexcharts";
import type { SeriesData, ChartConfig } from '@/types/chart';

defineProps<{
  series: SeriesData[];
  chartOptions: ChartConfig;
  isLoading: boolean;
  emptyMessage?: string;
}>();
</script> 