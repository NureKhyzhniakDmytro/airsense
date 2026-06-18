<template>
  <div class="detail-page">
    <Skeleton v-if="isLoading" height="18rem" />

    <EmptyState
      v-else-if="!device"
      class="detail-empty empty-state--fill empty-state--centered"
      title="Device not found"
      description="The requested device does not exist or has been removed."
      icon="pi pi-exclamation-circle"
    />

    <div v-else>
      <DeviceHeader :device="device" /> 

      <section class="detail-panel">
        <header class="detail-panel__header">
          <div>
            <span>History</span>
            <h2>Speed timeline</h2>
          </div>
        </header>
        <div class="detail-toolbar">
          <DateRangeSelector
            v-model:from="fromDate"
            v-model:to="toDate"
            v-model:interval="selectedInterval" 
            :interval-options="INTERVAL_OPTIONS"
            from-label="Start"
            to-label="End"
          />
        </div>
        <div class="detail-chart">
          <ChartDisplay
            :series="series"
            :chart-options="chartOptions"
            :is-loading="isChartLoading"
            empty-message="No speed data available for the selected period"
            height="100%"
          />
        </div>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ name: 'device', requiresAuth: true })

import { ref, onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import type { Device } from "@/types/sensor";
import api from "@/api";
import { useDeviceStore } from "@/store/deviceStore";
import DateRangeSelector from "@/components/common/DateRangeSelector.vue";
import ChartDisplay from "@/components/common/ChartDisplay.vue";
import DeviceHeader from "@/components/device/DeviceHeader.vue";
import EmptyState from "@/components/common/EmptyState.vue";
import { INTERVAL_OPTIONS, type HistoryEntry } from "@/types/sensor";
import { useChartConfig } from "@/config/chartConfig";
import Skeleton from 'primevue/skeleton';

const route = useRoute();
const deviceStore = useDeviceStore();
const deviceId = Number(route.params.deviceId);
const roomId = Number(route.params.roomId);

const isLoading = ref(true);
const isChartLoading = ref(false);
const device = ref<Device | null>(null);
const { series, chartOptions } = useChartConfig();

const selectedInterval = ref(INTERVAL_OPTIONS[1]);
const fromDate = ref<Date>(new Date(new Date().setDate(new Date().getDate() - 1)));
const toDate = ref<Date>(new Date());

const loadDevice = async () => {
  isLoading.value = true;
  try {
    device.value = await deviceStore.fetchDevice(roomId, deviceId);
  } catch (error) {
    console.error("Failed to load device:", error);
  } finally {
    isLoading.value = false;
  }
};

const loadChartData = async () => {
  if (!device.value) return;

  isChartLoading.value = true;
  series.value[0].data = [];

  try {
    const from = fromDate.value.getTime();
    const to = toDate.value.getTime();

    const res = await api.get(
      `/room/${roomId}/history/${deviceId}`,
      { params: { from, to, interval: selectedInterval.value.value } }
    );

    const history: HistoryEntry[] = res.data?.data?.history || [];
    if (history.length > 0) {
      series.value[0].name = 'Speed';
      series.value[0].data = history.map(h => ({
        x: h.timestamp * 1000,
        y: h.value
      }));
    }
  } catch (err) {
    console.error("Failed to load chart data:", err);
  } finally {
    isChartLoading.value = false;
  }
};

onMounted(async () => {
  await loadDevice();
  await loadChartData();
});

watch([selectedInterval, fromDate, toDate], loadChartData);
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
  display: flex;
  justify-content: flex-end;
  padding: 12px;
}

.detail-chart {
  display: flex;
  flex: 1;
  min-height: 24rem;
  padding: 0 12px 10px;
}
</style>
