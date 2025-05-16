<template>
  <div class="items-center flex-grow">
    <div v-if="devices.length !== 0">
      <div class="flex flex-row gap-4 items-center justify-between mb-4">
        <h2 class="text-lg font-semibold text-gray-800">Devices List</h2>

        <Button 
          icon="pi pi-plus" 
          label="Add Device" 
          @click="showAddDialog = true"
          severity="primary"
        />
      </div>

      <div v-if="loading" class="space-y-2">
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
            v-for="device in devices"
            :key="device.id"
            class="cursor-pointer hover:shadow-lg transition-shadow"
            @click="goToDevice(device.id)"
        >
          <template #title>
            <div class="flex items-center gap-2">
              <i class="pi pi-fan text-purple-600"></i>
              <span>Device #{{ device.id }}</span>
            </div>
          </template>
          <template #subtitle>
            <span class="text-sm text-gray-500">Serial number: {{ device.serial_number }}</span>
          </template>
          <template #content>
            <div v-if="device.fan_speed" class="mt-3 space-y-1">
              <div
                  class="flex items-center justify-between text-sm text-gray-700 bg-gray-50 px-3 py-1.5 rounded-md"
              >
                <span class="font-medium">Fan Speed:</span>
                <span>{{ device.fan_speed }}%</span>
              </div>
            </div>
            <p v-else class="text-sm text-gray-400 mt-3">Device offline</p>
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
      <i class="pi pi-slack text-5xl text-gray-400" />
      <h3 class="text-lg font-semibold text-gray-800 mt-4">No devices</h3>
      <p class="text-gray-500 text-sm mt-2 text-center">
        Add your first device to start monitoring.
      </p>
      <div class="mt-4">
        <Button 
          icon="pi pi-plus" 
          label="Add Device" 
          @click="showAddDialog = true"
          severity="primary"
        />
      </div>
    </div>

    <AddDeviceDialog
        v-model="showAddDialog"
        :roomId="roomId"
        @added="refresh"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { getRoomDevices } from "@/services/apiService";
import type { Device } from "@/types/sensor";
import type { PaginationState, PageChangeEvent } from "@/types/pagination";
import Button from 'primevue/button';
import Card from 'primevue/card';
import Paginator from 'primevue/paginator';
import AddDeviceDialog from './AddDeviceDialog.vue';

const route = useRoute();
const router = useRouter();
const roomId = Number(route.params.roomId);

const devices = ref<Device[]>([]);
const loading = ref(true);
const showAddDialog = ref(false);
const pagination = ref<PaginationState>({ total: 0, skip: 0, count: 6 });

const onPageChange = (event: PageChangeEvent) => {
  pagination.value.skip = event.first;
  pagination.value.count = event.rows;
  loadDevices();
};

const goToDevice = (deviceId: number) => {
  router.push(`${route.path}/${deviceId}`);
};

const loadDevices = async () => {
  loading.value = true;
  try {
    const res = await getRoomDevices(roomId, pagination.value.skip, pagination.value.count);
    devices.value = res.data || [];
    pagination.value.total = res.pagination?.total || 0;
  } catch (error) {
    console.error("Error loading devices:", error);
  } finally {
    loading.value = false;
  }
};

const refresh = async () => {
  pagination.value.skip = 0;
  await loadDevices();
};

onMounted(loadDevices);
</script>
