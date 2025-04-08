<template>
  <div class="container max-w-7xl mx-auto py-6 space-y-6">
    <Dropdown
        v-model="selectedParam"
        :options="availableOptions"
        buttonClass="bg-white text-gray-900 border border-gray-300 py-2 px-4 rounded-md text-left hover:border-gray-400"
        listClass="bg-white w-full border border-gray-300 rounded-md shadow-lg text-gray-900"
        itemClass="px-4 py-2 hover:bg-gray-100 cursor-pointer"
        activeItemClass="bg-blue-100 text-blue-800 font-semibold"
        wrapperClass="w-64"
    />

    <DateTimePicker
        v-model="singleDateTime"
        :showDate="true"
        :showTime="true"
        :range="false"
        buttonClass="bg-white text-black border border-gray-300 px-4 py-2 rounded-md"
        listClass="bg-white border border-gray-300"
        inputClass="border border-gray-300 px-2 py-1 rounded-md"
        confirmButtonClass="bg-blue-600 text-white py-1 rounded-md hover:bg-blue-700"
        cancelButtonClass="bg-gray-200 text-black py-1 rounded-md hover:bg-gray-300"
    />

    <DateTimePicker
        v-model="rangeDateTime"
        :showDate="true"
        :showTime="true"
        :range="true"
        buttonClass="bg-white text-black border border-gray-300 px-4 py-2 rounded-md"
        listClass="bg-white border border-gray-300"
        inputClass="border border-gray-300 px-2 py-1 rounded-md"
        confirmButtonClass="bg-blue-600 text-white py-1 rounded-md hover:bg-blue-700"
        cancelButtonClass="bg-gray-200 text-black py-1 rounded-md hover:bg-gray-300"
    />


    <div v-if="isLoading" class="text-center text-gray-500">
      Завантаження графіка...
    </div>
    <div v-else-if="!chartData" class="text-center text-gray-500">
      Дані для графіка відсутні.
    </div>
    <CustomChart
        class="w-full h-96"
        v-else
        :chart-data="chartData"
        :chart-label="chartLabel"
        :chart-unit="chartUnit"
        :chart-min-x="chartMinX"
        :chart-max-x="chartMaxX"
        @clear-points="clearPoints"
    />
  </div>
</template>

<script setup lang="ts">
import { shallowRef, ref, onMounted, watch, computed } from "vue";
import { getAvailableParameters, Parameter } from "@/services/apiService";
import { useRoute } from "vue-router";
import ParameterSelector from "../ParameterSelector.vue";
import CustomChart from "../charts/CustomChart.vue";
import Dropdown from "@/components/Dropdown.vue";
import DateTimePicker from "@/components/DateTimePicker.vue";

const route = useRoute();
const roomId = Number(route.params.roomId);
const isLoading = ref(false);
const selectedParam = ref<string>("");
const chartData = shallowRef<{ labels: number[]; datasets: { data: number[] }[] } | null>(null);
const availableParams = ref<Parameter[]>([]);
const chartLabel = ref<string>("");
const chartUnit = ref<string>("");
const chartMinX = ref<number>(0);
const chartMaxX = ref<number>(100);

const singleDateTime = ref();
const rangeDateTime = ref();

const availableOptions = computed(() => {
  return availableParams.value.map(param => ({
    value: param.name,
    label: getLabel(param.name),
  }));
});

const getLabel = (name: string) => {
  const labels: Record<string, string> = {
    temperature: "Температура",
    humidity: "Вологість",
    pressure: "Тиск",
  };
  return labels[name] || name;
};

const initializeChartData = () => {
  const step = (chartMaxX.value - chartMinX.value) / 4;
  const labels = Array.from({ length: 5 }, (_, i) => chartMinX.value + i * step);
  const data = Array.from({ length: 5 }, () => Math.random() * 100);

  chartData.value = {
    labels,
    datasets: [{ data }],
  };
};

const updateChartData = () => {
  const param = availableParams.value.find(p => p.name === selectedParam.value);
  if (param) {
    chartLabel.value = getLabel(param.name);
    chartUnit.value = param.unit;
    chartMinX.value = param.min_value;
    chartMaxX.value = param.max_value;
  }
  initializeChartData();
};

const clearPoints = () => {
  initializeChartData();
};

const loadParams = async () => {
  isLoading.value = true;
  try {
    availableParams.value = await getAvailableParameters(roomId);
    selectedParam.value = availableParams.value[0]?.name || "";
    updateChartData();
  } catch (error) {
    console.error("Помилка завантаження параметрів:", error);
  } finally {
    isLoading.value = false;
  }
};

onMounted(loadParams);
watch(selectedParam, (newVal, oldVal) => {
  if (newVal !== oldVal) {
    updateChartData();
  }
});
</script>