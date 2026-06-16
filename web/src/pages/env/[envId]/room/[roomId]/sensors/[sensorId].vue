<template>
  <div class="detail-page">
    <Skeleton v-if="isLoading" height="18rem" />

    <EmptyState
      v-else-if="!sensor"
      title="Sensor not found"
      description="The requested sensor does not exist or has been removed."
      icon="pi pi-exclamation-circle"
    />

    <div v-else>
      <SensorHeader 
        :sensor="sensor" 
        :parameters="sensor.parameters" 
      />

      <section class="detail-panel">
        <header class="detail-panel__header">
          <div>
            <span>History</span>
            <h2>Parameter timeline</h2>
          </div>
        </header>
        <div class="detail-toolbar">
          <ParameterSelector
            :types="sensor.types"
            :selected-param="selectedParam"
            @select="selectType"
          />
          <DateRangeSelector
            v-model:from="fromDate"
            v-model:to="toDate"
            v-model:interval="selectedInterval"
            :interval-options="INTERVAL_OPTIONS"
          />
        </div>
        <div class="detail-chart">
          <ChartDisplay
            :series="series"
            :chart-options="chartOptions"
            :is-loading="isChartLoading"
            height="100%"
          />
        </div>
      </section>
    </div>  
  </div>
</template>

<script setup lang="ts">
definePageMeta({ name: 'sensor', requiresAuth: true })

import { ref, onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import { getAvailableParameters } from "@/services/apiService";
import api from "@/api";
import { useSensorStore } from "@/store/sensorStore";
import { useChartConfig } from "@/config/chartConfig";
import { INTERVAL_OPTIONS, PARAMETER_LABELS, type HistoryEntry } from "@/types/sensor";
import type { Sensor, Parameter } from "@/types/sensor";
import SensorHeader from "@/components/sensor/SensorHeader.vue";
import ParameterSelector from "@/components/common/ParameterSelector.vue";
import DateRangeSelector from "@/components/common/DateRangeSelector.vue";
import ChartDisplay from "@/components/common/ChartDisplay.vue";
import EmptyState from "@/components/common/EmptyState.vue";
import Skeleton from 'primevue/skeleton';

const route = useRoute();
const sensorStore = useSensorStore();
const { series, chartOptions } = useChartConfig();

const sensorId = Number(route.params.sensorId);
const roomId = Number(route.params.roomId);
const isLoading = ref(true);
const isChartLoading = ref(false);
const sensor = ref<Sensor | null>(null);
const params = ref<Parameter[]>([]);
const selectedParam = ref<string>("");
const selectedInterval = ref(INTERVAL_OPTIONS[1]);
const fromDate = ref<Date>(new Date(    
  new Date().getFullYear(),
  new Date().getMonth(), 
  new Date().getDate() - 1,
  new Date().getHours(),
  new Date().getMinutes()
));
const toDate = ref<Date>(new Date());

const loadSensor = async () => {
  isLoading.value = true;
  try {
    sensor.value = await sensorStore.fetchSensor(roomId, sensorId);
    const parameters = await getAvailableParameters(roomId);
    params.value = parameters.filter(parameter => 
      sensor.value?.types.some(type => type === parameter.name)
    );
    selectedParam.value = sensor.value?.types[0] || "";
  } catch (error) {
    console.error("Error loading sensor:", error);
  } finally {
    isLoading.value = false;
  }
};

const selectType = (type: string) => {
  selectedParam.value = type;
};

const loadChartData = async () => {
  if (!sensor.value || !selectedParam.value) return;

  isChartLoading.value = true;
  series.value[0].data = [];
  chartOptions.value.xaxis.categories = [];

  try {
    const from = new Date(fromDate.value).getTime();
    const to = new Date(toDate.value).getTime();

    const res = await api.get(
      `/room/${roomId}/${selectedParam.value}/history/${sensorId}`,
      { params: { from, to, interval: selectedInterval.value.value } }
    );

    const history: HistoryEntry[] = res.data?.data?.history || [];
    if (history.length > 0) {
      const unit = params.value.find(p => p.name === selectedParam.value)?.unit || "";
      const label = `${PARAMETER_LABELS[selectedParam.value]} (${unit})`;
      const parameter = params.value.find(p => p.name === selectedParam.value);

      series.value[0].name = label;
      series.value[0].data = history.map(h => ({
        x: h.timestamp * 1000,
        y: h.value
      }));

      if (parameter) {
        chartOptions.value = {
          ...chartOptions.value,
          yaxis: {
            ...chartOptions.value.yaxis,
            min: parameter.min_value,
            max: parameter.max_value,
            tickAmount: 5,
            labels: {
              formatter: (val: number) => val.toFixed(1)
            }
          }
        };
      }
    }
  } catch (err) {
    console.error("Error loading chart data:", err);
  } finally {
    isChartLoading.value = false;
  }
};

onMounted(async () => {
  await loadSensor();
  await loadChartData();
});

watch(
  [selectedParam, selectedInterval, fromDate, toDate],
  loadChartData
);
</script>

<style scoped>
.detail-page {
  display: flex;
  flex: 1;
  flex-direction: column;
  width: 100%;
}

.detail-page > div {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 14px;
  min-height: 0;
}

.detail-panel {
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
}

.detail-panel__header {
  background: var(--app-surface-soft);
  border-bottom: 1px solid var(--app-border);
  padding: 10px 12px;
}

.detail-panel__header span {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 600;
  text-transform: uppercase;
}

.detail-panel__header h2 {
  color: var(--app-text-strong);
  font-size: 0.95rem;
  font-weight: 760;
  line-height: 1.3rem;
  margin: 2px 0 0;
}

.detail-toolbar {
  align-items: center;
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  justify-content: space-between;
  padding: 12px;
}

.detail-chart {
  display: flex;
  flex: 1;
  min-height: 24rem;
  padding: 0 12px 10px;
}
</style>
