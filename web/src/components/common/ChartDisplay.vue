<template>
  <div class="chart-display">
    <ClientOnly>
      <component
        :is="ApexCharts"
        type="area"
        :height="props.height"
        :options="props.chartOptions"
        :series="props.series"
      />
      <template #fallback>
        <div class="chart-display__fallback" :style="fallbackStyle">
          <span>Loading chart...</span>
        </div>
      </template>
    </ClientOnly>
    <div v-if="!props.isLoading && !hasPoints" class="chart-display__empty">
      <span>{{ props.emptyMessage }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, defineAsyncComponent } from "vue";
import type { SeriesData, ChartConfig } from '@/types/chart';

const ApexCharts = defineAsyncComponent(() => import("vue3-apexcharts"));

const props = withDefaults(defineProps<{
  series: SeriesData[];
  chartOptions: ChartConfig;
  isLoading: boolean;
  emptyMessage?: string;
  height?: number | string;
}>(), {
  emptyMessage: "No data available for the chart",
  height: 350,
});

const hasPoints = computed(() => props.series.some((item) => item.data?.length));
const fallbackStyle = computed(() => ({
  minHeight: typeof props.height === "number"
    ? `${props.height}px`
    : props.height === "100%"
      ? "24rem"
      : props.height,
}));
</script>

<style scoped>
.chart-display {
  flex: 1;
  min-height: 0;
  position: relative;
  width: 100%;
}

.chart-display :deep(.vue-apexcharts),
.chart-display :deep(.apexcharts-canvas) {
  min-height: 100%;
  width: 100% !important;
}

.chart-display__fallback,
.chart-display__empty {
  align-items: center;
  display: flex;
  justify-content: center;
}

.chart-display__fallback {
  background: var(--app-surface);
  color: var(--app-muted);
  height: 100%;
  width: 100%;
}

.chart-display__empty {
  background: color-mix(in srgb, var(--app-surface) 84%, transparent);
  color: var(--app-muted);
  inset: 0;
  position: absolute;
}
</style>
