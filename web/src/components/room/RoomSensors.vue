<template>
  <div class="section-page">
    <AppSectionHeader
      title="Sensors"
      description="Connected sensor modules and latest parameter values."
    >
      <template v-if="hasSensors" #actions>
        <Button
          icon="pi pi-plus"
          label="Add Sensor"
          @click="addSensorDialog = true"
          severity="primary"
        />
      </template>
    </AppSectionHeader>

    <div v-if="isLoading || hasSensors" class="asset-frame">
      <div v-if="isLoading" class="asset-list asset-list--loading">
        <Skeleton v-for="i in pagination.count" :key="i" height="3.75rem" />
      </div>

      <div
          v-else
          class="asset-list"
      >
        <div
            v-for="sensor in sensors"
            :key="sensor.id"
            class="asset-row app-clickable"
            role="button"
            tabindex="0"
            @click="goToSensor(sensor.id)"
            @keydown.enter="goToSensor(sensor.id)"
        >
          <span class="asset-row__index">SNS-{{ sensor.id }}</span>

          <span class="asset-row__copy">
            <span class="asset-row__title">{{ sensor.type_name }}</span>
            <span class="asset-row__serial">{{ sensor.serial_number }}</span>
          </span>

          <span class="asset-row__telemetry">
            <template v-if="sensor.parameters?.length">
              <Tag
                v-for="param in sensor.parameters"
                :key="param.name"
                severity="secondary"
                :value="`${getLabel(param.name)} ${formatValue(param.value)}${param.unit}`"
                rounded
              />
            </template>
            <span v-else class="asset-row__muted">No telemetry</span>
          </span>

          <Button
            icon="pi pi-trash"
            severity="danger"
            variant="text"
            rounded
            aria-label="Delete sensor"
            @click.stop="deleteSensor(sensor.id)"
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
      title="No sensors"
      description="Add a sensor to collect room telemetry and feed automation decisions."
      icon="pi pi-bullseye"
      action-label="Add Sensor"
      action-icon="pi pi-plus"
      @action="addSensorDialog = true"
    />

    <AddSensorDialog
        v-model="addSensorDialog"
        :roomId="roomId"
        @added="refresh"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { getRoomSensors, removeSensor as deleteSensorApi } from "@/services/apiService";
import type { Sensor } from "@/types/sensor";
import { PARAMETER_LABELS } from "@/types/sensor";
import type { PaginationState, PageChangeEvent } from "@/types/pagination";
import AppSectionHeader from "@/components/common/AppSectionHeader.vue";
import EmptyState from "@/components/common/EmptyState.vue";
import Button from 'primevue/button';
import Paginator from 'primevue/paginator';
import Skeleton from 'primevue/skeleton';
import Tag from 'primevue/tag';
import AddSensorDialog from './AddSensorDialog.vue';
import { useConfirm } from "primevue/useconfirm";
import { useToast } from "primevue/usetoast";

const route = useRoute();
const router = useRouter();
const roomId = Number(route.params.roomId);

const sensors = ref<Sensor[]>([]);
const isLoading = ref(true);
const addSensorDialog = ref(false);
const pagination = ref<PaginationState>({ total: 0, skip: 0, count: 8 });
const confirm = useConfirm();
const toast = useToast();
const hasSensors = computed(() => sensors.value.length !== 0);

const { data: initialSensorsData } = await useAsyncData(
  `room-${roomId}-sensors-page-0`,
  () => getRoomSensors(roomId, 0, pagination.value.count),
);

if (initialSensorsData.value) {
  sensors.value = initialSensorsData.value.data || [];
  pagination.value.total = initialSensorsData.value.pagination?.total || 0;
  isLoading.value = false;
}

const onPageChange = (event: PageChangeEvent) => {
  pagination.value.skip = event.first;
  pagination.value.count = event.rows;
  loadSensors();
};

const getLabel = (key: string) => {
  return PARAMETER_LABELS[key] || key;
};

const formatValue = (value: number | null | undefined) => {
  if (value === null || value === undefined) return "-";
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
};

const goToSensor = (sensorId: number) => {
  router.push(`${route.path}/${sensorId}`);
};

const loadSensors = async () => {
  isLoading.value = true;
  try {
    const res = await getRoomSensors(roomId, pagination.value.skip, pagination.value.count);
    sensors.value = res.data || [];
    pagination.value.total = res.pagination?.total || 0;
  } catch (error) {
    console.error("Error loading sensors:", error);
  } finally {
    isLoading.value = false;
  }
};

const deleteSensor = async (sensorId: number) => {
  confirm.require({
        message: 'Are you sure you want to delete this sensor?',
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
          await deleteSensorApi(roomId, sensorId);
          await loadSensors();
          toast.add({ severity: 'success', summary: 'Success', detail: 'Sensor successfully deleted', life: 3000 });
        },
      });
};

const refresh = async () => {
  pagination.value.skip = 0;
  await loadSensors();
};

onMounted(loadSensors);
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
