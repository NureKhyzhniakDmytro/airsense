<template>
  <div class="container max-w-7xl mx-auto py-6 space-y-6">
    <div v-if="isLoading" class="text-center text-gray-500">
      Завантаження інформації про пристрій...
    </div>

    <div v-else-if="!device" class="text-center text-gray-500">
      Пристрій не знайдено.
    </div>

    <div v-else>
      <div class="flex justify-between items-center">
        <div>
          <h1 class="text-3xl font-bold text-gray-800">Пристрій №{{ device.id }}</h1>
          <p class="text-sm text-gray-500 mt-1">Серійний номер: {{ device.serial_number }}</p>
        </div>
      </div>

      <!-- Параметр (скорость) -->
      <div class="mt-6">
        <h2 class="text-lg font-semibold text-gray-700 mb-2">Параметри</h2>
        <div v-if="device.fan_speed" class="grid grid-cols-1 gap-4">
          <div class="p-4 bg-white rounded-lg shadow border border-gray-200">
            <div class="text-gray-500 text-sm mb-1">Швидкість</div>
            <div class="text-2xl font-bold text-gray-800">
              {{ device.fan_speed }}%
            </div>
          </div>
        </div>
        <p v-else class="text-gray-400">Пристрій офлайн.</p>
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
            Швидкість (%)
          </h3>
          <LineChart :data="chartData" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { DevicesResponse, Device } from "@/services/apiService";
import api from "@/api";
import LineChart from "@/components/charts/LineChart.vue";
import { useDeviceStore } from "@/store/deviceStore";

interface HistoryEntry {
  value: number;
  timestamp: number;
}

const route = useRoute();
const router = useRouter();
const deviceStore = useDeviceStore();
const deviceId = Number(route.params.deviceId);
const roomId = Number(route.params.roomId);
const isLoading = ref(true);
const isChartLoading = ref(false);
const device = ref<Device | null>(null);
const selectedInterval = ref<"minute" | "hour" | "day">("hour");
const toDate = ref(new Date().toISOString().slice(0, 16));
const fromDate = ref(
    new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString().slice(0, 16)
);
const chartData = ref<any>(null);

const loadDevice = async () => {
  isLoading.value = true;
  try {
    device.value = await deviceStore.fetchDevice(roomId, deviceId);
  } catch (error) {
    console.error("Помилка завантаження пристрою:", error);
  } finally {
    isLoading.value = false;
  }
};

const loadChartData = async () => {
  if (!device.value) return;

  isChartLoading.value = true;
  chartData.value = null;

  try {
    const from = new Date(fromDate.value).getTime();
    const to = new Date(toDate.value).getTime();

    const res = await api.get(
        `/room/${roomId}/history/${deviceId}`,
        { params: { from, to, interval: selectedInterval.value } }
    );

    const history: HistoryEntry[] = res.data?.data?.history || [];
    if (history.length > 0) {
      chartData.value = {
        labels: history.map(h => new Date(h.timestamp * 1000).toLocaleTimeString()),
        datasets: [
          {
            label: 'Швидкість (%)',
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
  await loadDevice();
  await loadChartData();
});

watch(
    [selectedInterval, fromDate, toDate],
    loadChartData
);
</script>