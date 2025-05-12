<template>
  <div class="items-center flex-grow">
    <div v-if="sensors.length !== 0">
      <div class="flex flex-row gap-4 items-center justify-between mb-4">
        <h2 class="text-lg font-semibold text-gray-800">Sensors List</h2>

        <Button 
          icon="pi pi-plus" 
          label="Add Sensor" 
          @click="addSensorDialog = true"
          severity="primary"
        />
      </div>

      <div v-if="isLoading" class="space-y-2">
        <div
            v-for="i in pagination.count"
            :key="i"
            class="h-16 bg-gray-200 animate-pulse rounded-lg"
        ></div>
      </div>

      <div
          v-else
          class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4"
      >
        <Card
            v-for="sensor in sensors"
            :key="sensor.id"
            class="cursor-pointer hover:shadow-lg transition-shadow"
            @click="goToSensor(sensor.id)"
        >
          <template #title>
            <div class="flex items-center gap-2">
              <i class="pi pi-sensor text-purple-600"></i>
              <span>{{ sensor.type_name }}</span>
            </div>
          </template>
          <template #subtitle>
            <span class="text-sm text-gray-500">Serial number: {{ sensor.serial_number }}</span>
          </template>
          <template #content>
            <div v-if="sensor.parameters?.length" class="mt-3 space-y-1">
              <div
                  v-for="param in sensor.parameters"
                  :key="param.name"
                  class="flex items-center justify-between text-sm text-gray-700 bg-gray-50 px-3 py-1.5 rounded-md"
              >
                <span class="font-medium">{{ getLabel(param.name) }}:</span>
                <span>{{ param.value }}{{ param.unit }}</span>
              </div>
            </div>
            <p v-else class="text-sm text-gray-400 mt-3">Sensor offline</p>
          </template>
        </Card>
      </div>

      <div v-if="pagination.total > pagination.count" class="flex justify-center mt-6">
        <Paginator
          v-model:first="pagination.skip"
          :rows="pagination.count"
          :totalRecords="pagination.total"
          @page="onPageChange"
          template="FirstPageLink PrevPageLink PageLinks NextPageLink LastPageLink"
        />
      </div>
    </div>
    
    <div v-else class="flex flex-col items-center justify-center h-full">
      <i class="pi pi-bullseye text-5xl text-gray-400" />
      <h3 class="text-lg font-semibold text-gray-800 mt-4">No sensors</h3>
      <p class="text-gray-500 text-sm mt-2 text-center">
        Add your first sensor to start monitoring.
      </p>
      <div class="mt-4">
        <Button 
          icon="pi pi-plus" 
          label="Add Sensor" 
          @click="addSensorDialog = true"
          severity="primary"
        />
      </div>
    </div>

    <AddSensorDialog
        v-model="addSensorDialog"
        :roomId="roomId"
        @added="refresh"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import api from "@/api";
import AddSensorDialog from "@/components/room/AddSensorDialog.vue";
import { Sensor } from "@/services/apiService";
import Button from 'primevue/button';
import Card from 'primevue/card';
import Paginator from 'primevue/paginator';

const route = useRoute();
const router = useRouter();
const roomId = Number(route.params.roomId);

const sensors = ref<Sensor[]>([]);
const isLoading = ref(true);
const addSensorDialog = ref(false);
const pagination = ref({ total: 0, skip: 0, count: 6 });

const onPageChange = (event: { first: number, rows: number }) => {
  pagination.value.skip = event.first;
  pagination.value.count = event.rows;
  loadSensors();
};

const getLabel = (key: string) => {
  const map: Record<string, string> = {
    temperature: "Temperature",
    humidity: "Humidity",
    co2: "CO₂",
    device_speed: "Ventilation speed",
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

const refresh = async () => {
  pagination.value.skip = 0;
  await loadSensors();
};

onMounted(loadSensors);
</script>
