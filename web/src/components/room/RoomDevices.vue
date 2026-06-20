<template>
  <div class="section-page">
    <AppSectionHeader
      title="Devices"
      description="Connected actuators and latest operating state."
    >
      <template v-if="hasDevices && !isReadOnly" #actions>
        <Button
          icon="pi pi-plus"
          label="Add Device"
          @click="showAddDialog = true"
          severity="primary"
        />
      </template>
    </AppSectionHeader>

    <div v-if="loading || hasDevices" class="asset-frame">
      <div v-if="loading" class="asset-list asset-list--loading">
        <Skeleton v-for="i in pagination.count" :key="i" height="3.75rem" />
      </div>

      <div
          v-else
          class="asset-list"
      >
        <div
            v-for="device in devices"
            :key="device.id"
            class="asset-row app-clickable"
            role="button"
            tabindex="0"
            @click="goToDevice(device.id)"
            @keydown.enter="goToDevice(device.id)"
        >
          <span class="asset-row__index">DEV-{{ device.id }}</span>

          <span class="asset-row__copy">
            <span class="asset-row__title">Device #{{ device.id }}</span>
            <span class="asset-row__serial">{{ device.serial_number }}</span>
          </span>

          <span class="asset-row__telemetry">
              <Tag
                v-if="device.fan_speed !== null && device.fan_speed !== undefined"
                severity="info"
                :value="`Fan speed ${formatValue(device.fan_speed)}%`"
                rounded
              />
              <span v-else class="asset-row__muted">No telemetry</span>
          </span>

          <Button
            v-if="!isReadOnly"
            icon="pi pi-trash"
            severity="danger"
            variant="text"
            rounded
            aria-label="Delete device"
            @click.stop="deleteDevice(device.id)"
          />
        </div>
      </div>

      <div v-if="pagination.total > pagination.count" class="asset-pagination">
        <Paginator
          v-model:first="pagination.skip"
          :rows="pagination.count"
          :totalRecords="pagination.total"
          @page="onPageChange"
          template="FirstPageLink PrevPageLink PageLinks NextPageLink LastPageLink"
        />
      </div>
    </div>
    
    <EmptyState
      v-else
      class="section-empty empty-state--fill empty-state--centered"
      title="No devices"
      :description="isReadOnly ? 'Ventilation devices connected to this room and their latest state.' : 'Add a controllable device to automate ventilation and view fan history.'"
      icon="pi pi-slack"
      :action-label="isReadOnly ? undefined : 'Add Device'"
      action-icon="pi pi-plus"
      @action="openAddDeviceDialog"
    />

    <AddDeviceDialog
        v-if="!isReadOnly"
        v-model="showAddDialog"
        :roomId="roomId"
        @added="refresh"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, inject, ref, onBeforeUnmount, onMounted, type ComputedRef } from "vue";
import { useRoute, useRouter } from "vue-router";
import { getRoomDevices, removeDevice as deleteDeviceApi } from "@/services/apiService";
import type { Device } from "@/types/sensor";
import type { PaginationState, PageChangeEvent } from "@/types/pagination";
import {
  applyDeviceLiveEvent,
  patchDeviceSnapshot,
  useRoomLiveStream,
} from "@/composables/useRoomLiveStream";
import AppSectionHeader from "@/components/common/AppSectionHeader.vue";
import EmptyState from "@/components/common/EmptyState.vue";
import Button from 'primevue/button';
import Paginator from 'primevue/paginator';
import Skeleton from 'primevue/skeleton';
import Tag from 'primevue/tag';
import AddDeviceDialog from './AddDeviceDialog.vue';
import { useConfirm } from "primevue/useconfirm";
import { useToast } from "primevue/usetoast";

const route = useRoute();
const router = useRouter();
const roomId = Number(route.params.roomId);

const devices = ref<Device[]>([]);
const loading = ref(true);
const showAddDialog = ref(false);
const pagination = ref<PaginationState>({ total: 0, skip: 0, count: 8 });
const confirm = useConfirm();
const toast = useToast();
const hasDevices = computed(() => devices.value.length !== 0);
const injectedReadOnly = inject<ComputedRef<boolean>>("roomReadOnly", computed(() => false));
const isReadOnly = computed(() => injectedReadOnly.value);

const { data: initialDevicesData } = await useAsyncData(
  `room-${roomId}-devices-page-0`,
  () => getRoomDevices(roomId, 0, pagination.value.count),
);

if (initialDevicesData.value) {
  devices.value = initialDevicesData.value.data || [];
  pagination.value.total = initialDevicesData.value.pagination?.total || 0;
  loading.value = false;
}

const onPageChange = (event: PageChangeEvent) => {
  pagination.value.skip = event.first;
  pagination.value.count = event.rows;
  loadDevices();
};

const formatValue = (value: number | null | undefined) => {
  if (value === null || value === undefined) return "-";
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
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

const openAddDeviceDialog = () => {
  if (isReadOnly.value) return;
  showAddDialog.value = true;
};

const deleteDevice = async (deviceId: number) => {
  if (isReadOnly.value) return;

  confirm.require({
        message: 'Are you sure you want to delete this device?',
        header: 'Confirmation',
        icon: 'pi pi-exclamation-triangle',
        rejectProps: {
          label: 'Cancel',
          severity: 'secondary',
          outlined: true
        },
        acceptProps: {
          label: 'Delete',
          severity: 'danger'
        },
        accept: async () => {
          await deleteDeviceApi(roomId, deviceId);
          await loadDevices();
          toast.add({ severity: 'success', summary: 'Success', detail: 'Device successfully deleted', life: 3000 });
        },
      });
};

const refresh = async () => {
  pagination.value.skip = 0;
  await loadDevices();
};

const liveStream = useRoomLiveStream(roomId, {
  snapshot: (snapshot) => patchDeviceSnapshot(devices, snapshot.devices),
  device: (event) => applyDeviceLiveEvent(devices, event),
  error: (error) => console.error("Device live stream error:", error),
});

onMounted(() => {
  if (!initialDevicesData.value)
    void loadDevices();

  liveStream.start();
});

onBeforeUnmount(() => {
  liveStream.stop();
});
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

.asset-frame {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  min-width: 0;
}

.asset-list {
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
}

.asset-list--loading {
  gap: 1px;
}

.asset-pagination {
  align-items: center;
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-top: 0;
  border-radius: 0 0 var(--app-radius) var(--app-radius);
  display: flex;
  justify-content: center;
}

.asset-list:has(+ .asset-pagination) {
  border-bottom-left-radius: 0;
  border-bottom-right-radius: 0;
}

.section-empty {
  flex: 1;
}

.asset-row {
  align-items: center;
  background: var(--app-surface);
  border-bottom: 1px solid var(--app-border);
  color: inherit;
  cursor: pointer;
  display: grid;
  gap: var(--app-list-gap);
  grid-template-columns: var(--app-row-code-width) minmax(150px, 1fr) minmax(0, 1.3fr) auto;
  min-height: var(--app-row-height);
  padding: 10px var(--app-list-padding-x);
}

.asset-row:last-child {
  border-bottom: 0;
}

.asset-row__index,
.asset-row__serial {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.72rem;
}

.asset-row__title {
  color: var(--app-text-strong);
  display: block;
  font-size: 0.95rem;
  font-weight: 760;
}

.asset-row__copy,
.asset-row__telemetry {
  min-width: 0;
}

.asset-row__telemetry {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  justify-content: flex-start;
}

.asset-row__muted {
  color: var(--app-muted);
  font-size: 0.8125rem;
}

@media (max-width: 760px) {
  .asset-row {
    grid-template-columns: minmax(0, 1fr) auto;
  }

  .asset-row__index {
    display: none;
  }

  .asset-row__telemetry {
    grid-column: 1 / -1;
  }
}
</style>
