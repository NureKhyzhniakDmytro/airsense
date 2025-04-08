<template>
  <div class="space-y-6">
    <!-- Фильтры -->
    <div class="grid md:grid-cols-2 gap-4">
      <!-- Параметр -->
      <div>
        <label class="text-gray-700 font-medium block mb-1">Параметр</label>
        <select
            v-model="selectedParam"
            class="w-full px-4 py-2 rounded-lg border border-gray-300 bg-gray-100 text-gray-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          <option
              v-for="param in availableParams"
              :key="param.name"
              :value="param.name"
          >
            {{ param.label }}
          </option>
        </select>
      </div>

      <!-- Интервал -->
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

    <!-- Даты -->
    <div class="grid md:grid-cols-2 gap-4">
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
    </div>

    <!-- График -->
    <div v-if="isLoading" class="text-center text-gray-500">Завантаження...</div>

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
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import api from "@/api";
import LineChart from "@/components/charts/LineChart.vue";

interface Param {
  name: string;
  label: string;
  unit: string;
}

interface HistoryEntry {
  value: number;
  timestamp: number;
}

const route = useRoute();
const roomId = Number(route.params.roomId);

const isLoading = ref(false);
const selectedParam = ref("device_speed");
const selectedInterval = ref<"minute" | "hour" | "day">("hour");

const toDate = ref(new Date().toISOString().slice(0, 16));
const fromDate = ref(
    new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString().slice(0, 16)
);

const params = ref<Param[]>([
  { name: "device_speed", label: "Швидкість вентиляції", unit: "%" },
]);

const availableParams = computed(() => params.value);
const selectedLabel = computed(
    () => availableParams.value.find(p => p.name === selectedParam.value)?.label || selectedParam.value
);

const chartData = ref<any>(null);

const getLabel = (name: string) => {
  const labels: Record<string, string> = {
    temperature: "Температура",
    humidity: "Вологість",
    co2: "CO₂",
    device_speed: "Швидкість вентиляції",
  };
  return labels[name] || name;
};

const loadChartData = async () => {
  isLoading.value = true;
  chartData.value = null;

  try {
    const from = new Date(fromDate.value).getTime();
    const to = new Date(toDate.value).getTime();

    let res;

    if (selectedParam.value === "device_speed") {
      res = await api.get(`/room/${roomId}/history`, {
        params: { from, to, interval: selectedInterval.value },
      });
    } else {
      res = await api.get(
          `/room/${roomId}/${selectedParam.value}/history`,
          { params: { from, to, interval: selectedInterval.value } }
      );
    }

    const history: HistoryEntry[] = res.data?.data?.[0]?.history || [];

    if (history.length > 0) {
      const unit = availableParams.value.find(p => p.name === selectedParam.value)?.unit || "";

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
    isLoading.value = false;
  }
};

const loadParams = async () => {
  try {
    const res = await api.get(`/room/${roomId}`);
    const fetched: { name: string; unit: string }[] = res.data;

    for (const p of fetched) {
      if (!params.value.find(x => x.name === p.name)) {
        params.value.push({
          name: p.name,
          label: getLabel(p.name),
          unit: p.unit,
        });
      }
    }
  } catch (err) {
    console.error("Помилка при завантаженні параметрів:", err);
  }
};

onMounted(async () => {
  await loadParams();
  await loadChartData();
});

watch(
    [selectedParam, selectedInterval, fromDate, toDate],
    loadChartData
);
</script>
<!--https://dribbble.com/shots/15506112-Curved-Line-Chart-UI-Design-D-->