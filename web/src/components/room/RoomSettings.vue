<template>
  <div class="section-page">
    <section class="automation-panel">
      <header class="automation-panel__header">
        <div class="automation-panel__title">
          <span class="automation-panel__icon" aria-hidden="true">
            <i class="pi pi-sliders-h" />
          </span>
          <div class="automation-panel__copy">
            <span class="automation-panel__eyebrow">Automation</span>
            <h2>Room automation</h2>
            <p>{{ automationDescription }}</p>
          </div>
        </div>

        <div class="automation-panel__save">
          <Tag
            :severity="automationStatusSeverity"
            :value="automationStatusLabel"
            rounded
          />
          <Button
            v-if="isManualModeSelected && !aiControlEnabled"
            icon="pi pi-save"
            @click="saveChanges"
            severity="primary"
            label="Save curve"
            :disabled="isCurveEditingDisabled || !hasChanges"
          />
        </div>
      </header>

      <Skeleton v-if="isLoading" class="automation-skeleton" height="31rem" />

      <template v-else>
        <div class="automation-panel__body">
          <section
            class="automation-mode"
            aria-label="Automation mode"
          >
            <button
              type="button"
              class="automation-mode__option"
              :class="{ 'automation-mode__option--active': isAiModeSelected }"
              :aria-pressed="isAiModeSelected"
              @click="selectAutomationMode('ai')"
            >
              <span class="automation-mode__icon" aria-hidden="true">
                <i class="pi pi-bolt" />
              </span>
              <div class="automation-mode__copy">
                <span>AI autopilot</span>
                <strong>{{ aiModeLabel }}</strong>
              </div>
            </button>

            <button
              type="button"
              class="automation-mode__option"
              :class="{ 'automation-mode__option--active': isManualModeSelected }"
              :aria-pressed="isManualModeSelected"
              @click="selectAutomationMode('manual')"
            >
              <span class="automation-mode__icon" aria-hidden="true">
                <i class="pi pi-sliders-h" />
              </span>
              <div class="automation-mode__copy">
                <span>Manual curve</span>
                <strong>{{ manualModeLabel }}</strong>
              </div>
            </button>
          </section>

          <section class="critical-card" aria-label="Critical thresholds">
            <div class="critical-card__copy">
              <span>Critical threshold</span>
              <strong>{{ selectedCriticalParam.label || 'Parameter' }}</strong>
            </div>

            <div class="critical-card__controls">
              <Select
                v-model="selectedCriticalParam"
                inputId="critical-parameter"
                :options="parametersOptions"
                optionLabel="label"
                class="critical-card__select"
              />
              <div class="critical-card__value">
                <span>Current</span>
                <strong>{{ criticalValueLabel }}</strong>
              </div>
              <Button
                icon="pi pi-exclamation-triangle"
                :label="criticalButtonLabel"
                severity="secondary"
                variant="outlined"
                :disabled="isCriticalEditingDisabled"
                :loading="isSavingCritical"
                @click="openCriticalValueDialog"
              />
            </div>
          </section>

          <RoomAiPanel
            v-show="isAiModeSelected"
            class="automation-ai-panel"
            :room-id="roomId"
            @control-updated="handleAiControlUpdated"
          />

          <section
            v-show="isManualModeSelected"
            class="curve-card"
            :class="{ 'curve-card--standby': aiControlEnabled }"
            aria-label="Curve automation"
          >
            <div class="automation-controls">
              <div class="automation-controls__field">
                <label for="automation-parameter">Parameter</label>
                <Select
                  v-model="selectedParam"
                  inputId="automation-parameter"
                  :options="parametersOptions"
                  optionLabel="label"
                  class="automation-controls__select"
                />
              </div>

              <div class="automation-controls__actions" aria-label="Curve actions">
                <Tag
                  v-if="aiControlEnabled"
                  value="AI control active"
                  severity="info"
                  rounded
                />
                <Button
                  icon="pi pi-plus"
                  label="Point"
                  @click="addPoint"
                  severity="secondary"
                  variant="outlined"
                  :disabled="isCurveEditingDisabled"
                />
                <Button
                  icon="pi pi-trash"
                  @click="deleteSelectedPoint"
                  severity="secondary"
                  variant="outlined"
                  :disabled="isCurveEditingDisabled || selectedPointIndex === null"
                  aria-label="Delete selected point"
                  v-tooltip.top="'Delete selected point'"
                />
              </div>
            </div>

            <div class="automation-workspace">
              <section class="chart-surface">
                <div class="chart-surface__header">
                  <div>
                    <span>{{ aiControlEnabled ? 'Manual curve paused' : 'Curve' }}</span>
                    <strong>{{ selectedParam.label || 'Parameter' }}</strong>
                  </div>
                  <Tag :value="`${pointCount} points`" severity="secondary" rounded />
                </div>

                <div class="chart-surface__body">
                  <ClientOnly>
                    <component
                      :is="ApexCharts"
                      type="line"
                      height="100%"
                      :options="chartOptions"
                      :series="series"
                      ref="chart"
                    />
                    <template #fallback>
                      <Skeleton height="430px" />
                    </template>
                  </ClientOnly>
                </div>
              </section>

              <aside class="curve-summary" aria-label="Curve summary">
                <div class="curve-summary__item">
                  <span>Active source</span>
                  <strong>{{ activeSourceLabel }}</strong>
                </div>
                <div class="curve-summary__item">
                  <span>Input range</span>
                  <strong>{{ inputRangeLabel }}</strong>
                </div>
                <div class="curve-summary__item">
                  <span>Curve points</span>
                  <strong>{{ pointCount }}</strong>
                </div>
              </aside>
            </div>
          </section>
        </div>
      </template>
    </section>

    <Dialog v-model:visible="showCriticalValueDialog" modal header="Set critical value" :style="{ width: 'min(26rem, calc(100vw - 2rem))' }">
      <div class="entity-dialog-form">
        <div class="entity-dialog-field">
          <label class="entity-dialog-label" for="critical-value-input">Critical value</label>
          <InputNumber 
            inputId="critical-value-input"
            v-model="tempCriticalValue" 
            :min="criticalInputMin"
            :max="criticalInputMax"
            :step="0.1"
            class="w-full"
          />
        </div>
      </div>
      <template #footer>
        <div class="entity-dialog-actions">
          <Button label="Cancel" @click="showCriticalValueDialog = false" text />
          <Button
            label="Save"
            @click="saveCriticalValue"
            severity="primary"
            :loading="isSavingCritical"
            :disabled="isCriticalEditingDisabled"
          />
        </div>
      </template>
    </Dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch, computed, defineAsyncComponent, inject, type ComputedRef } from "vue";
import { useRoute } from "vue-router";
import { 
  getRoomCurve, 
  updateRoomCurve, 
  getAvailableParameters,
} from "@/services/apiService";
import { PARAMETER_LABELS, type CurveData, type ExtendedParam } from "@/types/sensor";
import type { AiControlSettings } from "@/types/ai";
import type { 
  ChartEvent, 
  ChartContext,
} from "@/types/chart";
import RoomAiPanel from "@/components/room/RoomAiPanel.vue";
import Select from 'primevue/select';
import Button from 'primevue/button';
import { useToast } from "primevue/usetoast";
import Dialog from 'primevue/dialog';
import InputNumber from 'primevue/inputnumber';
import Skeleton from 'primevue/skeleton';
import Tag from 'primevue/tag';
import { chartPalette, useChartConfig } from "@/config/chartConfig";

const ApexCharts = defineAsyncComponent(() => import("vue3-apexcharts"));

const route = useRoute();
const roomId = Number(route.params.roomId);
const toast = useToast();
const chart = ref();
const isLoading = ref(true);
const hasChanges = ref(false);
const isSavingCritical = ref(false);
const selectedPointIndex = ref<number | null>(null);
const showCriticalValueDialog = ref(false);
const tempCriticalValue = ref<number | null>(null);
const injectedReadOnly = inject<ComputedRef<boolean>>("roomReadOnly", computed(() => false));
const isReadOnly = computed(() => injectedReadOnly.value);

const parametersOptions = ref<ExtendedParam[]>([]);
const selectedParam = ref<ExtendedParam>({ name: "", label: "", unit: "" });
const selectedCriticalParam = ref<ExtendedParam>({ name: "", label: "", unit: "" });
const aiControlSettings = ref<AiControlSettings | null>(null);

const { series } = useChartConfig();

type AutomationMode = "ai" | "manual";
type CurvePoint = { x: number; y: number };

const selectedAutomationMode = ref<AutomationMode>("manual");
const savedCurvePoints = ref<CurvePoint[]>([]);

const asNumber = (value: number | null | undefined, fallback: number) => (
  Number.isFinite(value) ? Number(value) : fallback
);

const getParameterMin = (parameter: ExtendedParam) => asNumber(parameter.min_value, 0);
const getParameterMax = (parameter: ExtendedParam) => {
  const min = getParameterMin(parameter);
  const max = asNumber(parameter.max_value, 100);
  return max > min ? max : min + 1;
};

const inputMin = computed(() => getParameterMin(selectedParam.value));
const inputMax = computed(() => getParameterMax(selectedParam.value));
const criticalInputMin = computed(() => getParameterMin(selectedCriticalParam.value));
const criticalInputMax = computed(() => getParameterMax(selectedCriticalParam.value));

const clamp = (value: number, min: number, max: number) => (
  Math.max(min, Math.min(max, value))
);

const formatAxisValue = (value: number) => (
  Number.isInteger(value) ? String(value) : value.toFixed(1)
);

const normalizeCurvePoints = (rawPoints: CurvePoint[]): CurvePoint[] => {
  const min = inputMin.value;
  const max = inputMax.value;
  const normalized = rawPoints
    .map((point) => ({
      x: clamp(Number(point.x), min, max),
      y: clamp(Number(point.y), 0, 100),
    }))
    .filter((point) => Number.isFinite(point.x) && Number.isFinite(point.y))
    .sort((a, b) => a.x - b.x);

  if (!normalized.length) {
    return [
      { x: min, y: 0 },
      { x: max, y: 100 },
    ];
  }

  const byX = new Map<number, CurvePoint>();
  for (const point of normalized) {
    byX.set(Number(point.x.toFixed(4)), point);
  }

  const points = [...byX.values()].sort((a, b) => a.x - b.x);
  const first = points[0];
  const last = points[points.length - 1];

  if (first.x > min) {
    points.unshift({ x: min, y: 0 });
  } else {
    first.x = min;
    first.y = 0;
  }

  if (last.x < max) {
    points.push({ x: max, y: 100 });
  } else {
    points[points.length - 1].x = max;
    points[points.length - 1].y = 100;
  }

  if (points.length === 1) {
    points[0].y = 0;
    points.push({ x: max, y: 100 });
  }

  return points;
};

const commitCurvePoints = (points: CurvePoint[]) => {
  series.value[0].data = normalizeCurvePoints(points);
  series.value = [...series.value];
};

const getCurvePayloadPoints = (points: CurvePoint[] = savedCurvePoints.value) => (
  normalizeCurvePoints(points).map(point => ({
    value: Number(point.x.toFixed(3)),
    fan_speed: Math.round(clamp(point.y, 0, 100))
  }))
);

const getParameterCurvePayloadPoints = (
  parameter: ExtendedParam,
  points: CurveData["points"] | undefined,
) => {
  const min = getParameterMin(parameter);
  const max = getParameterMax(parameter);
  const normalized = (points ?? [])
    .map((point) => ({
      value: clamp(Number(point.value), min, max),
      fan_speed: clamp(Number(point.fan_speed), 0, 100),
    }))
    .filter((point) => Number.isFinite(point.value) && Number.isFinite(point.fan_speed));

  const payload = normalized.length
    ? normalized
    : [
      { value: min, fan_speed: 0 },
      { value: max, fan_speed: 100 },
    ];

  return payload.map((point) => ({
    value: Number(point.value.toFixed(3)),
    fan_speed: Math.round(point.fan_speed),
  }));
};

async function getCriticalCurvePayloadPoints(parameter: ExtendedParam) {
  if (parameter.name === selectedParam.value.name) {
    return getCurvePayloadPoints();
  }

  const data = await getRoomCurve(roomId, parameter.name);
  return getParameterCurvePayloadPoints(parameter, data?.points);
}

const formatNumber = (value: number | null | undefined) => {
  if (value === null || value === undefined) return "-";
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
};

const pointCount = computed(() => series.value[0]?.data?.length || 0);
const inputRangeLabel = computed(() => {
  const min = formatNumber(inputMin.value);
  const max = formatNumber(inputMax.value);
  const unit = selectedParam.value.unit || "";
  return `${min}-${max}${unit}`;
});
const criticalValueLabel = computed(() => {
  const value = selectedCriticalParam.value.critical_value;
  if (value === null || value === undefined) return "Not set";
  return `${formatNumber(value)}${selectedCriticalParam.value.unit || ""}`;
});
const criticalButtonLabel = computed(() => (
  selectedCriticalParam.value.critical_value === null || selectedCriticalParam.value.critical_value === undefined
    ? "Set threshold"
    : "Edit threshold"
));
const aiControlEnabled = computed(() => aiControlSettings.value?.enabled ?? false);
const isAiModeSelected = computed(() => selectedAutomationMode.value === "ai");
const isManualModeSelected = computed(() => selectedAutomationMode.value === "manual");
const isCurveEditingDisabled = computed(() => isReadOnly.value || aiControlEnabled.value);
const isCriticalEditingDisabled = computed(() => isReadOnly.value || !selectedCriticalParam.value.name);
const activeSourceLabel = computed(() => aiControlEnabled.value ? "AI control" : "Response curve");
const aiModeLabel = computed(() => aiControlEnabled.value ? "Active" : "Configure");
const manualModeLabel = computed(() => aiControlEnabled.value ? "Paused" : "Active");
const automationDescription = computed(() => {
  if (isAiModeSelected.value) {
    return "AI uses CO₂, temperature, humidity, supply and exhaust signals together.";
  }

  if (aiControlEnabled.value) {
    return "Manual response curve is paused while AI control is active.";
  }

  return selectedParam.value.label
    ? `${selectedParam.value.label} response profile`
    : "Fan-speed response curve";
});
const automationStatusSeverity = computed(() => {
  if (isReadOnly.value) return "secondary";
  if (isAiModeSelected.value || aiControlEnabled.value) return "info";
  return hasChanges.value ? "warn" : "success";
});
const automationStatusLabel = computed(() => {
  if (isReadOnly.value) return "Read only";
  if (isAiModeSelected.value) return aiControlEnabled.value ? "AI active" : "AI setup";
  if (aiControlEnabled.value) return "Curve paused";
  return hasChanges.value ? "Draft" : "Saved";
});

const chartOptions = computed(() => ({
  chart: {
    type: 'line' as const,
    toolbar: { show: false },
    zoom: { enabled: false },
    animations: {
      enabled: true,
      easing: 'easeinout',
      speed: 220,
    },
    events: {
      dataPointSelection: handleDataPointSelection,
      click: handleChartClick
    }
  },
  stroke: {
    curve: 'straight',
    width: 3,
    colors: [chartPalette.primary]
  },
  markers: {
    size: 6,
    colors: [chartPalette.surface],
    strokeColors: chartPalette.primary,
    strokeWidth: 3,
    hover: { size: 8 },
    selected: {
      size: 8,
      colors: [chartPalette.surface],
      strokeColors: chartPalette.primary,
      strokeWidth: 3
    }
  },
  grid: {
    borderColor: chartPalette.border,
    strokeDashArray: 2,
  },
  xaxis: {
    type: 'numeric',
    min: inputMin.value,
    max: inputMax.value,
    tickAmount: 6,
    labels: {
      formatter: (val: number) => formatAxisValue(val)
    }
  },
  yaxis: {
    min: 0,
    max: 100,
    tickAmount: 5,
    labels: {
      formatter: (val: number) => `${formatAxisValue(val)}%`
    }
  },
  tooltip: {
    intersect: true,
    shared: false
  },
  annotations: {
    xaxis:
      selectedParam.value.critical_value !== null && selectedParam.value.critical_value !== undefined
        ? [{
          x: clamp(selectedParam.value.critical_value, inputMin.value, inputMax.value),
          strokeDashArray: 0,
          borderColor: chartPalette.danger,
          label: {
            text: 'Critical',
            style: {
              color: chartPalette.onDanger,
              background: chartPalette.danger,
              fontSize: '12px',
              padding: {
                left: 5,
                right: 5,
                top: 2,
                bottom: 2
              }
            }
          }
        }]
        : []
  }
}));

function getCurvePointFromEvent(event: { clientX: number; clientY: number }, chartContext: ChartContext): CurvePoint | null {
  const chartEl = chart.value?.$el as HTMLElement | undefined;
  if (!chartEl || !chartContext?.w?.globals) return null;

  const bounds = chartEl.getBoundingClientRect();
  const globals = chartContext.w.globals as Record<string, any>;
  const plotWidth = Number(globals.gridWidth) || bounds.width;
  const plotHeight = Number(globals.gridHeight) || bounds.height;
  const translateX = Number(globals.translateX);
  const translateY = Number(globals.translateY);
  const plotLeft = bounds.left + (Number.isFinite(translateX) ? translateX : Math.max(0, (bounds.width - plotWidth) / 2));
  const plotTop = bounds.top + (Number.isFinite(translateY) ? translateY : Math.max(0, (bounds.height - plotHeight) / 2));

  const ratioX = (event.clientX - plotLeft) / plotWidth;
  const ratioY = (event.clientY - plotTop) / plotHeight;
  if (ratioX < 0 || ratioX > 1 || ratioY < 0 || ratioY > 1) return null;

  return {
    x: inputMin.value + (inputMax.value - inputMin.value) * ratioX,
    y: 100 - ratioY * 100,
  };
}

function handleDataPointSelection(event: ChartEvent, chartContext: ChartContext, config: { dataPointIndex: number }) {
  if (isCurveEditingDisabled.value) return;

  const pointIndex = config.dataPointIndex;
  const points = series.value[0].data as CurvePoint[];
  
  if (pointIndex === 0 || pointIndex === points.length - 1) {
    return;
  }
  
  selectedPointIndex.value = pointIndex;
  
  const handleMouseMove = (e: MouseEvent) => {
    const nextPoint = getCurvePointFromEvent(e, chartContext);
    if (!nextPoint) return;

    const minGap = (inputMax.value - inputMin.value) / 1000;
    const minX = points[pointIndex - 1].x + minGap;
    const maxX = points[pointIndex + 1].x - minGap;

    points[pointIndex] = {
      x: clamp(nextPoint.x, minX, maxX),
      y: clamp(nextPoint.y, 0, 100),
    };
    commitCurvePoints(points);
    hasChanges.value = true;
  };

  const handleMouseUp = () => {
    document.removeEventListener('mousemove', handleMouseMove);
    document.removeEventListener('mouseup', handleMouseUp);
  };

  document.addEventListener('mousemove', handleMouseMove);
  document.addEventListener('mouseup', handleMouseUp);
}

function handleChartClick(event: ChartEvent, chartContext: ChartContext, config: { dataPointIndex: number | undefined }) {
  if (isCurveEditingDisabled.value) return;

  if (config.dataPointIndex !== undefined && config.dataPointIndex >= 0) return;

  const point = getCurvePointFromEvent(event, chartContext);
  if (!point) return;

  const points = [...series.value[0].data] as CurvePoint[];
  let insertIndex = points.length;

  for (let i = 0; i < points.length; i++) {
    if (point.x < points[i].x) {
      insertIndex = i;
      break;
    }
  }

  if (insertIndex === 0 || insertIndex === points.length) return;

  points.splice(insertIndex, 0, {
    x: clamp(point.x, inputMin.value, inputMax.value),
    y: clamp(point.y, 0, 100),
  });
  commitCurvePoints(points);
  hasChanges.value = true;
}

async function loadParams() {
  isLoading.value = true;
  try {
    const res = await getAvailableParameters(roomId);
    const fetched = res;

    for (const p of fetched) {
      if (!parametersOptions.value.find(x => x.name === p.name)) {
        parametersOptions.value.push({
          name: p.name,
          label: getLabel(p.name),
          unit: p.unit,
          min_value: p.min_value,
          max_value: p.max_value,
          critical_value: p.critical_value,
        });
      }
    }
    if (parametersOptions.value.length > 0) {
      selectedParam.value = parametersOptions.value[0];
      selectedCriticalParam.value = parametersOptions.value[0];
    }
  } catch (err) {
    console.error("Error loading parameters:", err);
  } finally {
    isLoading.value = false;
  }
}

async function loadChartData(paramName: string) {
  isLoading.value = true;
  try {
    const data = await getRoomCurve(roomId, paramName);
    
    if (data && data.points && data.points.length > 0) {
      const points = data.points.map(point => ({
        x: point.value,
        y: point.fan_speed
      }));
      savedCurvePoints.value = normalizeCurvePoints(points);
      commitCurvePoints(savedCurvePoints.value);
    } else {
      savedCurvePoints.value = normalizeCurvePoints([]);
      commitCurvePoints(savedCurvePoints.value);
    }
  } catch (err) {
    console.error("Error loading chart data:", err);
    savedCurvePoints.value = normalizeCurvePoints([]);
    commitCurvePoints(savedCurvePoints.value);
  } finally {
    isLoading.value = false;
  }
}

function addPoint() {
  if (isCurveEditingDisabled.value) return;

  const points = series.value[0].data as CurvePoint[];
  if (points.length < 2) return;

  let maxDistance = 0;
  let insertIndex = 1;

  for (let i = 0; i < points.length - 1; i++) {
    const distance = points[i + 1].x - points[i].x;
    if (distance > maxDistance) {
      maxDistance = distance;
      insertIndex = i + 1;
    }
  }

  const prevPoint = points[insertIndex - 1];
  const nextPoint = points[insertIndex];
  const x = (prevPoint.x + nextPoint.x) / 2;
  const y = (prevPoint.y + nextPoint.y) / 2;

  points.splice(insertIndex, 0, { x, y });
  commitCurvePoints(points);
  hasChanges.value = true;
}

function deleteSelectedPoint() {
  if (isCurveEditingDisabled.value) return;

  if (selectedPointIndex.value === null) return;
  
  const points = series.value[0].data as CurvePoint[];
  if (selectedPointIndex.value === 0 || selectedPointIndex.value === points.length - 1) {
    return;
  }
  
  points.splice(selectedPointIndex.value, 1);
  commitCurvePoints(points);
  selectedPointIndex.value = null;
  hasChanges.value = true;
}

function openCriticalValueDialog() {
  if (isCriticalEditingDisabled.value) return;

  tempCriticalValue.value = selectedCriticalParam.value.critical_value ?? null;
  showCriticalValueDialog.value = true;
}

function updateSelectedCriticalValue(value: number | null) {
  selectedCriticalParam.value = {
    ...selectedCriticalParam.value,
    critical_value: value ?? undefined,
  };

  const option = parametersOptions.value.find(parameter => parameter.name === selectedCriticalParam.value.name);
  if (option) {
    option.critical_value = value ?? undefined;
  }
}

async function saveCriticalValue() {
  if (isCriticalEditingDisabled.value) return;

  const parameter = selectedCriticalParam.value;
  const previousValue = parameter.critical_value ?? null;
  const nextValue = tempCriticalValue.value === null
    ? null
    : clamp(tempCriticalValue.value, criticalInputMin.value, criticalInputMax.value);

  isSavingCritical.value = true;
  updateSelectedCriticalValue(nextValue);

  try {
    await updateRoomCurve(roomId, parameter.name, {
      points: await getCriticalCurvePayloadPoints(parameter),
      critical_value: nextValue,
    });

    showCriticalValueDialog.value = false;
    toast.add({ severity: "success", summary: "Critical threshold saved", life: 2400 });
  } catch (err) {
    updateSelectedCriticalValue(previousValue);
    console.error("Error saving critical threshold:", err);
    toast.add({ severity: "error", summary: "Error", detail: "Failed to save critical threshold", life: 3000 });
  } finally {
    isSavingCritical.value = false;
  }
}

function selectAutomationMode(mode: AutomationMode) {
  selectedAutomationMode.value = mode;
  selectedPointIndex.value = null;
}

function handleAiControlUpdated(settings: AiControlSettings) {
  aiControlSettings.value = settings;
  selectedAutomationMode.value = settings.enabled ? "ai" : "manual";
  selectedPointIndex.value = null;
}

async function saveChanges() {
  if (isCurveEditingDisabled.value) return;

  try {
    const curvePoints = normalizeCurvePoints(series.value[0].data as CurvePoint[]);

    await updateRoomCurve(roomId, selectedParam.value.name, {
      points: getCurvePayloadPoints(curvePoints),
      critical_value: selectedParam.value.critical_value ?? null
    });

    savedCurvePoints.value = curvePoints;
    hasChanges.value = false;
    toast.add({ severity: 'success', summary: 'Success', detail: 'Curve saved successfully', life: 3000 });
  } catch (err) {
    console.error("Error saving curve:", err);
    toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to save curve', life: 3000 });
  }
}

function getLabel(name: string) { return PARAMETER_LABELS[name] || name; }

watch(selectedParam, (newVal) => {
  if (newVal) {
    selectedPointIndex.value = null;
    series.value[0].name = newVal.label;
    loadChartData(newVal.name);
    hasChanges.value = false;
  }
});

onMounted(async () => {
  await loadParams();
});
</script>

<style scoped>
.section-page {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  min-width: 0;
  width: 100%;
}

.automation-panel {
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  min-width: 0;
  overflow: hidden;
  width: 100%;
}

.automation-panel__header {
  align-items: center;
  background: var(--app-surface);
  border-bottom: 1px solid var(--app-border);
  display: flex;
  gap: var(--app-list-gap);
  justify-content: space-between;
  min-width: 0;
  padding: var(--app-panel-padding);
}

.automation-panel__title {
  align-items: center;
  display: flex;
  gap: 12px;
  min-width: 0;
}

.automation-panel__icon {
  align-items: center;
  background: var(--app-surface-soft);
  border: 1px solid var(--app-border);
  border-radius: 5px;
  color: var(--app-text);
  display: inline-flex;
  flex: 0 0 auto;
  height: 36px;
  justify-content: center;
  width: 36px;
}

.automation-panel__copy {
  min-width: 0;
}

.automation-panel__eyebrow {
  color: var(--app-muted);
  display: block;
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 650;
  line-height: 1rem;
  text-transform: uppercase;
}

.automation-panel h2 {
  color: var(--app-text-strong);
  font-size: 1rem;
  font-weight: 780;
  line-height: 1.25rem;
  margin: 0;
}

.automation-panel p {
  color: var(--app-muted);
  font-size: 0.8125rem;
  line-height: 1.15rem;
  margin: 2px 0 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.automation-panel__save {
  align-items: center;
  display: flex;
  flex: 0 0 auto;
  gap: 8px;
}

.automation-skeleton {
  margin: var(--app-panel-padding);
}

.automation-panel__body {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: var(--app-gap-sm);
  min-height: 0;
  min-width: 0;
  overflow: auto;
  padding: var(--app-panel-padding);
}

.automation-ai-panel {
  flex: 0 0 auto;
}

.critical-card {
  align-items: center;
  background: color-mix(in srgb, var(--app-surface) 88%, var(--app-surface-soft));
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: grid;
  gap: var(--app-gap-sm) var(--app-list-gap);
  grid-template-columns: minmax(180px, 260px) minmax(0, 1fr);
  min-width: 0;
  padding: 12px;
}

.critical-card__copy,
.critical-card__value {
  min-width: 0;
}

.critical-card__copy span,
.critical-card__value span {
  color: var(--app-muted);
  display: block;
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 700;
  line-height: 1rem;
  text-transform: uppercase;
}

.critical-card__copy strong,
.critical-card__value strong {
  color: var(--app-text-strong);
  display: block;
  font-size: 0.98rem;
  font-weight: 800;
  line-height: 1.25rem;
  margin-top: 2px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.critical-card__controls {
  align-items: center;
  display: grid;
  gap: var(--app-gap-sm);
  grid-template-columns: minmax(180px, 240px) minmax(110px, 1fr) auto;
  min-width: 0;
}

.critical-card__select {
  min-width: 0;
  width: 100%;
}

.automation-mode {
  align-items: stretch;
  display: grid;
  gap: var(--app-gap-sm);
  grid-template-columns: repeat(2, minmax(0, 1fr));
  min-width: 0;
}

.automation-mode__option {
  align-items: center;
  background: color-mix(in srgb, var(--app-surface) 84%, var(--app-surface-soft));
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  color: inherit;
  cursor: pointer;
  display: flex;
  font: inherit;
  gap: 12px;
  min-height: 70px;
  min-width: 0;
  padding: 12px;
  text-align: left;
  transition: background-color 0.18s ease, border-color 0.18s ease, box-shadow 0.18s ease, opacity 0.18s ease;
}

.automation-mode__option:hover {
  border-color: color-mix(in srgb, var(--app-primary) 30%, var(--app-border));
}

.automation-mode__option--active {
  background: color-mix(in srgb, var(--app-primary) 10%, var(--app-surface));
  border-color: color-mix(in srgb, var(--app-primary) 52%, var(--app-border));
  box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--app-primary) 24%, transparent);
}

.automation-mode__icon {
  align-items: center;
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: 5px;
  color: var(--app-text);
  display: inline-flex;
  flex: 0 0 auto;
  height: 38px;
  justify-content: center;
  width: 38px;
}

.automation-mode__option--active .automation-mode__icon {
  border-color: color-mix(in srgb, var(--app-primary) 44%, var(--app-border));
  color: var(--app-primary);
}

.automation-mode__copy {
  min-width: 0;
}

.automation-mode__copy span {
  color: var(--app-muted);
  display: block;
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 700;
  letter-spacing: 0;
  line-height: 1rem;
  text-transform: uppercase;
}

.automation-mode__copy strong {
  color: var(--app-text-strong);
  display: block;
  font-size: 1.05rem;
  font-weight: 820;
  line-height: 1.3rem;
  margin-top: 2px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.curve-card {
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 520px;
  min-width: 0;
  overflow: hidden;
}

.curve-card--standby {
  background: color-mix(in srgb, var(--app-surface-soft) 58%, var(--app-surface));
  border-color: color-mix(in srgb, var(--app-primary) 24%, var(--app-border));
  box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--app-primary) 12%, transparent);
}

.curve-card--standby .automation-controls,
.curve-card--standby .chart-surface,
.curve-card--standby .curve-summary__item {
  opacity: 0.68;
}

.automation-controls {
  align-items: end;
  border-bottom: 1px solid var(--app-border);
  display: grid;
  gap: var(--app-gap-sm) var(--app-list-gap);
  grid-template-columns: minmax(180px, 230px) minmax(0, 1fr);
  padding: var(--app-panel-padding);
}

.automation-controls__field {
  display: flex;
  flex-direction: column;
  gap: var(--app-gap-xs);
  min-width: 0;
}

.automation-controls__field label {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 650;
  line-height: 1rem;
  text-transform: uppercase;
}

.automation-controls__select {
  min-height: var(--app-control-height);
  width: 100%;
}

.automation-controls__select :deep(.p-select-label) {
  min-height: calc(var(--app-control-height) - 2px);
}

.automation-controls__actions {
  align-items: center;
  display: flex;
  flex-wrap: wrap;
  gap: var(--app-gap-sm);
  justify-content: flex-end;
  min-width: 0;
}

.automation-workspace {
  display: grid;
  flex: 1;
  gap: var(--app-gap-sm);
  grid-template-columns: minmax(0, 1fr) minmax(190px, 230px);
  min-height: 0;
  min-width: 0;
  padding: var(--app-panel-padding);
}

.chart-surface {
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex-direction: column;
  min-height: 0;
  min-width: 0;
  overflow: hidden;
}

.chart-surface__header {
  align-items: center;
  background: var(--app-surface);
  border-bottom: 1px solid var(--app-border);
  display: flex;
  gap: var(--app-list-gap);
  justify-content: space-between;
  min-width: 0;
  min-height: 48px;
  padding: 9px var(--app-panel-padding);
}

.chart-surface__header span,
.curve-summary__item span {
  color: var(--app-muted);
  display: block;
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 650;
  line-height: 1rem;
  text-transform: uppercase;
}

.chart-surface__header strong,
.curve-summary__item strong {
  color: var(--app-text-strong);
  display: block;
  font-size: 1rem;
  font-weight: 800;
  line-height: 1.35rem;
  margin-top: 2px;
}

.chart-surface__body {
  flex: 1;
  min-height: 0;
  min-width: 0;
  padding: 6px var(--app-panel-padding) 8px;
}

.curve-summary {
  align-content: start;
  display: grid;
  gap: var(--app-gap-sm);
  grid-auto-rows: minmax(0, auto);
  min-width: 0;
}

.curve-summary__item {
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex-direction: column;
  justify-content: center;
  min-height: 76px;
  padding: 11px 12px;
}

@media (max-width: 920px) {
  .automation-mode {
    grid-template-columns: 1fr;
  }

  .critical-card,
  .critical-card__controls {
    grid-template-columns: 1fr;
  }

  .automation-controls,
  .automation-workspace {
    grid-template-columns: 1fr;
  }

  .automation-controls__actions {
    justify-content: flex-start;
  }

  .curve-summary {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 560px) {
  .automation-panel__header {
    align-items: flex-start;
    flex-direction: column;
  }

  .automation-panel__save,
  .automation-panel__save :deep(.p-button),
  .automation-controls__actions,
  .automation-controls__actions :deep(.p-button) {
    width: 100%;
  }

  .automation-controls__actions :deep(.p-button) {
    justify-content: center;
  }

  .automation-workspace,
  .automation-controls,
  .automation-panel__body {
    padding-left: 12px;
    padding-right: 12px;
  }

  .chart-surface__body {
    min-height: 360px;
    padding: 8px 6px 4px;
  }

  .curve-summary {
    grid-template-columns: 1fr;
  }
}
</style>
