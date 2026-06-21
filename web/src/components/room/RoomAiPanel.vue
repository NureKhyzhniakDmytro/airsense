<template>
  <section class="ai-panel" aria-label="AI predictions">
    <header class="ai-panel__header">
      <div>
        <span class="ai-panel__eyebrow">AI forecast</span>
        <h3>Predictive ventilation</h3>
      </div>
      <div class="ai-panel__actions">
        <Button
          type="button"
          aria-label="Refresh AI predictions"
          icon="pi pi-refresh"
          severity="secondary"
          outlined
          size="small"
          :loading="isLoading"
          @click="loadInsights"
        />
      </div>
    </header>

    <Message v-if="errorMessage" severity="error" size="small">
      {{ errorMessage }}
    </Message>

    <div v-if="isLoading && !insights" class="ai-panel__loading">
      Loading AI predictions...
    </div>

    <div v-else-if="insights && !insights.has_sample" class="ai-panel__empty">
      <i class="pi pi-chart-line" aria-hidden="true"></i>
      <div>
        <strong>No prediction sample</strong>
        <span>{{ insights.message || "CO2, temperature, and humidity telemetry are required." }}</span>
      </div>
    </div>

    <template v-else-if="insights && sample">
      <div class="ai-panel__summary">
        <div class="ai-sample">
          <span>CO₂</span>
          <strong>{{ formatNumber(sample.co2) }} ppm</strong>
        </div>
        <div class="ai-sample">
          <span>Temperature</span>
          <strong>{{ formatNumber(sample.temperature) }}°C</strong>
        </div>
        <div class="ai-sample">
          <span>Humidity</span>
          <strong>{{ formatNumber(sample.humidity) }}%</strong>
        </div>
        <div class="ai-sample">
          <span>Ventilation</span>
          <strong>{{ formatNumber(sample.ventilation_power) }}%</strong>
        </div>
        <div v-if="sample.supply_ventilation_power !== null && sample.supply_ventilation_power !== undefined" class="ai-sample">
          <span>Supply</span>
          <strong>{{ formatNumber(sample.supply_ventilation_power) }}%</strong>
        </div>
        <div v-if="sample.exhaust_ventilation_power !== null && sample.exhaust_ventilation_power !== undefined" class="ai-sample">
          <span>Exhaust</span>
          <strong>{{ formatNumber(sample.exhaust_ventilation_power) }}%</strong>
        </div>
      </div>

      <section class="ai-control">
        <header>
          <div>
            <span class="ai-panel__eyebrow">AI control</span>
            <h4>Autonomous ventilation</h4>
          </div>
          <label class="ai-control__toggle">
            <Checkbox
              v-model="controlDraft.enabled"
              :input-id="`ai-control-${props.roomId}`"
              binary
              :disabled="isReadOnly"
            />
            <span>AI adjusts fan speed</span>
          </label>
        </header>

        <div class="ai-control__grid">
          <label class="ai-control__field">
            <span>CO₂ target</span>
            <InputNumber v-model="controlDraft.target_co2" :min="400" :max="3000" suffix=" ppm" :disabled="isReadOnly" fluid />
          </label>
          <label class="ai-control__field">
            <span>Temp target</span>
            <InputNumber v-model="controlDraft.target_temperature" :min="10" :max="40" suffix=" °C" :max-fraction-digits="1" :disabled="isReadOnly" placeholder="Any" fluid />
          </label>
          <label class="ai-control__field">
            <span>Humidity target</span>
            <InputNumber v-model="controlDraft.target_humidity" :min="10" :max="90" suffix="%" :disabled="isReadOnly" placeholder="Any" fluid />
          </label>
          <label class="ai-control__field">
            <span>Max fan</span>
            <InputNumber v-model="controlDraft.max_ventilation_power" :min="0" :max="100" suffix="%" :disabled="isReadOnly" fluid />
          </label>
          <Button
            v-if="!isReadOnly"
            type="button"
            label="Save AI control"
            icon="pi pi-save"
            size="small"
            :loading="isSavingControl"
            @click="saveControlSettings"
          />
        </div>
      </section>

      <section v-if="forecastRows.length" class="ai-panel__table-section">
        <header>
          <span>Forecast</span>
        </header>
        <table class="ai-table">
          <thead>
            <tr>
              <th>Time</th>
              <th>CO₂</th>
              <th>Temperature</th>
              <th>Humidity</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="point in forecastRows" :key="point.horizon_minutes">
              <td>In {{ point.horizon_minutes }} min</td>
              <td>{{ formatNumber(point.co2) }} ppm</td>
              <td>{{ formatNumber(point.temperature) }}°C</td>
              <td>{{ formatNumber(point.humidity) }}%</td>
            </tr>
          </tbody>
        </table>
      </section>

      <section class="ai-recommendation">
        <header>
          <div>
            <span class="ai-panel__eyebrow">Recommendation</span>
            <h4>{{ latestRecommendation ? `${formatNumber(latestRecommendation.requested_power)}% ventilation` : "No recommendation yet" }}</h4>
          </div>
          <span v-if="latestRecommendation" class="ai-status" :class="`ai-status--${latestRecommendation.status}`">
            {{ formatRecommendationStatus(latestRecommendation.status) }}
          </span>
        </header>

        <p v-if="latestRecommendation?.reason">{{ latestRecommendation.reason }}</p>
        <p v-else>Generate a recommendation from the current room conditions.</p>

        <div v-if="!isReadOnly" class="ai-recommendation__actions">
          <Button
            type="button"
            label="Generate"
            icon="pi pi-sparkles"
            size="small"
            :loading="isCreatingRecommendation"
            @click="createRecommendation"
          />
          <Button
            v-if="latestRecommendation && latestRecommendation.status === 'recommended'"
            type="button"
            label="Apply"
            icon="pi pi-check"
            severity="secondary"
            outlined
            size="small"
            :loading="isAcceptingRecommendation"
            @click="acceptRecommendation(latestRecommendation.id)"
          />
        </div>
      </section>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, inject, onMounted, ref, type ComputedRef } from "vue";
import Button from "primevue/button";
import Checkbox from "primevue/checkbox";
import InputNumber from "primevue/inputnumber";
import Message from "primevue/message";
import { useToast } from "primevue/usetoast";
import {
  acceptRoomAiRecommendation,
  createRoomAiRecommendation,
  getRoomAiInsights,
  updateRoomAiControlSettings,
} from "@/services/apiService";
import type { AiControlSettings, AiControlSettingsPayload, AiRecommendationAudit, RoomAiInsights } from "@/types/ai";

const props = defineProps<{
  roomId: number;
}>();

const emit = defineEmits<{
  (event: "control-updated", settings: AiControlSettings): void;
}>();

const toast = useToast();
const insights = ref<RoomAiInsights | null>(null);
const isLoading = ref(false);
const isCreatingRecommendation = ref(false);
const isAcceptingRecommendation = ref(false);
const isSavingControl = ref(false);
const errorMessage = ref("");
const controlDraft = ref<AiControlSettingsPayload>({
  enabled: false,
  target_co2: 900,
  target_temperature: 22,
  target_humidity: 45,
  max_ventilation_power: 100,
});
const injectedReadOnly = inject<ComputedRef<boolean>>("roomReadOnly", computed(() => false));
const isReadOnly = computed(() => injectedReadOnly.value);

const sample = computed(() => insights.value?.sample ?? null);
const latestRecommendation = computed(() => insights.value?.recent_recommendations?.[0] ?? null);
const forecastRows = computed(() =>
  [...(insights.value?.prediction?.predictions ?? [])].sort((left, right) => left.horizon_minutes - right.horizon_minutes),
);

const upsertRecommendation = (recommendation: AiRecommendationAudit) => {
  if (!insights.value) return;
  const existing = insights.value.recent_recommendations.filter((item) => item.id !== recommendation.id);
  insights.value.recent_recommendations = [recommendation, ...existing].slice(0, 5);
};

const syncControlDraft = (settings?: AiControlSettings | null) => {
  controlDraft.value = {
    enabled: settings?.enabled ?? false,
    target_co2: settings?.target_co2 ?? 900,
    target_temperature: settings?.target_temperature ?? 22,
    target_humidity: settings?.target_humidity ?? 45,
    max_ventilation_power: settings?.max_ventilation_power ?? 100,
  };
};

const loadInsights = async () => {
  isLoading.value = true;
  errorMessage.value = "";
  try {
    insights.value = await getRoomAiInsights(props.roomId);
    syncControlDraft(insights.value.control_settings);
    if (insights.value.control_settings)
      emit("control-updated", insights.value.control_settings);
  } catch (error) {
    console.error("Failed to load AI predictions:", error);
    errorMessage.value = "AI predictions are unavailable right now.";
  } finally {
    isLoading.value = false;
  }
};

const saveControlSettings = async () => {
  if (isReadOnly.value) return;

  isSavingControl.value = true;
  errorMessage.value = "";
  try {
    const settings = await updateRoomAiControlSettings(props.roomId, {
      ...controlDraft.value,
      target_co2: controlDraft.value.target_co2 ?? 900,
      max_ventilation_power: controlDraft.value.max_ventilation_power ?? 100,
    });
    if (insights.value)
      insights.value.control_settings = settings;
    syncControlDraft(settings);
    emit("control-updated", settings);
    toast.add({ severity: "success", summary: "AI control saved", life: 2400 });
  } catch (error) {
    console.error("Failed to save AI control settings:", error);
    errorMessage.value = "Unable to save AI control settings.";
  } finally {
    isSavingControl.value = false;
  }
};

const createRecommendation = async () => {
  if (isReadOnly.value) return;

  isCreatingRecommendation.value = true;
  errorMessage.value = "";
  try {
    const recommendation = await createRoomAiRecommendation(props.roomId);
    upsertRecommendation(recommendation);
    toast.add({ severity: "success", summary: "Recommendation generated", life: 2500 });
  } catch (error) {
    console.error("Failed to create AI recommendation:", error);
    errorMessage.value = "Unable to generate an AI recommendation from the current sample.";
  } finally {
    isCreatingRecommendation.value = false;
  }
};

const acceptRecommendation = async (recommendationId: number) => {
  if (isReadOnly.value) return;

  isAcceptingRecommendation.value = true;
  errorMessage.value = "";
  try {
    const recommendation = await acceptRoomAiRecommendation(props.roomId, recommendationId);
    upsertRecommendation(recommendation);
    toast.add({ severity: "success", summary: "Recommendation applied", detail: "Command was sent to the room ventilation device.", life: 3000 });
  } catch (error) {
    console.error("Failed to apply AI recommendation:", error);
    errorMessage.value = "Unable to apply this recommendation.";
  } finally {
    isAcceptingRecommendation.value = false;
  }
};

const formatNumber = (value?: number | null) => {
  if (value == null || Number.isNaN(value)) return "—";
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 }).format(value);
};

const formatRecommendationStatus = (status: string) => {
  if (status === "recommended") return "Ready";
  if (status === "accepted") return "Applied";
  if (status === "used") return "Used";
  return status;
};

onMounted(loadInsights);
</script>

<style scoped>
.ai-panel {
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding: 14px;
}

.ai-panel__header,
.ai-recommendation header,
.ai-panel__table-section header {
  align-items: center;
  display: flex;
  gap: 12px;
  justify-content: space-between;
  min-width: 0;
}

.ai-panel__header h3,
.ai-recommendation h4 {
  color: var(--app-text-strong);
  font-size: 1rem;
  line-height: 1.2;
  margin: 0;
}

.ai-panel__eyebrow,
.ai-panel__table-section small,
.ai-sample span {
  color: var(--app-muted);
  font-size: 0.72rem;
  font-weight: 760;
  letter-spacing: 0;
  text-transform: uppercase;
}

.ai-panel__actions,
.ai-recommendation__actions {
  align-items: center;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  justify-content: flex-end;
}

.ai-panel__loading,
.ai-panel__empty {
  align-items: center;
  color: var(--app-muted);
  display: flex;
  gap: 12px;
  min-height: 96px;
}

.ai-panel__empty i {
  align-items: center;
  background: color-mix(in srgb, var(--app-primary) 10%, var(--app-surface-soft));
  border: 1px solid color-mix(in srgb, var(--app-primary) 28%, var(--app-border));
  border-radius: 6px;
  color: var(--app-primary);
  display: inline-flex;
  height: 38px;
  justify-content: center;
  width: 38px;
}

.ai-panel__empty div {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.ai-panel__empty strong {
  color: var(--app-text-strong);
}

.ai-panel__summary {
  display: grid;
  gap: 8px;
  grid-template-columns: repeat(auto-fit, minmax(132px, 1fr));
}

.ai-sample {
  background: var(--app-surface-soft);
  border: 1px solid var(--app-border);
  border-radius: 6px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
  padding: 10px;
}

.ai-sample strong {
  color: var(--app-text-strong);
  font-size: 1rem;
  line-height: 1.2;
}

.ai-panel__table-section,
.ai-recommendation,
.ai-control {
  background: var(--app-surface-raised);
  border: 1px solid var(--app-border);
  border-radius: 6px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-width: 0;
  padding: 12px;
}

.ai-control header {
  align-items: center;
  display: flex;
  gap: 12px;
  justify-content: space-between;
  min-width: 0;
}

.ai-control h4 {
  color: var(--app-text-strong);
  font-size: 0.95rem;
  line-height: 1.2;
  margin: 0;
}

.ai-control__toggle {
  align-items: center;
  color: var(--app-text);
  display: inline-flex;
  flex: 0 0 auto;
  gap: 8px;
  font-size: 0.84rem;
  font-weight: 680;
  min-height: 28px;
}

.ai-control__grid {
  align-items: end;
  display: grid;
  gap: 8px;
  grid-template-columns: repeat(4, minmax(130px, 1fr)) minmax(130px, auto);
}

.ai-control__field {
  display: flex;
  flex-direction: column;
  gap: 5px;
  min-width: 0;
}

.ai-control__field span {
  color: var(--app-muted);
  font-size: 0.68rem;
  font-weight: 760;
  text-transform: uppercase;
}

.ai-panel__table-section header span {
  color: var(--app-text-strong);
  font-weight: 760;
}

.ai-table {
  border-collapse: collapse;
  font-size: 0.84rem;
  width: 100%;
}

.ai-table th,
.ai-table td {
  border-bottom: 1px solid var(--app-border);
  color: var(--app-text);
  padding: 7px 6px;
  text-align: left;
  white-space: nowrap;
}

.ai-table th {
  color: var(--app-muted);
  font-size: 0.72rem;
  font-weight: 760;
  text-transform: uppercase;
}

.ai-table tr:last-child td {
  border-bottom: 0;
}

.ai-recommendation p {
  color: var(--app-muted);
  line-height: 1.45;
  margin: 0;
}

.ai-status {
  border: 1px solid var(--app-border);
  border-radius: 999px;
  color: var(--app-muted);
  font-size: 0.72rem;
  font-weight: 760;
  padding: 4px 8px;
  text-transform: uppercase;
}

.ai-status--accepted {
  background: color-mix(in srgb, var(--app-primary) 12%, var(--app-surface));
  border-color: color-mix(in srgb, var(--app-primary) 34%, var(--app-border));
  color: var(--app-primary);
}

.ai-status--used {
  background: color-mix(in srgb, var(--app-success) 12%, var(--app-surface));
  border-color: color-mix(in srgb, var(--app-success) 34%, var(--app-border));
  color: var(--app-success);
}

@media (max-width: 980px) {
  .ai-panel__summary {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .ai-control__grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 620px) {
  .ai-panel__header,
  .ai-recommendation header,
  .ai-control header {
    align-items: flex-start;
    flex-direction: column;
  }

  .ai-panel__actions,
  .ai-recommendation__actions {
    justify-content: flex-start;
    width: 100%;
  }

  .ai-panel__summary {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .ai-table {
    font-size: 0.78rem;
  }
}
</style>
