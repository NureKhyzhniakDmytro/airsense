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
            <h2>Fan response curve</h2>
            <p>{{ selectedParam.label ? `${selectedParam.label} response profile` : 'Fan-speed response curve' }}</p>
          </div>
        </div>

        <div class="automation-panel__save">
          <Tag
            :severity="hasChanges ? 'warn' : 'success'"
            :value="hasChanges ? 'Draft' : 'Saved'"
            rounded
          />
          <Button
            icon="pi pi-save"
            @click="saveChanges"
            severity="primary"
            label="Save"
            :disabled="!hasChanges"
          />
        </div>
      </header>

      <Skeleton v-if="isLoading" class="automation-skeleton" height="31rem" />

      <template v-else>
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
            <Button
              icon="pi pi-plus"
              label="Point"
              @click="addPoint"
              severity="secondary"
              variant="outlined"
            />
            <Button
              icon="pi pi-trash"
              @click="deleteSelectedPoint"
              severity="secondary"
              variant="outlined"
              :disabled="selectedPointIndex === null"
              aria-label="Delete selected point"
              v-tooltip.top="'Delete selected point'"
            />
            <Button
              icon="pi pi-exclamation-triangle"
              label="Critical"
              @click="openCriticalValueDialog"
              severity="secondary"
              variant="outlined"
              :class="{ 'automation-controls__critical-button': selectedParam.critical_value !== null && selectedParam.critical_value !== undefined }"
            />
          </div>
        </div>

        <div class="automation-workspace">
          <section class="chart-surface">
            <div class="chart-surface__header">
              <div>
                <span>Curve</span>
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
              <span>Input range</span>
              <strong>{{ inputRangeLabel }}</strong>
            </div>
            <div class="curve-summary__item">
              <span>Curve points</span>
              <strong>{{ pointCount }}</strong>
            </div>
            <div class="curve-summary__item">
              <span>Critical</span>
              <strong>{{ criticalValueLabel }}</strong>
            </div>
          </aside>
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
            :min="selectedParam.min_value || 0" 
            :max="selectedParam.max_value || 100"
            :step="0.1"
            class="w-full"
          />
        </div>
      </div>
      <template #footer>
        <div class="entity-dialog-actions">
          <Button label="Cancel" @click="showCriticalValueDialog = false" text />
          <Button label="Save" @click="saveCriticalValue" severity="primary" />
        </div>
      </template>
    </Dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch, computed, defineAsyncComponent } from "vue";
import { useRoute } from "vue-router";
import { 
  getRoomCurve, 
  updateRoomCurve, 
  getAvailableParameters,
} from "@/services/apiService";
import { PARAMETER_LABELS, type ExtendedParam } from "@/types/sensor";
import type { 
  ChartEvent, 
  ChartContext,
} from "@/types/chart";
import Select from 'primevue/select';
import Button from 'primevue/button';
import { useToast } from "primevue/usetoast";
import Dialog from 'primevue/dialog';
import InputNumber from 'primevue/inputnumber';
import Skeleton from 'primevue/skeleton';
import Tag from 'primevue/tag';
import { useChartConfig } from "@/config/chartConfig";

const ApexCharts = defineAsyncComponent(() => import("vue3-apexcharts"));

const route = useRoute();
const roomId = Number(route.params.roomId);
const toast = useToast();
const chart = ref();
const isLoading = ref(true);
const hasChanges = ref(false);
const selectedPointIndex = ref<number | null>(null);
const showCriticalValueDialog = ref(false);
const tempCriticalValue = ref<number | null>(null);

const parametersOptions = ref<ExtendedParam[]>([]);
const selectedParam = ref<ExtendedParam>({ name: "", label: "", unit: "" });

const { series } = useChartConfig();

const formatNumber = (value: number | null | undefined) => {
  if (value === null || value === undefined) return "-";
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
};

const pointCount = computed(() => series.value[0]?.data?.length || 0);
const inputRangeLabel = computed(() => {
  const min = formatNumber(selectedParam.value.min_value);
  const max = formatNumber(selectedParam.value.max_value);
  const unit = selectedParam.value.unit || "";
  return `${min}-${max}${unit}`;
});
const criticalValueLabel = computed(() => {
  const value = selectedParam.value.critical_value;
  if (value === null || value === undefined) return "Not set";
  return `${formatNumber(value)}${selectedParam.value.unit || ""}`;
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
    colors: ['#0F766E']
  },
  markers: {
    size: 6,
    colors: ['#FBFCFD'],
    strokeColors: '#0F766E',
    strokeWidth: 3,
    hover: { size: 8 },
    selected: {
      size: 8,
      colors: ['#FBFCFD'],
      strokeColors: '#0F766E',
      strokeWidth: 3
    }
  },
  grid: {
    borderColor: '#D0D7DE',
    strokeDashArray: 2,
  },
  xaxis: {
    type: 'numeric',
    min: selectedParam.value.min_value || 0,
    max: selectedParam.value.max_value || 100,
    labels: {
      formatter: (val: number) => val.toFixed(1)
    }
  },
  yaxis: {
    min: 0,
    max: 100,
    tickAmount: 5,
    labels: {
      formatter: (val: number) => val.toFixed(1)
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
          x: selectedParam.value.critical_value,
          strokeDashArray: 0,
          borderColor: '#C24135',
          label: {
            text: 'Critical',
            style: {
              color: '#fff',
              background: '#C24135',
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

function handleDataPointSelection(event: ChartEvent, chartContext: ChartContext, config: { dataPointIndex: number }) {
  const pointIndex = config.dataPointIndex;
  const points = series.value[0].data;
  
  if (pointIndex === 0 || pointIndex === points.length - 1) {
    return;
  }
  
  selectedPointIndex.value = pointIndex;
  
  const handleMouseMove = (e: MouseEvent) => {
    const chartRect = chart.value.$el.getBoundingClientRect();
    const rawX = chartContext.w.globals.xAxisScale.niceMin + 
      (chartContext.w.globals.xAxisScale.niceMax - chartContext.w.globals.xAxisScale.niceMin) * 
      ((e.clientX - chartRect.left) / chartRect.width) * 1;
    
    const minX = selectedParam.value.min_value || 0;
    const maxX = selectedParam.value.max_value || 100;
    const x = Math.max(minX, Math.min(maxX, rawX));
    
    const rawY = 100 - (e.clientY - chartRect.top) / chartRect.height * 100;
    const y = Math.max(0, Math.min(100, rawY));
    
    series.value[0].data[pointIndex] = { x, y };
    series.value = [...series.value];
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
  if (config.dataPointIndex === undefined && chartContext && chartContext.w) {
    const chartRect = chart.value.$el.getBoundingClientRect();
    const x = chartContext.w.globals.xAxisScale.niceMin + 
      (chartContext.w.globals.xAxisScale.niceMax - chartContext.w.globals.xAxisScale.niceMin) * 
      (event.clientX - chartRect.left) / chartRect.width;
    
    const y = 100 - (event.clientY - chartRect.top) / chartRect.height * 100;

    const points = series.value[0].data;
    let insertIndex = points.length;

    for (let i = 0; i < points.length; i++) {
      if (x < points[i].x) {
        insertIndex = i;
        break;
      }
    }

    points.splice(insertIndex, 0, { x, y });
    series.value = [...series.value];
    hasChanges.value = true;
  }
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
      series.value[0].data = data.points.map(point => ({
        x: point.value,
        y: point.fan_speed
      }));
    } else {
      const param = parametersOptions.value.find(p => p.name === paramName);
      series.value[0].data = [
        { x: param?.min_value || 0, y: 0 },
        { x: param?.max_value || 100, y: 100 }
      ];
    }
    series.value = [...series.value];
  } catch (err) {
    console.error("Error loading chart data:", err);
    const param = parametersOptions.value.find(p => p.name === paramName);
    series.value[0].data = [
      { x: param?.min_value || 0, y: 0 },
      { x: param?.max_value || 100, y: 100 }
    ];
    series.value = [...series.value];
  } finally {
    isLoading.value = false;
  }
}

function addPoint() {
  const points = series.value[0].data;
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
  series.value = [...series.value];
  hasChanges.value = true;
}

function deleteSelectedPoint() {
  if (selectedPointIndex.value === null) return;
  
  const points = series.value[0].data;
  if (selectedPointIndex.value === 0 || selectedPointIndex.value === points.length - 1) {
    return;
  }
  
  points.splice(selectedPointIndex.value, 1);
  series.value = [...series.value];
  selectedPointIndex.value = null;
  hasChanges.value = true;
}

function openCriticalValueDialog() {
  tempCriticalValue.value = selectedParam.value.critical_value ?? null;
  showCriticalValueDialog.value = true;
}

function saveCriticalValue() {
  selectedParam.value.critical_value = tempCriticalValue.value ?? undefined;
  showCriticalValueDialog.value = false;
  hasChanges.value = true;
}

async function saveChanges() {
  try {
    const points = series.value[0].data.map(point => ({
      value: point.x,
      fan_speed: Math.round(point.y)
    }));

    await updateRoomCurve(roomId, selectedParam.value.name, {
      points,
      critical_value: selectedParam.value.critical_value
    });

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
  gap: 12px;
  justify-content: space-between;
  min-width: 0;
  padding: 12px;
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

.automation-controls {
  align-items: end;
  border-bottom: 1px solid var(--app-border);
  display: grid;
  gap: var(--app-gap-md);
  grid-template-columns: minmax(220px, 360px) minmax(0, 1fr);
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
  width: 100%;
}

.automation-controls__actions {
  align-items: center;
  display: flex;
  flex-wrap: wrap;
  gap: var(--app-gap-sm);
  justify-content: flex-end;
  min-width: 0;
}

.automation-controls__critical-button {
  border-color: color-mix(in srgb, var(--app-danger) 36%, var(--app-border));
  color: var(--app-danger);
}

.automation-workspace {
  display: grid;
  flex: 1;
  gap: var(--app-gap-md);
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
  gap: 12px;
  justify-content: space-between;
  min-width: 0;
  padding: 10px var(--app-panel-padding);
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
  padding: 8px var(--app-panel-padding) 10px;
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
  .automation-controls,
  .automation-workspace {
    grid-template-columns: 1fr;
  }

  .automation-controls__actions {
    justify-content: flex-start;
  }

  .curve-summary {
    grid-template-columns: repeat(3, minmax(0, 1fr));
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
  .automation-controls {
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
