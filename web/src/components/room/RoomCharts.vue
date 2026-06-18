<template>
  <div class="section-page">
    <h2 class="visually-hidden">Telemetry</h2>

    <section class="telemetry-controls" aria-label="Telemetry filters">
      <div class="telemetry-controls__parameter">
        <label for="telemetry-parameter">Signal</label>
        <Select
          v-model="selectedParam"
          inputId="telemetry-parameter"
          :options="parametersOptions"
          optionLabel="label"
          class="telemetry-controls__select"
        />
      </div>

      <DateRangeSelector
        v-model:from="fromDate"
        v-model:to="toDate"
        v-model:interval="selectedInterval"
        :interval-options="INTERVAL_OPTIONS"
        class="telemetry-controls__range"
      />

      <div class="telemetry-controls__summary" aria-label="Telemetry summary">
        <span class="telemetry-controls__metric">
          <strong>{{ seriesCount }}</strong>
          <span>series</span>
        </span>
        <span class="telemetry-controls__metric">
          <strong>{{ pointCount }}</strong>
          <span>{{ pointCount === 1 ? 'point' : 'points' }}</span>
        </span>
      </div>
    </section>

    <div
      class="telemetry-charts"
      :class="{ 'telemetry-charts--single': seriesCount === 1 }"
    >
      <Skeleton v-if="isLoading" height="100%" class="telemetry-charts__skeleton"/>

      <EmptyState
        v-else-if="Object.keys(series).length === 0"
        class="telemetry-empty empty-state--fill empty-state--centered"
        title="No chart data"
        description="There is no telemetry for the selected parameter and period yet."
        icon="pi pi-chart-line"
      />
      <template v-else>
        <section
          v-for="(deviceSeries, sensorId) in series"
          :key="sensorId"
          class="chart-panel"
        >
          <header class="chart-panel__header">
            <div>
              <span class="chart-panel__eyebrow">Source {{ sensorId }}</span>
              <h3>{{ sensorNames[sensorId]?.name }}</h3>
            </div>
            <span class="chart-panel__serial">{{ sensorNames[sensorId]?.serial_number }}</span>
          </header>
          <div class="chart-panel__body">
            <ChartDisplay
              :series="deviceSeries"
              :chart-options="chartOptions"
              :is-loading="isChartLoading"
              height="100%"
              empty-message="No data available for the chart"
            />
          </div>
        </section>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, shallowRef, ref, onMounted, watch } from 'vue';
import { useRoute } from 'vue-router';
import Select from 'primevue/select';
import Skeleton from 'primevue/skeleton';
import EmptyState from '@/components/common/EmptyState.vue';
import DateRangeSelector from '@/components/common/DateRangeSelector.vue';
import ChartDisplay from '@/components/common/ChartDisplay.vue';
import { useSensorStore } from '@/store/sensorStore';
import {
  getRoomHistory,
  getParameterHistory,
  getAvailableParameters,
} from '@/services/apiService';
import {
  type Param,
  type HistoryParams,
  type SeriesData,
  type Parameter,
  type ChartLabel,
  INTERVAL_OPTIONS,
  PARAMETER_LABELS,
} from '@/types/sensor';
import { useChartConfig } from '@/config/chartConfig';

function debounce<F extends (...args: any[]) => void>(func: F, wait = 300) {
  let timeout: ReturnType<typeof setTimeout>;
  return (...args: Parameters<F>) => {
    clearTimeout(timeout);
    timeout = setTimeout(() => func(...args), wait);
  };
}

const route = useRoute();
const roomId = Number(route.params.roomId);
const sensorStore = useSensorStore();

const parametersOptions = ref<Param[]>([
  { label: 'Device Speed', name: 'device_speed', unit: '%' },
]);

const { chartOptions } = useChartConfig();
const series = shallowRef<Record<number, SeriesData[]>>({});
const sensorNames = shallowRef<Record<number, ChartLabel>>({});
const isLoading = ref(false);
const isChartLoading = ref(false);

const selectedParam = ref<Param>(parametersOptions.value[0]);
const selectedInterval = ref(INTERVAL_OPTIONS[1]);
const fromDate = ref<Date>(
  new Date(new Date().setDate(new Date().getDate() - 1))
);
const toDate = ref<Date>(new Date());
const seriesCount = computed(() => Object.keys(series.value).length);
const pointCount = computed(() => Object.values(series.value).reduce(
  (sum, deviceSeries) => sum + deviceSeries.reduce((innerSum, item) => innerSum + item.data.length, 0),
  0
));

const getLabel = (name: string) => PARAMETER_LABELS[name] || name;

async function loadChartData() {
  isLoading.value = true;
  isChartLoading.value = true;

  const newSeries: Record<number, SeriesData[]> = {};
  const newNames: Record<number, ChartLabel> = {};

  try {
    const params: HistoryParams = {
      from: fromDate.value.getTime(),
      to: toDate.value.getTime(),
      interval: selectedInterval.value.value,
    };

    const res =
      selectedParam.value.name === 'device_speed'
        ? await getRoomHistory(roomId, params)
        : await getParameterHistory(
            roomId,
            selectedParam.value.name,
            params
          );

    await Promise.all(
      res.data.map(async (deviceData) => {
        const history = deviceData.history || [];
        if (!history.length) return;

        const unit =
          parametersOptions.value.find(
            (p) => p.name === selectedParam.value.name
          )?.unit || '';
        const label = `${getLabel(selectedParam.value.name)} (${unit})`;

        newSeries[deviceData.id] = [
          {
            name: label,
            data: history.map((h) => ({
              x: h.timestamp * 1000,
              y: h.value,
            })),
          },
        ];
        if (selectedParam.value.name === 'device_speed') {
          newNames[deviceData.id] = {
            name: `Device #${deviceData.id}`,
            serial_number: deviceData.serial_number,
          }
        } else {
          const sensor = await sensorStore.fetchSensor(
            roomId,
            deviceData.id
          );
          newNames[deviceData.id] = {
            name: sensor?.type_name || String(deviceData.id),
            serial_number: deviceData.serial_number,
          }
        }
      })
    );

    series.value = newSeries;
    sensorNames.value = newNames;
  } catch (err) {
    console.error('Error loading chart data:', err);
  } finally {
    isChartLoading.value = false;
    isLoading.value = false;
  }
}

const loadChartDataDebounced = debounce(loadChartData, 300);

async function loadParams() {
  try {
    const fetched: Parameter[] = await getAvailableParameters(roomId);
    fetched.forEach((p) => {
      if (!parametersOptions.value.find((x) => x.name === p.name)) {
        parametersOptions.value.push({
          name: p.name,
          label: getLabel(p.name),
          unit: p.unit,
        });
      }
    });
  } catch (err) {
    console.error('Error loading parameters:', err);
  }
}

onMounted(async () => {
  await loadParams();
  await loadChartData();
});

watch(
  [selectedParam, selectedInterval, fromDate, toDate],
  loadChartDataDebounced
);
</script>

<style scoped>
.section-page {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: var(--app-gap-sm);
  height: 100%;
  min-height: 0;
  min-width: 0;
  overflow: hidden;
  width: 100%;
}

.visually-hidden {
  border: 0;
  clip: rect(0 0 0 0);
  height: 1px;
  margin: -1px;
  overflow: hidden;
  padding: 0;
  position: absolute;
  white-space: nowrap;
  width: 1px;
}

.telemetry-controls {
  align-items: end;
  background: color-mix(in srgb, var(--app-surface) 88%, var(--app-surface-soft));
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: grid;
  flex: 0 0 auto;
  gap: var(--app-gap-sm) 10px;
  grid-template-areas: "parameter range summary";
  grid-template-columns: minmax(180px, 230px) minmax(0, 1fr) auto;
  padding: 10px var(--app-panel-padding);
}

.telemetry-controls__parameter {
  display: flex;
  flex-direction: column;
  gap: 6px;
  grid-area: parameter;
  min-width: 0;
}

.telemetry-controls__parameter label {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.66rem;
  font-weight: 650;
  line-height: 1rem;
  text-transform: uppercase;
}

.telemetry-controls__select {
  min-height: var(--app-compact-control-height);
  min-width: 0;
  width: 100%;
}

.telemetry-controls__select :deep(.p-select-label) {
  min-height: calc(var(--app-compact-control-height) - 2px);
}

.telemetry-controls__range {
  grid-area: range;
  min-width: 0;
}

.telemetry-controls__summary {
  align-items: center;
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: grid;
  gap: 1px;
  grid-area: summary;
  grid-template-columns: repeat(2, minmax(72px, 1fr));
  justify-content: stretch;
  height: var(--app-control-height);
  min-width: 154px;
  overflow: hidden;
}

.telemetry-controls__metric {
  align-items: center;
  background: var(--app-surface-soft);
  color: var(--app-muted);
  display: inline-flex;
  gap: 5px;
  justify-content: center;
  height: 100%;
  min-width: 0;
  padding: 0 0.5rem;
}

.telemetry-controls__metric strong {
  color: var(--app-text-strong);
  font-family: var(--app-mono);
  font-size: 0.82rem;
  font-weight: 760;
  line-height: 1rem;
}

.telemetry-controls__metric span {
  font-family: var(--app-mono);
  font-size: 0.66rem;
  font-weight: 650;
  line-height: 1rem;
  text-transform: uppercase;
}

.telemetry-charts {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: var(--app-gap-sm);
  min-height: 0;
  min-width: 0;
  overflow: auto;
  scrollbar-gutter: stable;
}

.telemetry-charts--single {
  overflow: hidden;
}

.telemetry-charts__skeleton {
  flex: 1;
  min-height: 25rem;
}

.chart-panel {
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex: 1 0 auto;
  flex-direction: column;
  min-height: 25rem;
  overflow: hidden;
}

.chart-panel__header {
  align-items: center;
  background: var(--app-surface-soft);
  border-bottom: 1px solid var(--app-border);
  display: flex;
  gap: var(--app-list-gap);
  justify-content: space-between;
  min-height: 48px;
  padding: 9px var(--app-panel-padding);
}

.chart-panel__eyebrow,
.chart-panel__serial {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 600;
  line-height: 1rem;
  text-transform: uppercase;
}

.chart-panel h3 {
  color: var(--app-text-strong);
  font-size: 0.95rem;
  font-weight: 760;
  line-height: 1.3rem;
  margin: 0;
}

.chart-panel__body {
  display: flex;
  flex: 1;
  min-height: 0;
  padding: 6px var(--app-panel-padding) 8px;
}

@media (min-width: 901px) {
  .telemetry-charts--single .chart-panel {
    height: 100%;
    min-height: 0;
  }
}

@media (max-width: 1180px) {
  .telemetry-controls {
    grid-template-areas:
      "parameter summary"
      "range range";
    grid-template-columns: minmax(180px, 1fr) auto;
  }
}

@media (min-width: 641px) {
  .telemetry-controls__range :deep(.date-range-selector),
  .telemetry-controls__range :deep(.date-range-selector__dates) {
    align-items: center;
    flex-wrap: nowrap;
    justify-content: flex-start;
    width: 100%;
  }

  .telemetry-controls__range :deep(.date-range-selector__dates) {
    flex: 0 1 auto;
    min-width: 0;
    width: auto;
  }

  .telemetry-controls__range :deep(.date-range-selector__separator) {
    display: inline-flex;
    flex: 0 0 auto;
  }

  .telemetry-controls__range :deep(.p-datepicker),
  .telemetry-controls__range :deep(.p-inputwrapper),
  .telemetry-controls__range :deep(.p-selectbutton) {
    min-width: 0;
    width: auto;
  }

  .telemetry-controls__range :deep(.date-range-selector__interval) {
    flex: 0 0 auto;
  }

  .telemetry-controls__range :deep(.p-datepicker-input) {
    width: 9.75rem;
  }
}

@media (max-width: 900px) {
  .section-page {
    height: auto;
    overflow: visible;
  }

  .telemetry-controls__summary {
    align-self: center;
  }

  .telemetry-charts,
  .telemetry-charts--single {
    overflow: visible;
  }
}

@media (max-width: 640px) {
  .telemetry-controls {
    align-items: stretch;
    grid-template-areas:
      "parameter"
      "range"
      "summary";
    grid-template-columns: 1fr;
  }

  .telemetry-controls__summary {
    justify-content: flex-start;
  }
}
</style>
