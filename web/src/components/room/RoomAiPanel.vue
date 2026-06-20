<template>
  <section class="ai-panel" aria-label="AI predictions">
    <header class="ai-panel__header">
      <div>
        <span class="ai-panel__eyebrow">AI forecast</span>
        <h3>Predictive ventilation</h3>
      </div>
      <div class="ai-panel__actions">
        <span v-if="modelLabel" class="ai-panel__model">{{ modelLabel }}</span>
        <Button
          type="button"
          label="Refresh"
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
        <div class="ai-sample">
          <span>Sample age</span>
          <strong>{{ formatAge(insights.telemetry_age_seconds) }}</strong>
        </div>
      </div>

      <div class="ai-panel__grid">
        <section class="ai-panel__table-section">
          <header>
            <span>Forecast</span>
            <small>{{ insights.prediction?.mode || "unknown" }}</small>
          </header>
          <table class="ai-table">
            <thead>
              <tr>
                <th>Horizon</th>
                <th>CO₂</th>
                <th>Temp</th>
                <th>Humidity</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="point in insights.prediction?.predictions || []" :key="point.horizon_minutes">
                <td>{{ point.horizon_minutes }}m</td>
                <td>{{ formatNumber(point.co2) }}</td>
                <td>{{ formatNumber(point.temperature) }}</td>
                <td>{{ formatNumber(point.humidity) }}</td>
              </tr>
            </tbody>
          </table>
        </section>

        <section class="ai-panel__table-section">
          <header>
            <span>Scenarios</span>
            <small>20m CO₂</small>
          </header>
          <table class="ai-table">
            <thead>
              <tr>
                <th>Mode</th>
                <th>Power</th>
                <th>CO₂</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="scenario in scenarioRows" :key="scenario.label">
                <td>{{ scenario.label }}</td>
                <td>{{ formatNumber(scenario.ventilation_power) }}%</td>
                <td>{{ formatNumber(scenario.co2) }}</td>
              </tr>
            </tbody>
          </table>
        </section>
      </div>

      <section class="ai-recommendation">
        <header>
          <div>
            <span class="ai-panel__eyebrow">Recommendation</span>
            <h4>{{ latestRecommendation ? `${formatNumber(latestRecommendation.requested_power)}% ventilation` : "No recommendation yet" }}</h4>
          </div>
          <span v-if="latestRecommendation" class="ai-status" :class="`ai-status--${latestRecommendation.status}`">
            {{ latestRecommendation.status }}
          </span>
        </header>

        <p v-if="latestRecommendation?.reason">{{ latestRecommendation.reason }}</p>
        <p v-else>Generate a recommendation from the current room sample and model version.</p>

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
import Message from "primevue/message";
import { useToast } from "primevue/usetoast";
import {
  acceptRoomAiRecommendation,
  createRoomAiRecommendation,
  getRoomAiInsights,
} from "@/services/apiService";
import type { AiRecommendationAudit, RoomAiInsights } from "@/types/ai";

const props = defineProps<{
  roomId: number;
}>();

const toast = useToast();
const insights = ref<RoomAiInsights | null>(null);
const isLoading = ref(false);
const isCreatingRecommendation = ref(false);
const isAcceptingRecommendation = ref(false);
const errorMessage = ref("");
const injectedReadOnly = inject<ComputedRef<boolean>>("roomReadOnly", computed(() => false));
const isReadOnly = computed(() => injectedReadOnly.value);

const sample = computed(() => insights.value?.sample ?? null);
const latestRecommendation = computed(() => insights.value?.recent_recommendations?.[0] ?? null);
const modelLabel = computed(() => {
  const prediction = insights.value?.prediction;
  return prediction ? `${prediction.mode} · ${prediction.model_version}` : "";
});

const scenarioRows = computed(() =>
  (insights.value?.simulation?.scenarios ?? []).map((scenario) => ({
    label: scenario.label,
    ventilation_power: scenario.ventilation_power,
    co2: scenario.predictions.find((point) => point.horizon_minutes === 20)?.co2
      ?? scenario.predictions.at(-1)?.co2
      ?? null,
  })),
);

const upsertRecommendation = (recommendation: AiRecommendationAudit) => {
  if (!insights.value) return;
  const existing = insights.value.recent_recommendations.filter((item) => item.id !== recommendation.id);
  insights.value.recent_recommendations = [recommendation, ...existing].slice(0, 5);
};

const loadInsights = async () => {
  isLoading.value = true;
  errorMessage.value = "";
  try {
    insights.value = await getRoomAiInsights(props.roomId);
  } catch (error) {
    console.error("Failed to load AI predictions:", error);
    errorMessage.value = "AI predictions are unavailable right now.";
  } finally {
    isLoading.value = false;
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

const formatAge = (seconds?: number | null) => {
  if (seconds == null) return "—";
  if (seconds < 60) return `${Math.round(seconds)}s`;
  if (seconds < 3600) return `${Math.round(seconds / 60)}m`;
  return `${Math.round(seconds / 3600)}h`;
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

.ai-panel__model {
  color: var(--app-muted);
  font-size: 0.8rem;
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
  grid-template-columns: repeat(6, minmax(0, 1fr));
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

.ai-panel__grid {
  display: grid;
  gap: 12px;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
}

.ai-panel__table-section,
.ai-recommendation {
  background: var(--app-surface-raised);
  border: 1px solid var(--app-border);
  border-radius: 6px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-width: 0;
  padding: 12px;
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

  .ai-panel__grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 620px) {
  .ai-panel__header,
  .ai-recommendation header {
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
