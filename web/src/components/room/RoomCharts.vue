<template>
  <div class="items-center flex-grow">
    <div class="flex flex-row gap-4 items-center justify-between">
      <Select
        v-model="selectedParam"
        :options="parametersOptions"
        optionLabel="label"
        class="w-full md:w-56"
      />

      <DateRangeSelector
        v-model:from="fromDate"
        v-model:to="toDate"
        v-model:interval="selectedInterval"
        :interval-options="INTERVAL_OPTIONS"
      />
    </div>

    <div class="flex flex-col gap-4 mt-8">
      <Skeleton v-if="isLoading" height="24rem" class="mb-2"/>

      <div 
        v-else-if="Object.keys(series).length === 0"
        class="bg-white shadow-md rounded-lg p-4 h-96 flex items-center justify-center"
      >
        <h3 class="text-lg font-semibold text-gray-800 mb-4">
          No data available for the chart
        </h3>
      </div>
      <div v-else
        v-for="(deviceSeries, sensorId) in series"
        :key="sensorId"
        class="bg-white shadow-md rounded-lg p-4"
      >
        <div class="flex flex-row items-center justify-between mb-2">
          <h3 class="text-lg font-semibold text-gray-800 mb-4">
            {{ sensorNames[sensorId] || sensorId }}
          </h3>
        </div>
        <ChartDisplay
          :series="deviceSeries"
          :chart-options="chartOptions"
          :is-loading="isChartLoading"
          empty-message="No data available for the chart"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { shallowRef, ref, onMounted, watch } from 'vue';
import { useRoute } from 'vue-router';
import Select from 'primevue/select';
import Skeleton from 'primevue/skeleton';
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
const sensorNames = shallowRef<Record<number, string>>({});
const isLoading = ref(false);
const isChartLoading = ref(false);

const selectedParam = ref<Param>(parametersOptions.value[0]);
const selectedInterval = ref(INTERVAL_OPTIONS[1]);
const fromDate = ref<Date>(
  new Date(new Date().setDate(new Date().getDate() - 1))
);
const toDate = ref<Date>(new Date());

const getLabel = (name: string) => PARAMETER_LABELS[name] || name;

async function loadChartData() {
  isLoading.value = true;
  isChartLoading.value = true;

  const newSeries: Record<number, SeriesData[]> = {};
  const newNames: Record<number, string> = {};

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

        const sensor = await sensorStore.fetchSensor(
          roomId,
          deviceData.id
        );
        newNames[deviceData.id] =
          sensor?.type_name || String(deviceData.id);
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
