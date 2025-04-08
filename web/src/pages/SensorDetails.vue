<template>
  <div class="container max-w-7xl mx-auto py-6 space-y-6">
    <div v-if="isLoading" class="text-center text-gray-500">
      Завантаження інформації про сенсор...
    </div>

    <div v-else-if="!sensor" class="text-center text-gray-500">
      Сенсор не знайдено.
    </div>

    <div v-else>
      <div class="flex justify-between items-center">
        <div>
          <h1 class="text-3xl font-bold text-gray-800">{{ sensor.type_name }}</h1>
          <p class="text-sm text-gray-500 mt-1">Серійний номер: {{ sensor.serial_number }}</p>
        </div>
      </div>

      <div class="mt-4">
        <h2 class="text-lg font-semibold text-gray-700 mb-2">Параметри</h2>
        <div class="flex flex-wrap gap-2">
          <span
              v-for="type in sensor.types"
              :key="type"
              @click="selectType(type)"
              :class="[
                'px-3 py-1 text-sm rounded-full cursor-pointer transition-all',
                getTypeStyles(type),
                selectedParam === type ? 'ring-2 ring-blue-500 ring-offset-2' : ''
              ]"
          >
            {{ getLabel(type) }}
          </span>
        </div>
      </div>

      <!-- Параметри -->
      <div class="mt-6">
        <div v-if="sensor.parameters?.length" class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div
              v-for="param in sensor.parameters"
              :key="param.name"
              class="p-4 bg-white rounded-lg shadow border border-gray-200"
          >
            <div class="text-gray-500 text-sm mb-1">{{ getLabel(param.name) }}</div>
            <div class="text-2xl font-bold text-gray-800">
              {{ param.value }}{{ param.unit }}
            </div>
          </div>
        </div>
        <p v-else class="text-gray-400">Сенсор офлайн.</p>
      </div>

      <div class="mt-6 space-y-6">
        <div class="grid md:grid-cols-3 gap-4">
          <div>
            <label class="text-gray-700 font-medium block mb-1">Від</label>
            <input
                type="datetime-local"
                v-model="fromDate"
                class="w-full px-4 py-2 rounded-lg border border-gray-300 bg-gray-100 text-gray-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div>
            <label class="text-gray-700 font-medium block mb-1">До</label>
            <input
                type="datetime-local"
                v-model="toDate"
                class="w-full px-4 py-2 rounded-lg border border-gray-300 bg-gray-100 text-gray-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div>
            <label class="text-gray-700 font-medium block mb-1">Інтервал</label>
            <select
                v-model="selectedInterval"
                class="w-full px-4 py-2 rounded-lg border border-gray-300 bg-gray-100 text-gray-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="minute">Хвилина</option>
              <option value="hour">Година</option>
              <option value="day">День</option>
            </select>
          </div>
        </div>

        <!-- График -->
        <div v-if="isChartLoading" class="text-center text-gray-500">Завантаження графіка...</div>
        <div v-else-if="!chartData" class="text-center text-gray-500">
          Дані для графіка відсутні.
        </div>
        <div v-else class="bg-white shadow-md rounded-lg p-4">
          <h3 class="text-lg font-semibold text-gray-800 mb-4">
            {{ selectedLabel }}
          </h3>
          <LineChart :data="chartData" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { getAvailableParameters, Sensor, Parameter } from "@/services/apiService";
import api from "@/api";
import LineChart from "@/components/charts/LineChart.vue";
import { useSensorStore } from "@/store/sensorStore";

interface HistoryEntry {
  value: number;
  timestamp: number;
}

const route = useRoute();
const router = useRouter();
const sensorStore = useSensorStore();
const sensorId = Number(route.params.sensorId);
const roomId = Number(route.params.roomId);
const isLoading = ref(true);
const isChartLoading = ref(false);
const sensor = ref<Sensor | null>(null);
const params = ref<Parameter[]>([]);
const selectedParam = ref<string>("");
const selectedInterval = ref<"minute" | "hour" | "day">("hour");
const toDate = ref(new Date().toISOString().slice(0, 16));
const fromDate = ref(
    new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString().slice(0, 16)
);
const chartData = ref<any>(null);

const loadSensor = async () => {
  isLoading.value = true;
  try {
    sensor.value = await sensorStore.fetchSensor(roomId, sensorId);
    const parameters = await getAvailableParameters(roomId);
    for (const parameter of parameters)
      if (sensor.value?.types.some((el) => el === parameter.name))
        params.value.push(parameter);
    selectedParam.value = sensor.value?.types[0] || "";
  } catch (error) {
    console.error("Помилка завантаження сенсора:", error);
  } finally {
    isLoading.value = false;
  }
};

const getLabel = (key: string) => {
  const map: Record<string, string> = {
    temperature: "Температура",
    humidity: "Вологість",
    co2: "CO₂",
    device_speed: "Швидкість вентиляції",
  };
  return map[key] || key;
};

const getTypeStyles = (type: string) => {
  const styleMap: Record<string, string> = {
    temperature: 'bg-red-100 text-red-700',
    humidity: 'bg-blue-100 text-blue-700',
    co2: 'bg-green-100 text-green-700',
    device_speed: 'bg-purple-100 text-purple-700',
  };
  return styleMap[type] || 'bg-gray-100 text-gray-700';
};

const selectType = (type: string) => {
  selectedParam.value = type;
};

const selectedLabel = computed(() => getLabel(selectedParam.value));

const loadChartData = async () => {
  if (!sensor.value || !selectedParam.value) return;

  isChartLoading.value = true;
  chartData.value = null;

  try {
    const from = new Date(fromDate.value).getTime();
    const to = new Date(toDate.value).getTime();

    const res = await api.get(
        `/room/${roomId}/${selectedParam.value}/history/${sensorId}`,
        { params: { from, to, interval: selectedInterval.value } }
    );

    const history: HistoryEntry[] = res.data?.data?.history || [];
    if (history.length > 0) {
      const unit = params.value.find(p => p.name === selectedParam.value)?.unit || "";

      chartData.value = {
        labels: history.map(h => new Date(h.timestamp * 1000).toLocaleTimeString()),
        datasets: [
          {
            label: `${getLabel(selectedParam.value)} (${unit})`,
            data: history.map(h => h.value),
            borderColor: "#3B82F6",
            backgroundColor: "#3B82F6",
            fill: false,
            tension: 0.3,
          },
        ],
      };
    }
  } catch (err) {
    console.error("Помилка при завантаженні графіка:", err);
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