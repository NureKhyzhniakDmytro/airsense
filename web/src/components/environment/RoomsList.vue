<template>
  <div
      class="items-center flex-grow"
      :class="{ 'place-content-center': rooms.length === 0 }"
  >
    <div v-if="rooms.length !== 0">
      <div class="flex justify-between items-center">
        <div>
          <h2 class="text-xl font-semibold text-gray-800">Rooms</h2>
          <h2 class="text-sm text-gray-500">List of all rooms in your environment.</h2>
        </div>

        <Button 
            @click="createRoomDialog = true" 
            label="New Room"
            icon="pi pi-plus" 
            :disabled="environment?.role === 'user'"
        />
        <create-room-dialog v-model="createRoomDialog" :envId="envId" />
      </div>

      <DataView
          :value="rooms"
          :total-records="pagination.total"
          @page="changePage"
          paginator
          :rows="pagination.count"
          :pt="{ content: 'rounded-t-xl', pcPaginator: {
            root: 'rounded-b-xl rounded-none'
          } }"
          class = "mt-8"
      >
        <template #list="slotProps">
          <div class="flex flex-col rounded-md">
            <div
                v-if="!isLoading"
                v-for="(item, index) in slotProps.items"
                v-ripple
                :key="index"
                class="p-6 flex justify-between items-center cursor-pointer hover:bg-surface-200 transition"
                :class="{
                  'border-t border-surface-200': index !== 0,
                  'rounded-t-xl': index === 0,
                  }"
                @click="goToRoom(item.id)"
            >
              <div>
                <span class="text-lg font-medium text-gray-800 cursor-pointer">
                  {{ item.name }}
                </span>

                <div v-if="item.parameters" class="mt-2 flex flex-wrap gap-2">
                  <div
                      v-if="item.device_speed !== undefined"
                      class="text-sm bg-gray-200 px-2 py-1 rounded-lg text-gray-700"
                  >
                    Fan speed:
                    <span class="font-medium text-gray-800">{{ item.device_speed }}%</span>
                  </div>
                  <Divider layout="vertical" class="m-0" />
                  <div
                    v-for="param in item.parameters"
                    :key="param.name"
                    class="bg-gray-200 px-2 py-1 rounded-lg text-sm text-gray-700"
                  >
                    <span class="font-medium">{{ getParameterLabel(param.name) }}:</span>
                    <span class="ml-1">{{ param.value }}{{ param.unit }}</span>
                  </div>
                </div>
              </div>
              <i class="pi pi-angle-right place-self-center text-muted-color group-hover:text-color" />
            </div>

            <div
                v-if="isLoading"
                v-for="index in pagination.count"
                :key="index"
                class="p-6 flex justify-between items-center cursor-pointer hover:bg-surface-200 transition"
                :class="{
                  'border-t border-surface-200': index !== 1,
                  'rounded-t-xl': index === 1,
                  }"
            >
              <div>
                <Skeleton width="8rem" height="1.25rem" class="my-2" />

                <div class="mt-2 flex flex-wrap gap-2">
                  <Skeleton width="5rem" height="1.7rem" />
                  <Divider layout="vertical" class="m-0" />
                  <Skeleton width="5rem" height="1.7rem" v-for="_ in 3" />
                </div>
              </div>
              <i class="pi pi-angle-right place-self-center text-muted-color group-hover:text-color" />
            </div>
          </div>
        </template>
      </DataView>
    </div>

    <div v-else class="flex flex-col items-center">
      <i class="pi pi-plus text-5xl text-gray-400" />
      <h3 class="text-lg font-semibold text-gray-800 mt-4">No rooms</h3>
      <p class="text-gray-500 text-sm mt-2 text-center">
        Start by creating a new room.
      </p>
      <div class="mt-4">
        <Button @click="createRoomDialog = true" label="New Room" />
        <create-room-dialog v-model="createRoomDialog" :envId="envId" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useEnvironmentStore } from "@/store/environmentStore";
import { getRooms } from "@/services/apiService";
import type { Environment } from "@/types/environment";
import type { Room } from "@/types/room";
import CreateRoomDialog from "@/components/environment/CreateRoomDialog.vue";
import Button from "primevue/button";
import DataView, { type DataViewPageEvent } from "primevue/dataview";
import Divider from 'primevue/divider';
import Skeleton from "primevue/skeleton";

const router = useRouter();
const route = useRoute();
const environmentStore = useEnvironmentStore();
const envId = Number(route.params.envId);
const rooms = ref<Room[]>([]);
const pagination = ref({ total: 0, skip: 0, count: 3 });
const isLoading = ref(true);
const environment = ref<Environment>();
let refreshInterval: ReturnType<typeof setInterval> | null = null;
const createRoomDialog = ref(false);
let currentPage = 0;

const getParameterLabel = (name: string) => {
  const labels: Record<string, string> = {
    temperature: "Temperature",
    humidity: "Humidity",
    co2: "CO₂",
  };
  return labels[name] || name;
};

const startAutoRefresh = () => {
  refreshInterval = setInterval(
      async () => await changePage({ page: currentPage } as DataViewPageEvent), 10_000
  );
};

const stopAutoRefresh = () => {
  if (refreshInterval) {
    clearInterval(refreshInterval);
    refreshInterval = null;
  }
};

const goToRoom = (roomId: number) => {
  if (environment.value?.role === 'user') {
    return;
  }
  router.push({
    name: 'room',
    params: {
      envId,
      roomId
    }
  });
};

onMounted(async () => {
  if (!environment.value) {
    environment.value = await environmentStore.fetchEnvironment(envId);
  }
  await changePage({ page: 0 } as DataViewPageEvent);
  startAutoRefresh();
});

const changePage = async (event: DataViewPageEvent) => {
  currentPage = event.page;

  isLoading.value = true;
  if (!environment.value) return;

  const { rooms: roomList, pagination: pag } = await getRooms(envId, pagination.value.skip, pagination.value.count);
  roomList.forEach((room) => {
    const index = rooms.value.findIndex(r => r.id === room.id);
    if (index !== -1) {
      rooms.value[index] = room;
    } else {
      rooms.value.push(room);
    }
  });
  pagination.value.total = pag.total;

  isLoading.value = false;
};

onUnmounted(stopAutoRefresh);
</script>
