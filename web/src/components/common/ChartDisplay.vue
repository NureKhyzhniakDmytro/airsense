<template>
  <div class="relative">
    <ClientOnly>
      <component
        :is="ApexCharts"
        type="area"
        height="350"
        :options="chartOptions"
        :series="series"
      />
      <template #fallback>
        <div class="h-[350px] flex items-center justify-center bg-white">
          <span class="text-gray-500">Loading chart...</span>
        </div>
      </template>
    </ClientOnly>
    <div v-if="!series[0].data.length" class="absolute inset-0 flex items-center justify-center bg-white bg-opacity-80">
      <span class="text-gray-500">{{ emptyMessage }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { defineAsyncComponent } from "vue";
import type { SeriesData, ChartConfig } from '@/types/chart';

const ApexCharts = defineAsyncComponent(() => import("vue3-apexcharts"));

defineProps<{
  series: SeriesData[];
  chartOptions: ChartConfig;
  isLoading: boolean;
  emptyMessage?: string;
}>();
</script>
