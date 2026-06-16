<template>
  <div class="items-center flex-grow">
    <div class="flex flex-row gap-4 items-center justify-between mb-4">
      <h2 class="text-lg font-semibold text-gray-800">Parameter Settings</h2>
    </div>

    <div class="flex flex-row gap-4 items-center justify-between">
      <Select v-model="selectedParam" :options="parametersOptions" optionLabel="label" class="w-full md:w-56" />
      <Button 
        icon="pi pi-save" 
        @click="saveChanges" 
        severity="primary" 
        label="Save" 
        :disabled="!hasChanges"
      />
    </div>

    <div v-if="isLoading" class="flex items-center justify-center h-64">
      <span class="text-gray-500">Loading...</span>
    </div>

    <div v-else class="bg-white shadow-md rounded-lg p-4 mt-4">
      <div class="flex flex-row items-center justify-between">
        <h3 class="text-lg font-semibold text-gray-800 mb-4">{{ selectedParam.label }}</h3>
        <div class="flex gap-2">
          <Button
            icon="pi pi-plus"
            @click="addPoint"
            severity="secondary"
            text
            tooltip="Add Point Between"
          />
          <Button
            icon="pi pi-trash"
            @click="deleteSelectedPoint"
            severity="secondary"
            text
            :disabled="selectedPointIndex === null"
            tooltip="Delete Selected Point"
          />
          <Button
            icon="pi pi-exclamation-triangle"
            @click="openCriticalValueDialog"
            severity="secondary"
            text
            :class="{ 'text-red-500': selectedParam.critical_value }"
            tooltip="Set Critical Value"
          />
        </div>
      </div>
      
      <div class="relative">
        <ApexCharts
          type="line" 
          height="350" 
          :options="chartOptions" 
          :series="series"
          ref="chart"
        />
        <div v-if="!series[0].data.length" class="absolute inset-0 flex items-center justify-center bg-white bg-opacity-80">
          <span class="text-gray-500">Click on the chart to add points or use the Add Point button.</span>
        </div>
      </div>
    </div>

    <Dialog v-model:visible="showCriticalValueDialog" modal header="Set Critical Value" :style="{ width: '400px' }">
      <div class="flex flex-col gap-4">
        <div class="flex flex-col gap-2">
          <label class="text-sm font-medium text-gray-700">Critical Value</label>
          <InputNumber 
            v-model="tempCriticalValue" 
            :min="selectedParam.min_value || 0" 
            :max="selectedParam.max_value || 100"
            :step="0.1"
            class="w-full"
          />
        </div>
      </div>
      <template #footer>
        <div class="flex justify-end gap-2">
          <Button label="Cancel" @click="showCriticalValueDialog = false" text />
          <Button label="Save" @click="saveCriticalValue" severity="primary" />
        </div>
      </template>
    </Dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch, computed } from "vue";
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
import ApexCharts from "vue3-apexcharts";
import { useToast } from "primevue/usetoast";
import Dialog from 'primevue/dialog';
import InputNumber from 'primevue/inputnumber';
import { useChartConfig } from "@/config/chartConfig";

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

const chartOptions = computed(() => ({
  chart: {
    type: 'line' as const,
    toolbar: { show: false },
    zoom: { enabled: false },
    animations: {
      enabled: true,
      easing: 'easeinout',
      speed: 800,
    },
    events: {
      dataPointSelection: handleDataPointSelection,
      click: handleChartClick
    }
  },
  stroke: {
    curve: 'straight',
    width: 3,
    colors: ['#7C3AED']
  },
  markers: {
    size: 6,
    colors: ['#fff'],
    strokeColors: '#7C3AED',
    strokeWidth: 3,
    hover: { size: 8 },
    selected: {
      size: 8,
      colors: ['#fff'],
      strokeColors: '#7C3AED',
      strokeWidth: 3
    }
  },
  grid: {
    borderColor: '#e0e6ed',
    strokeDashArray: 4,
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
    xaxis: [{
      x: selectedParam.value.critical_value,
      strokeDashArray: 0,
      borderColor: '#EF4444',
      label: {
        text: 'Critical',
        style: {
          color: '#fff',
          background: '#EF4444',
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
  tempCriticalValue.value = selectedParam.value.critical_value || null;
  showCriticalValueDialog.value = true;
}

function saveCriticalValue() {
  selectedParam.value.critical_value = tempCriticalValue.value || undefined;
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