<template>
  <div class="items-center flex-grow" :class="{ 'place-content-center': !sensor }">
    <div v-if="!sensor" class="flex flex-col items-center justify-center">
      <i class="pi pi-exclamation-circle text-6xl text-gray-400 mb-4"></i>
      <h2 class="text-2xl font-semibold text-gray-700 mb-2">Sensor Not Found</h2>
      <p class="text-gray-500">The requested sensor does not exist or has been removed</p>
    </div>

    <div v-else>
      <SensorHeader 
        :sensor="sensor" 
        :parameters="sensor.parameters" 
      />

      <div class="bg-white shadow-md rounded-lg p-4 mt-8">
        <div class="flex flex-row items-center justify-between mb-2">
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
        <ChartDisplay
          :series="series"
          :chart-options="chartOptions"
          :is-loading="isChartLoading"
        />
      </div>
    </div>  
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import { getAvailableParameters } from "@/services/apiService";
import api from "@/api";
import { useSensorStore } from "@/store/sensorStore";
import { useChartConfig } from "@/config/chartConfig";
import { 
  INTERVAL_OPTIONS, 
  PARAMETER_LABELS, 
  type HistoryEntry, 
  Sensor, 
  Parameter
} from "@/types/sensor";
import SensorHeader from "@/components/sensor/SensorHeader.vue";
import ParameterSelector from "@/components/common/ParameterSelector.vue";
import DateRangeSelector from "@/components/common/DateRangeSelector.vue";
import ChartDisplay from "@/components/common/ChartDisplay.vue";

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