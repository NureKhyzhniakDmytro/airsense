<template>
  <div class="demo-page">
    <AppPageHeader
      title="Demo data"
      eyebrow="Simulation control"
      description="Manage the demo device stream used for forecasts, charts, automation checks, and live room views."
    >
      <template #badge>
        <Tag :value="statusLabel" :severity="statusSeverity" />
      </template>

      <template #actions>
        <Button
          icon="pi pi-refresh"
          label="Refresh"
          severity="secondary"
          :loading="isLoading"
          @click="loadStatus"
        />
        <Button
          icon="pi pi-sitemap"
          label="Prepare topology"
          :loading="isBootstrapping"
          @click="bootstrap"
        />
      </template>
    </AppPageHeader>

    <div class="demo-metrics">
      <MetricCard
        label="Rooms"
        :value="status?.metrics.room_count ?? 0"
        icon="pi pi-home"
        :hint="`${status?.metrics.sensor_count ?? 0} sensors, ${status?.metrics.device_count ?? 0} devices`"
      />
      <MetricCard
        label="Telemetry rows"
        :value="formatNumber(status?.metrics.sensor_data_rows ?? 0)"
        icon="pi pi-chart-line"
        :hint="latestTelemetryHint"
      />
      <MetricCard
        label="Device rows"
        :value="formatNumber(status?.metrics.device_data_rows ?? 0)"
        icon="pi pi-sliders-h"
        :hint="latestDeviceHint"
      />
      <MetricCard
        label="Active profiles"
        :value="activeProfiles"
        icon="pi pi-bolt"
        hint="Room-level overrides"
      />
    </div>

    <section class="demo-workbench">
      <aside class="app-panel demo-control-panel">
        <div class="demo-section-heading">
          <div>
            <h2>History generation</h2>
            <p>Backfill ordinary sensor and device history for charts and model training.</p>
          </div>
        </div>

        <div class="demo-form-grid">
          <label class="demo-field">
            <span>Hours</span>
            <InputNumber v-model="backfillForm.hours" :min="1" :max="168" show-buttons fluid />
          </label>

          <label class="demo-field">
            <span>Step, min</span>
            <InputNumber v-model="backfillForm.interval_minutes" :min="1" :max="60" show-buttons fluid />
          </label>

          <label class="demo-field demo-field--wide">
            <span>Scenario</span>
            <Select
              v-model="backfillForm.scenario"
              :options="backfillScenarioOptions"
              option-label="label"
              option-value="value"
              fluid
            />
          </label>
        </div>

        <div class="demo-action-row">
          <Button
            icon="pi pi-database"
            label="Generate history"
            :loading="isBackfilling"
            @click="backfill"
          />
          <Button
            icon="pi pi-trash"
            label="Clear history"
            severity="danger"
            outlined
            :loading="isResetting"
            @click="confirmReset"
          />
        </div>
      </aside>

      <aside class="app-panel demo-control-panel demo-control-panel--builder">
        <div class="demo-section-heading">
          <div>
            <h2>Topology builder</h2>
            <p>Create demo rooms with real sensors, ventilation devices, and starting telemetry values.</p>
          </div>
        </div>

        <div class="demo-form-grid">
          <label class="demo-field demo-field--wide">
            <span>Room name</span>
            <InputText v-model="roomForm.name" maxlength="40" placeholder="Mixing room" fluid />
          </label>

          <label class="demo-field demo-field--wide">
            <span>Room type</span>
            <Select
              v-model="roomForm.icon"
              :options="roomIconOptions"
              option-label="label"
              option-value="value"
              fluid
            >
              <template #value="{ value }">
                <div class="place-select-value">
                  <PlaceIcon :name="value || roomForm.icon" size="sm" />
                  <span>{{ getPlaceIconOption(value || roomForm.icon).label }}</span>
                </div>
              </template>
              <template #option="{ option }">
                <div class="place-select-option">
                  <PlaceIcon :name="option.value" size="sm" />
                  <span>
                    <strong>{{ option.label }}</strong>
                    <small>{{ option.description }}</small>
                  </span>
                </div>
              </template>
            </Select>
          </label>

          <label class="demo-field">
            <span>Sensors</span>
            <InputNumber v-model="roomForm.sensor_count" :min="0" :max="20" show-buttons fluid />
          </label>

          <label class="demo-field">
            <span>Vent devices</span>
            <InputNumber v-model="roomForm.device_count" :min="0" :max="20" show-buttons fluid />
          </label>
        </div>

        <div class="demo-subsection-label">Initial values</div>

        <div class="demo-form-grid demo-form-grid--readings">
          <label class="demo-field">
            <span>CO₂</span>
            <InputNumber v-model="roomForm.readings.co2" :min="300" :max="5000" suffix=" ppm" fluid />
          </label>

          <label class="demo-field">
            <span>Temp</span>
            <InputNumber v-model="roomForm.readings.temperature" :min="-50" :max="50" suffix=" °C" :max-fraction-digits="1" fluid />
          </label>

          <label class="demo-field">
            <span>Humidity</span>
            <InputNumber v-model="roomForm.readings.humidity" :min="0" :max="100" suffix="%" fluid />
          </label>

          <label class="demo-field">
            <span>Vent</span>
            <InputNumber v-model="roomForm.readings.ventilation_power" :min="0" :max="100" suffix="%" fluid />
          </label>
        </div>

        <div class="demo-action-row">
          <Button
            icon="pi pi-plus"
            label="Create room"
            :disabled="!canCreateRoom"
            :loading="isCreatingRoom"
            @click="createRoom"
          />
        </div>
      </aside>

      <aside class="app-panel demo-control-panel">
        <div class="demo-section-heading">
          <div>
            <h2>Live stream</h2>
            <p>The simulator publishes to normal MQTT topics. Room profiles are picked up without restarting pods.</p>
          </div>
        </div>

        <div class="demo-live-grid">
          <div class="demo-live-cell">
            <span>Environment</span>
            <strong>{{ status?.environment?.name || "Not prepared" }}</strong>
          </div>
          <div class="demo-live-cell">
            <span>Last telemetry</span>
            <strong>{{ formatDateTime(status?.metrics.latest_telemetry_at) }}</strong>
          </div>
          <div class="demo-live-cell">
            <span>MQTT topic</span>
            <strong>sensor/{parameter}</strong>
          </div>
        </div>
      </aside>
    </section>

    <section class="app-panel demo-table-panel">
      <div class="demo-section-heading demo-section-heading--table">
        <div>
          <h2>Room profiles</h2>
          <p>Choose scenarios and optional overrides for each demo room.</p>
        </div>
      </div>

      <div v-if="isLoading" class="demo-loading">
        <i class="pi pi-spin pi-spinner" />
        <span>Loading demo status</span>
      </div>

      <div v-else-if="status?.rooms?.length" class="demo-room-list">
        <article v-for="room in status.rooms" :key="room.room_id" class="demo-room-row">
          <div class="demo-room-summary">
            <div class="room-cell">
              <span class="room-cell__code">ROOM-{{ room.room_id }}</span>
              <div class="room-title">
                <PlaceIcon :name="room.room_icon" size="sm" />
                <strong>{{ room.room_name }}</strong>
              </div>
              <small :title="roomSerialTitle(room)">{{ roomAssetSummary(room) }}</small>
            </div>

            <label class="demo-row-field demo-row-field--scenario">
              <span>Scenario</span>
              <Select
                v-model="drafts[room.room_id].scenario"
                :options="scenarioOptions"
                option-label="label"
                option-value="value"
                fluid
              />
            </label>

            <label class="demo-row-field">
              <span>Vent</span>
              <InputNumber
                v-model="drafts[room.room_id].ventilation_power_override"
                :min="0"
                :max="100"
                suffix="%"
                placeholder="Auto"
                fluid
              />
            </label>

            <label class="demo-row-field">
              <span>People</span>
              <InputNumber
                v-model="drafts[room.room_id].occupancy_override"
                :min="0"
                :max="100"
                placeholder="Auto"
                fluid
              />
            </label>

            <div class="demo-room-readings">
              <span>Current</span>
              <div class="current-pills">
                <b>{{ formatValue(room.co2, "ppm") }}</b>
                <b>{{ formatValue(room.temperature, "°C") }}</b>
                <b>{{ formatValue(room.humidity, "%") }}</b>
              </div>
            </div>

            <div class="demo-room-device">
              <span>Device</span>
              <strong>{{ formatValue(room.ventilation_power, "%") }}</strong>
              <small>{{ formatDateTime(room.last_activity_at) }}</small>
            </div>

            <div class="demo-row-actions">
              <Button
                icon="pi pi-save"
                label="Save"
                size="small"
                :loading="savingRoomId === room.room_id"
                @click="saveRoom(room.room_id)"
              />
              <Button
                icon="pi pi-times"
                aria-label="Clear profile"
                size="small"
                severity="secondary"
                outlined
                @click="clearProfile(room.room_id)"
              />
            </div>
          </div>

          <div class="demo-room-manage">
            <div class="demo-room-editor">
              <label class="demo-row-field">
                <span>Name</span>
                <InputText v-model="roomEdits[room.room_id].name" maxlength="40" fluid />
              </label>

              <label class="demo-row-field">
                <span>Type</span>
                <Select
                  v-model="roomEdits[room.room_id].icon"
                  :options="roomIconOptions"
                  option-label="label"
                  option-value="value"
                  fluid
                />
              </label>

              <Button
                icon="pi pi-check"
                label="Save room"
                size="small"
                severity="secondary"
                :disabled="!roomEdits[room.room_id].name?.trim()"
                :loading="renamingRoomId === room.room_id"
                @click="saveRoomMeta(room.room_id)"
              />
            </div>

            <div class="demo-room-tools">
              <Button
                icon="pi pi-microchip"
                label="+ Sensor"
                size="small"
                severity="secondary"
                outlined
                :loading="assetSavingKey === `${room.room_id}:sensor`"
                @click="addAssets(room.room_id, 1, 0)"
              />
              <Button
                icon="pi pi-sliders-h"
                label="+ Vent"
                size="small"
                severity="secondary"
                outlined
                :loading="assetSavingKey === `${room.room_id}:vent`"
                @click="addAssets(room.room_id, 0, 1)"
              />
            </div>

            <div class="demo-reading-editor">
              <label class="demo-row-field">
                <span>CO₂</span>
                <InputNumber v-model="readingDrafts[room.room_id].co2" :min="300" :max="5000" suffix=" ppm" fluid />
              </label>

              <label class="demo-row-field">
                <span>Temp</span>
                <InputNumber v-model="readingDrafts[room.room_id].temperature" :min="-50" :max="50" suffix=" °C" :max-fraction-digits="1" fluid />
              </label>

              <label class="demo-row-field">
                <span>Humidity</span>
                <InputNumber v-model="readingDrafts[room.room_id].humidity" :min="0" :max="100" suffix="%" fluid />
              </label>

              <label class="demo-row-field">
                <span>Vent</span>
                <InputNumber v-model="readingDrafts[room.room_id].ventilation_power" :min="0" :max="100" suffix="%" fluid />
              </label>

              <Button
                icon="pi pi-send"
                label="Apply values"
                size="small"
                :disabled="!hasReadingDraftValue(readingDrafts[room.room_id])"
                :loading="applyingReadingsRoomId === room.room_id"
                @click="applyReadings(room.room_id)"
              />
            </div>
          </div>
        </article>
      </div>

      <EmptyState
        v-else
        class="demo-empty empty-state--fill empty-state--centered"
        title="Demo topology is not ready"
        description="Prepare rooms, sensors, and devices before generating telemetry."
        icon="pi pi-sitemap"
        action-label="Prepare topology"
        action-icon="pi pi-sitemap"
        :disabled="isBootstrapping"
        @action="bootstrap"
      />
    </section>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ name: 'demo-data', layout: 'dashboard', requiresAuth: true });

import { computed, reactive, ref, watch } from "vue";
import { useConfirm } from "primevue/useconfirm";
import { useToast } from "primevue/usetoast";
import { useAuthStore } from "@/store/authStore";
import AppPageHeader from "@/components/common/AppPageHeader.vue";
import EmptyState from "@/components/common/EmptyState.vue";
import MetricCard from "@/components/common/MetricCard.vue";
import PlaceIcon from "@/components/common/PlaceIcon.vue";
import Button from "primevue/button";
import InputText from "primevue/inputtext";
import InputNumber from "primevue/inputnumber";
import Select from "primevue/select";
import Tag from "primevue/tag";
import {
  addDemoRoomAssets,
  applyDemoRoomReadings,
  backfillDemoData,
  bootstrapDemoData,
  clearDemoRoomProfile,
  createDemoRoom,
  getDemoDataStatus,
  resetDemoDataHistory,
  updateDemoRoom,
  updateDemoRoomProfile,
} from "@/services/apiService";
import { getPlaceIconOption, ROOM_ICON_OPTIONS } from "@/config/placeOptions";
import type { DemoDataStatus, DemoRoomProfilePayload, DemoRoomReadingsPayload, DemoRoomStatus } from "@/types/demoData";

type RoomDraft = Required<Pick<DemoRoomProfilePayload, "scenario">> & {
  ventilation_power_override: number | null;
  occupancy_override: number | null;
};

type RoomMetaDraft = {
  name: string;
  icon: string;
};

type ReadingDraft = {
  co2: number | null;
  temperature: number | null;
  humidity: number | null;
  ventilation_power: number | null;
};

const confirm = useConfirm();
const toast = useToast();
const authStore = useAuthStore();
const status = ref<DemoDataStatus | null>(null);
const isLoading = ref(false);
const isBootstrapping = ref(false);
const isBackfilling = ref(false);
const isResetting = ref(false);
const isCreatingRoom = ref(false);
const savingRoomId = ref<number | null>(null);
const renamingRoomId = ref<number | null>(null);
const applyingReadingsRoomId = ref<number | null>(null);
const assetSavingKey = ref<string | null>(null);
const drafts = reactive<Record<number, RoomDraft>>({});
const roomEdits = reactive<Record<number, RoomMetaDraft>>({});
const readingDrafts = reactive<Record<number, ReadingDraft>>({});
const backfillForm = reactive({
  hours: 6,
  interval_minutes: 5,
  scenario: null as string | null,
});
const roomForm = reactive({
  name: "",
  icon: "room",
  sensor_count: 1 as number | null,
  device_count: 1 as number | null,
  readings: {
    co2: 900,
    temperature: 22,
    humidity: 45,
    ventilation_power: 35,
  } as ReadingDraft,
});

const defaultScenarios = [
  "auto",
  "empty_room",
  "normal_usage",
  "crowded_room",
  "ventilation_failure",
  "night_mode",
  "critical_co2_event",
];

const scenarioLabels: Record<string, string> = {
  auto: "Auto rotation",
  empty_room: "Empty room",
  normal_usage: "Normal usage",
  crowded_room: "Crowded room",
  ventilation_failure: "Ventilation failure",
  night_mode: "Night mode",
  critical_co2_event: "Critical CO₂ event",
};

const scenarioOptions = computed(() => (status.value?.scenarios?.length ? status.value.scenarios : defaultScenarios)
  .map((value) => ({ value, label: scenarioLabels[value] || value })));

const backfillScenarioOptions = computed(() => [
  { value: null, label: "Use room profiles" },
  ...scenarioOptions.value.filter((item) => item.value !== "auto"),
]);
const roomIconOptions = ROOM_ICON_OPTIONS;

const canCreateRoom = computed(() => {
  const sensorCount = roomForm.sensor_count ?? 0;
  const deviceCount = roomForm.device_count ?? 0;
  return sensorCount >= 0 && deviceCount >= 0 && sensorCount + deviceCount > 0;
});

const statusLabel = computed(() => {
  if (!status.value?.environment) return "not prepared";
  if (!status.value.metrics.latest_telemetry_at) return "ready";
  return "streaming";
});

const statusSeverity = computed(() => {
  if (!status.value?.environment) return "warn";
  if (!status.value.metrics.latest_telemetry_at) return "secondary";
  return "success";
});

const activeProfiles = computed(() => (status.value?.rooms ?? [])
  .filter((room) => room.scenario !== "auto" || room.ventilation_power_override != null || room.occupancy_override != null)
  .length);

const latestTelemetryHint = computed(() => `Last ${formatDateTime(status.value?.metrics.latest_telemetry_at)}`);
const latestDeviceHint = computed(() => `Last ${formatDateTime(status.value?.metrics.latest_device_at)}`);

const syncDrafts = () => {
  for (const room of status.value?.rooms ?? []) {
    drafts[room.room_id] = {
      scenario: room.scenario || "auto",
      ventilation_power_override: room.ventilation_power_override ?? null,
      occupancy_override: room.occupancy_override ?? null,
    };
    roomEdits[room.room_id] = {
      name: room.room_name || "",
      icon: normalizeRoomIcon(room.room_icon),
    };
    readingDrafts[room.room_id] = {
      co2: roundForInput(room.co2, 0, 900),
      temperature: roundForInput(room.temperature, 1, 22),
      humidity: roundForInput(room.humidity, 0, 45),
      ventilation_power: roundForInput(room.ventilation_power, 0, 35),
    };
  }
};

const loadStatus = async () => {
  isLoading.value = true;
  try {
    status.value = await getDemoDataStatus();
    syncDrafts();
  } catch (error) {
    if (import.meta.client) {
      console.error("Failed to load demo data status:", error);
    }
  } finally {
    isLoading.value = false;
  }
};

await useAsyncData("demo-data-status", async () => {
  await loadStatus();
  return status.value;
});

watch(status, syncDrafts);

if (import.meta.client) {
  watch(
    () => authStore.token,
    async (token) => {
      if (!token) return;
      await loadStatus();
    },
    { immediate: true },
  );
}

const bootstrap = async () => {
  isBootstrapping.value = true;
  try {
    status.value = await bootstrapDemoData({ room_count: 4 });
    syncDrafts();
    toast.add({ severity: "success", summary: "Demo topology is ready", life: 2600 });
  } finally {
    isBootstrapping.value = false;
  }
};

const createRoom = async () => {
  isCreatingRoom.value = true;
  try {
    status.value = await createDemoRoom({
      name: roomForm.name.trim() || null,
      icon: roomForm.icon || "room",
      sensor_count: roomForm.sensor_count ?? 0,
      device_count: roomForm.device_count ?? 0,
      readings: buildReadingsPayload(roomForm.readings),
    });
    roomForm.name = "";
    roomForm.sensor_count = 1;
    roomForm.device_count = 1;
    syncDrafts();
    toast.add({ severity: "success", summary: "Demo room created", life: 2400 });
  } finally {
    isCreatingRoom.value = false;
  }
};

const backfill = async () => {
  isBackfilling.value = true;
  try {
    const result = await backfillDemoData({
      hours: backfillForm.hours,
      interval_minutes: backfillForm.interval_minutes,
      scenario: backfillForm.scenario,
    });
    await loadStatus();
    toast.add({
      severity: "success",
      summary: "History generated",
      detail: `${formatNumber(result.inserted_sensor_rows)} sensor rows, ${formatNumber(result.inserted_device_rows)} device rows`,
      life: 3600,
    });
  } finally {
    isBackfilling.value = false;
  }
};

const confirmReset = () => {
  confirm.require({
    header: "Clear demo history",
    message: "This removes demo sensor and device history, while keeping rooms, sensors, devices, and profiles.",
    icon: "pi pi-exclamation-triangle",
    acceptLabel: "Clear",
    rejectLabel: "Cancel",
    acceptClass: "p-button-danger",
    accept: resetHistory,
  });
};

const resetHistory = async () => {
  isResetting.value = true;
  try {
    const result = await resetDemoDataHistory();
    await loadStatus();
    toast.add({
      severity: "success",
      summary: "Demo history cleared",
      detail: `${formatNumber(result.deleted_sensor_rows)} sensor rows, ${formatNumber(result.deleted_device_rows)} device rows`,
      life: 3200,
    });
  } finally {
    isResetting.value = false;
  }
};

const saveRoom = async (roomId: number) => {
  savingRoomId.value = roomId;
  try {
    const draft = drafts[roomId];
    status.value = await updateDemoRoomProfile(roomId, {
      scenario: draft.scenario,
      ventilation_power_override: draft.ventilation_power_override,
      occupancy_override: draft.occupancy_override,
    });
    syncDrafts();
    toast.add({ severity: "success", summary: "Room profile saved", life: 2200 });
  } finally {
    savingRoomId.value = null;
  }
};

const clearProfile = async (roomId: number) => {
  savingRoomId.value = roomId;
  try {
    status.value = await clearDemoRoomProfile(roomId);
    syncDrafts();
    toast.add({ severity: "info", summary: "Room profile cleared", life: 2200 });
  } finally {
    savingRoomId.value = null;
  }
};

const saveRoomMeta = async (roomId: number) => {
  const draft = roomEdits[roomId];
  if (!draft?.name?.trim()) return;

  renamingRoomId.value = roomId;
  try {
    status.value = await updateDemoRoom(roomId, {
      name: draft.name.trim(),
      icon: draft.icon || "room",
    });
    syncDrafts();
    toast.add({ severity: "success", summary: "Room updated", life: 2200 });
  } finally {
    renamingRoomId.value = null;
  }
};

const addAssets = async (roomId: number, sensorCount: number, deviceCount: number) => {
  assetSavingKey.value = `${roomId}:${sensorCount > 0 ? "sensor" : "vent"}`;
  try {
    status.value = await addDemoRoomAssets(roomId, {
      sensor_count: sensorCount,
      device_count: deviceCount,
    });
    syncDrafts();
    toast.add({
      severity: "success",
      summary: sensorCount > 0 ? "Sensor added" : "Ventilation device added",
      life: 2200,
    });
  } finally {
    assetSavingKey.value = null;
  }
};

const applyReadings = async (roomId: number) => {
  const draft = readingDrafts[roomId];
  if (!hasReadingDraftValue(draft)) return;

  applyingReadingsRoomId.value = roomId;
  try {
    status.value = await applyDemoRoomReadings(roomId, buildReadingsPayload(draft));
    syncDrafts();
    toast.add({ severity: "success", summary: "Room values applied", life: 2200 });
  } finally {
    applyingReadingsRoomId.value = null;
  }
};

const formatNumber = (value: number) => new Intl.NumberFormat("en-US").format(value);

const formatValue = (value: number | null | undefined, unit: string) => (
  value == null ? "—" : `${Math.round(value * 10) / 10}${unit}`
);

function roomAssetSummary(room: DemoRoomStatus) {
  return `${room.sensor_count ?? 0} sensors, ${room.device_count ?? 0} vent devices`;
}

function roomSerialTitle(room: DemoRoomStatus) {
  const sensors = room.sensor_serial_numbers || room.sensor_serial_number || "no demo sensors";
  const devices = room.device_serial_numbers || room.device_serial_number || "no demo ventilation devices";
  return `Sensors: ${sensors}\nVentilation: ${devices}`;
}

function buildReadingsPayload(draft: ReadingDraft): DemoRoomReadingsPayload {
  return {
    co2: draft.co2,
    temperature: draft.temperature,
    humidity: draft.humidity,
    ventilation_power: draft.ventilation_power,
  };
}

function hasReadingDraftValue(draft?: ReadingDraft) {
  return Boolean(draft && Object.values(draft).some((value) => value != null));
}

function roundForInput(value: number | null | undefined, fractionDigits: number, fallback: number) {
  if (value == null || Number.isNaN(value)) return fallback;
  const scale = 10 ** fractionDigits;
  return Math.round(value * scale) / scale;
}

function normalizeRoomIcon(value?: string | null) {
  if (!value) return "room";
  return roomIconOptions.some((option) => option.value === value) ? value : "room";
}

function formatDateTime(value?: string | null) {
  if (!value) return "—";
  return new Intl.DateTimeFormat("en-US", {
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(value));
}
</script>

<style scoped>
.demo-page {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: var(--app-gap-md);
  min-height: 0;
  min-width: 0;
  width: 100%;
}

.demo-metrics {
  display: grid;
  gap: 8px;
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.demo-workbench {
  display: grid;
  gap: var(--app-gap-md);
  grid-template-columns: minmax(300px, 0.9fr) minmax(340px, 1.1fr) minmax(280px, 0.9fr);
}

.demo-control-panel,
.demo-table-panel {
  display: flex;
  flex-direction: column;
  gap: var(--app-gap-md);
  min-width: 0;
  padding: var(--app-panel-padding);
}

.demo-table-panel {
  flex: 1;
  min-height: 360px;
}

.demo-control-panel--builder {
  gap: 12px;
}

.demo-section-heading {
  align-items: flex-start;
  display: flex;
  gap: var(--app-gap-md);
  justify-content: space-between;
}

.demo-section-heading h2 {
  color: var(--app-text-strong);
  font-size: 1rem;
  font-weight: 780;
  line-height: 1.3;
  margin: 0;
}

.demo-section-heading p {
  color: var(--app-muted);
  font-size: 0.82rem;
  line-height: 1.25rem;
  margin: 4px 0 0;
}

.demo-form-grid {
  display: grid;
  gap: var(--app-gap-sm);
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.demo-form-grid--readings {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.demo-field {
  display: flex;
  flex-direction: column;
  gap: 5px;
  min-width: 0;
}

.demo-field--wide {
  grid-column: 1 / -1;
}

.demo-field span {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 700;
  line-height: 1rem;
  text-transform: uppercase;
}

.demo-subsection-label {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 800;
  letter-spacing: 0;
  line-height: 1rem;
  text-transform: uppercase;
}

.place-select-value,
.place-select-option {
  align-items: center;
  display: flex;
  gap: 8px;
  min-width: 0;
}

.place-select-value span,
.place-select-option span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.place-select-option span {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.place-select-option strong,
.place-select-option small {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.place-select-option small {
  color: var(--app-muted);
  font-size: 0.72rem;
}

.demo-action-row,
.demo-row-actions {
  align-items: center;
  display: flex;
  flex-wrap: wrap;
  gap: var(--app-gap-sm);
}

.demo-live-grid {
  display: grid;
  gap: 8px;
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.demo-live-cell {
  background: var(--app-surface-soft);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex-direction: column;
  gap: 5px;
  min-height: 70px;
  min-width: 0;
  padding: 10px;
}

.demo-live-cell span {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 700;
  text-transform: uppercase;
}

.demo-live-cell strong {
  color: var(--app-text-strong);
  font-size: 0.9rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.room-cell,
.current-stack {
  display: flex;
  flex-direction: column;
  gap: 3px;
  min-width: 0;
}

.room-cell__code {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 700;
}

.room-title {
  align-items: center;
  display: flex;
  gap: 8px;
  min-width: 0;
}

.room-title strong,
.room-cell strong {
  color: var(--app-text-strong);
  font-size: 0.9rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.room-cell small,
.current-stack small {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.current-stack span {
  color: var(--app-text-strong);
  font-size: 0.82rem;
  line-height: 1.1rem;
}

.demo-loading {
  align-items: center;
  border: 1px dashed var(--app-border);
  border-radius: var(--app-radius);
  color: var(--app-muted);
  display: flex;
  gap: var(--app-gap-sm);
  justify-content: center;
  min-height: 180px;
}

.demo-room-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.demo-room-row {
  background: var(--app-surface-soft);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-width: 0;
  padding: 12px;
}

.demo-room-summary {
  align-items: center;
  display: grid;
  gap: 10px;
  grid-template-columns:
    minmax(170px, 1.15fr)
    minmax(190px, 1.25fr)
    minmax(112px, 0.72fr)
    minmax(112px, 0.72fr)
    minmax(150px, 1fr)
    minmax(120px, 0.78fr)
    minmax(112px, auto);
  min-width: 0;
}

.demo-row-field,
.demo-room-readings,
.demo-room-device {
  display: flex;
  flex-direction: column;
  gap: 5px;
  min-width: 0;
}

.demo-row-field span,
.demo-room-readings > span,
.demo-room-device > span {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 700;
  line-height: 1rem;
  text-transform: uppercase;
}

.current-pills {
  display: flex;
  flex-wrap: wrap;
  gap: 5px;
}

.current-pills b {
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: 6px;
  color: var(--app-text-strong);
  font-size: 0.78rem;
  font-weight: 760;
  line-height: 1rem;
  padding: 5px 7px;
}

.demo-room-device strong {
  color: var(--app-text-strong);
  font-size: 0.95rem;
  line-height: 1.15rem;
}

.demo-room-device small {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.demo-room-manage {
  border-top: 1px solid var(--app-border);
  display: grid;
  gap: 10px;
  grid-template-columns: minmax(430px, 1fr) minmax(190px, auto);
  min-width: 0;
  padding-top: 10px;
}

.demo-room-editor {
  align-items: end;
  display: grid;
  gap: 8px;
  grid-template-columns: minmax(160px, 1fr) minmax(140px, 0.75fr) minmax(116px, auto);
  min-width: 0;
}

.demo-room-editor :deep(.p-button),
.demo-room-tools :deep(.p-button),
.demo-reading-editor :deep(.p-button) {
  white-space: nowrap;
}

.demo-room-editor :deep(.p-button-label),
.demo-room-tools :deep(.p-button-label),
.demo-reading-editor :deep(.p-button-label) {
  white-space: nowrap;
}

.demo-room-tools {
  align-items: end;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  min-width: 0;
}

.demo-reading-editor {
  align-items: end;
  display: grid;
  gap: 8px;
  grid-column: 1 / -1;
  grid-template-columns: repeat(5, minmax(105px, 1fr)) minmax(128px, auto);
  min-width: 0;
}

@media (max-width: 1180px) {
  .demo-metrics {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .demo-workbench {
    grid-template-columns: 1fr;
  }

  .demo-room-summary {
    align-items: end;
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .demo-room-manage {
    align-items: end;
    grid-template-columns: 1fr;
  }

  .demo-room-editor,
  .demo-reading-editor {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .demo-row-actions {
    align-self: end;
  }
}

@media (max-width: 720px) {
  .demo-metrics,
  .demo-live-grid,
  .demo-form-grid--readings,
  .demo-room-summary,
  .demo-room-manage,
  .demo-room-editor,
  .demo-reading-editor {
    grid-template-columns: 1fr;
  }

  .demo-row-actions {
    flex-direction: column;
    align-items: stretch;
  }

  .demo-room-row {
    grid-template-columns: 1fr;
  }
}
</style>
