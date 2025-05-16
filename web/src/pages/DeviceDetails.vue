<template>
  <div class="items-center flex-grow" :class="{ 'place-content-center': !device }">
    <div v-if="!device" class="flex flex-col items-center justify-center">
      <i class="pi pi-exclamation-circle text-6xl text-gray-400 mb-4"></i>
      <h2 class="text-2xl font-semibold text-gray-700 mb-2">Device Not Found</h2>
      <p class="text-gray-500">The requested device does not exist or has been removed</p>
    </div>

    <div v-else>
      <DeviceHeader :device="device" /> 

      <div class="bg-white shadow-md rounded-lg p-4 mt-8">
        <div class="flex flex-row items-center justify-between mb-2">
          <h3 class="text-lg font-semibold text-gray-800 mb-4">Speed</h3>
          <DateRangeSelector
            v-model:from="fromDate"
            v-model:to="toDate"
            v-model:interval="selectedInterval" 
            :interval-options="INTERVAL_OPTIONS"
            from-label="Start"
            to-label="End"
          />
        </div>
        <ChartDisplay
          :series="series"
          :chart-options="chartOptions"
          :is-loading="isChartLoading"
          empty-message="No speed data available for the selected period"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import { Device } from "@/types/sensor";
import api from "@/api";
import { useDeviceStore } from "@/store/deviceStore";
import DateRangeSelector from "@/components/common/DateRangeSelector.vue";
import ChartDisplay from "@/components/common/ChartDisplay.vue";
import DeviceHeader from "@/components/device/DeviceHeader.vue";
import { INTERVAL_OPTIONS, type HistoryEntry } from "@/types/sensor";
import { useChartConfig } from "@/config/chartConfig";

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
      { params: { from, to, interval: selectedInterval.value } }
    );

    const history: HistoryEntry[] = res.data?.data?.history || [];
    if (history.length > 0) {
      series.value[0].name = 'Speed';
      series.value[0].data = history.map(h => ({
        x: h.timestamp * 1000,
        y: h.value
      }));
    }
    console.log(series.value);
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