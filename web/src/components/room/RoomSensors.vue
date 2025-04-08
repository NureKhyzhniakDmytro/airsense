<template>
  <div class="space-y-6">
    <div class="flex justify-between items-center">
      <h2 class="text-xl font-semibold text-gray-800">Список сенсорів</h2>

      <button
          @click="isModalOpen = true"
          class="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition"
      >
        Додати сенсор
      </button>
    </div>

    <!-- Загрузка -->
    <div v-if="isLoading" class="space-y-2">
      <div
          v-for="i in pagination.count"
          :key="i"
          class="h-16 bg-gray-200 animate-pulse rounded-lg"
      ></div>
    </div>

    <!-- Пусто -->
    <div v-else-if="sensors.length === 0" class="text-center text-gray-500">
      Поки немає сенсорів для цієї кімнати.
    </div>

    <!-- Список сенсорів -->
    <ul
        v-else
        class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4"
    >
      <li
          v-for="sensor in sensors"
          :key="sensor.id"
          class="bg-white shadow rounded-lg p-4 border border-gray-200 cursor-pointer hover:bg-gray-50 transition"
          @click="goToSensor(sensor.id)"
      >
        <h3 class="text-lg font-semibold text-gray-800">
          {{ sensor.type_name }}
        </h3>
        <p class="text-sm text-gray-500 mt-1">
          Серійний номер: {{ sensor.serial_number }}
        </p>

        <div v-if="sensor.parameters?.length" class="mt-3 space-y-1">
          <div
              v-for="param in sensor.parameters"
              :key="param.name"
              class="flex items-center justify-between text-sm text-gray-700 bg-gray-100 px-3 py-1 rounded-md"
          >
            <span class="font-medium">{{ getLabel(param.name) }}:</span>
            <span>{{ param.value }}{{ param.unit }}</span>
          </div>
        </div>
        <p v-else class="text-sm text-gray-400 mt-3">Сенсор офлайн</p>
      </li>
    </ul>

    <!-- Пагинация -->
    <div v-if="pagination.total > pagination.count" class="flex justify-center mt-6">
      <Pagination
          :totalPages="totalPages"
          :currentPage="currentPage"
          @prev="prevPage"
          @next="nextPage"
          @page-change="goToPage"
      />
    </div>

    <!-- Модальное окно -->
    <AddSensorModal
        v-if="isModalOpen"
        :roomId="roomId"
        :isOpen="isModalOpen"
        @close="isModalOpen = false"
        @added="refresh"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import api from "@/api";
import Pagination from "@/components/Pagination.vue";
import AddSensorModal from "@/components/AddSensorModal.vue";
import { Sensor } from "@/services/apiService";


const route = useRoute();
const router = useRouter();
const roomId = Number(route.params.roomId);

const sensors = ref<Sensor[]>([]);
const isLoading = ref(true);
const isModalOpen = ref(false);
const pagination = ref({ total: 0, skip: 0, count: 6 });

const currentPage = computed(() => Math.floor(pagination.value.skip / pagination.value.count) + 1);
const totalPages = computed(() => Math.ceil(pagination.value.total / pagination.value.count));

const getLabel = (key: string) => {
  const map: Record<string, string> = {
    temperature: "Температура",
    humidity: "Вологість",
    co2: "CO₂",
    device_speed: "Швидкість вентиляції",
  };
  return map[key] || key;
};

const goToSensor = (sensorId: number) => {
  router.push(`${route.path}/${sensorId}`);
};

const loadSensors = async () => {
  isLoading.value = true;
  try {
    const res = await api.get(`/room/${roomId}/sensor`, {
      params: {
        skip: pagination.value.skip,
        count: pagination.value.count,
      },
    });

    sensors.value = res.data?.data || [];
    pagination.value.total = res.data?.pagination?.total || 0;
  } catch (error) {
    console.error("Помилка завантаження сенсорів:", error);
  } finally {
    isLoading.value = false;
  }
};

const goToPage = (page: number) => {
  pagination.value.skip = (page - 1) * pagination.value.count;
  loadSensors();
};

const prevPage = () => {
  if (pagination.value.skip > 0) {
    pagination.value.skip -= pagination.value.count;
    loadSensors();
  }
};

const nextPage = () => {
  if (pagination.value.skip + pagination.value.count < pagination.value.total) {
    pagination.value.skip += pagination.value.count;
    loadSensors();
  }
};

const refresh = async () => {
  pagination.value.skip = 0;
  await loadSensors();
};

onMounted(loadSensors);
</script>
