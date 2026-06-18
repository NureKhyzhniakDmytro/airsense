<template>
  <div class="section-page">
    <AppSectionHeader
      title="Rooms"
      description="Room telemetry and device groups in this environment."
    >
      <template v-if="rooms.length !== 0" #actions>
        <Button
          @click="createRoomDialog = true"
          label="New Room"
          icon="pi pi-plus"
          :disabled="environment?.role === 'user'"
        />
      </template>
    </AppSectionHeader>

    <div v-if="isLoading || rooms.length !== 0" class="collection-frame">
      <div v-if="isLoading" class="section-list section-list--loading">
        <Skeleton v-for="index in pagination.count" :key="index" height="3.75rem" />
      </div>

      <DataView
        v-else
        :value="rooms"
        :total-records="pagination.total"
        @page="changePage"
        paginator
        :rows="pagination.count"
        class="section-list"
      >
        <template #list="slotProps">
          <div class="flex flex-col">
            <button
              v-if="!isLoading"
              v-for="(item, index) in slotProps.items"
              v-ripple
              :key="item.id"
              type="button"
              class="room-row app-clickable"
              :class="{ 'border-t border-surface-200': index !== 0 }"
              @click="goToRoom(item.id)"
            >
              <span class="room-row__identity">
                <PlaceIcon :name="item.icon" size="sm" />
                <span>ROOM-{{ item.id }}</span>
              </span>

              <span class="room-row__copy">
                <span class="room-row__title">{{ item.name }}</span>
                <span class="room-row__meta">
                  <Tag
                    v-if="item.device_speed !== null && item.device_speed !== undefined"
                    severity="info"
                    :value="`Fan ${formatValue(item.device_speed)}%`"
                    rounded
                  />
                  <Tag
                    v-for="param in item.parameters || []"
                    :key="param.name"
                    severity="secondary"
                    :value="`${getParameterLabel(param.name)} ${formatValue(param.value)}${param.unit}`"
                    rounded
                  />
                  <span v-if="!item.parameters?.length && item.device_speed == null" class="room-row__muted">
                    Waiting for telemetry
                  </span>
                </span>
              </span>

              <i class="pi pi-angle-right text-muted-color" />
            </button>

          </div>
        </template>
      </DataView>
    </div>

    <EmptyState
      v-else
      class="section-empty"
      title="No rooms"
      description="Create a room to connect sensors, devices, and automation curves."
      icon="pi pi-home"
      action-label="New Room"
      action-icon="pi pi-plus"
      :disabled="environment?.role === 'user'"
      @action="createRoomDialog = true"
    />

    <create-room-dialog v-model="createRoomDialog" :envId="envId" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useEnvironmentStore } from "@/store/environmentStore";
import { getRooms } from "@/services/apiService";
import type { Environment } from "@/types/environment";
import type { Room } from "@/types/room";
import { PARAMETER_LABELS } from "@/types/sensor";
import CreateRoomDialog from "@/components/environment/CreateRoomDialog.vue";
import AppSectionHeader from "@/components/common/AppSectionHeader.vue";
import EmptyState from "@/components/common/EmptyState.vue";
import PlaceIcon from "@/components/common/PlaceIcon.vue";
import Button from "primevue/button";
import DataView, { type DataViewPageEvent } from "primevue/dataview";
import Skeleton from "primevue/skeleton";
import Tag from "primevue/tag";

const router = useRouter();
const route = useRoute();
const environmentStore = useEnvironmentStore();
const envId = Number(route.params.envId);
const rooms = ref<Room[]>([]);
const pagination = ref({ total: 0, skip: 0, count: 8 });
const isLoading = ref(true);
const environment = ref<Environment | null>(null);
let refreshInterval: ReturnType<typeof setInterval> | null = null;
const createRoomDialog = ref(false);
let currentPage = 0;

const getParameterLabel = (name: string) => {
  return PARAMETER_LABELS[name] || name;
};

const formatValue = (value: number | null | undefined) => {
  if (value === null || value === undefined) return "-";
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
};

const { data: initialRoomsData } = await useAsyncData(
  `environment-${envId}-rooms-page-0`,
  async () => {
    const env = await environmentStore.fetchEnvironment(envId);
    const result = await getRooms(envId, 0, pagination.value.count);
    return { environment: env, ...result };
  },
);

if (initialRoomsData.value) {
  environment.value = initialRoomsData.value.environment;
  rooms.value = initialRoomsData.value.rooms;
  pagination.value = initialRoomsData.value.pagination;
  isLoading.value = false;
}

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
  await changePage({ page: currentPage } as DataViewPageEvent);
  startAutoRefresh();
});

const changePage = async (event: DataViewPageEvent) => {
  currentPage = event.page ?? Math.floor((event.first ?? 0) / pagination.value.count);

  isLoading.value = true;
  if (!environment.value) {
    environment.value = await environmentStore.fetchEnvironment(envId);
  }

  const skip = currentPage * pagination.value.count;
  const { rooms: roomList, pagination: pag } = await getRooms(envId, skip, pagination.value.count);
  rooms.value = roomList;
  pagination.value = pag;

  isLoading.value = false;
};

onUnmounted(stopAutoRefresh);
</script>

<style scoped>
.section-page {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: var(--app-gap-md);
  min-height: 0;
  min-width: 0;
  width: 100%;
}

.collection-frame {
  display: flex;
  flex: 1;
  min-height: 0;
  min-width: 0;
}

.section-list {
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
  width: 100%;
}

.section-list--loading {
  gap: 1px;
  padding: 0;
}

.section-list :deep(.p-dataview-content) {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
}

.section-list :deep(.p-dataview-content > div),
.section-list :deep(.p-dataview-content .flex.flex-col) {
  display: flex;
  flex: 1;
  flex-direction: column;
}

.section-list :deep(.p-paginator) {
  border-top: 1px solid var(--app-border);
  flex: 0 0 auto;
}

.section-empty {
  flex: 1;
}

.room-row {
  align-items: center;
  background: var(--app-surface);
  border: 0;
  color: inherit;
  display: grid;
  gap: var(--app-list-gap);
  grid-template-columns: var(--app-row-code-width) minmax(0, 1fr) auto;
  min-height: var(--app-row-height);
  padding: 10px var(--app-list-padding-x);
  text-align: left;
  width: 100%;
}

.room-row:hover {
  background: var(--app-surface-soft);
}

.room-row__identity {
  align-items: center;
  color: var(--app-muted);
  display: flex;
  gap: 9px;
  font-family: var(--app-mono);
  font-size: 0.72rem;
  font-weight: 600;
  height: 100%;
  justify-content: flex-start;
}

.room-row__copy,
.room-row__meta {
  min-width: 0;
}

.room-row__copy {
  display: flex;
  flex-direction: column;
  gap: 7px;
}

.room-row__title {
  color: var(--app-text-strong);
  font-size: 0.95rem;
  font-weight: 760;
}

.room-row__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.room-row__muted {
  color: var(--app-muted);
  font-size: 0.8125rem;
}

@media (max-width: 620px) {
  .room-row {
    grid-template-columns: minmax(0, 1fr);
  }

  .room-row__identity {
    display: none;
  }

  .room-row > .pi-angle-right {
    display: none;
  }
}
</style>
