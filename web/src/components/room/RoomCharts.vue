<template>
  <div class="items-center flex-grow">
    <div class="flex flex-row gap-4 items-center justify-between">
      <Select v-model="selectedParam" :options="parametersOptions" optionLabel="label" class="w-full md:w-56" />
      
      <div class="flex flex-row gap-4 items-center">
        <FloatLabel variant="on">
          <DatePicker v-model="fromDate" inputId="on_label" showIcon showTime hourFormat="24" iconDisplay="input" />
          <label for="on_label">From</label>
        </FloatLabel>
        <i class="pi pi-minus my-auto" style="font-size: 1rem"></i>
        <FloatLabel variant="on">
          <DatePicker v-model="toDate" inputId="on_label" showIcon showTime hourFormat="24" iconDisplay="input" />
          <label for="on_label">To</label>
        </FloatLabel>
      </div>
    </div>

    <div v-if="isLoading" class="flex items-center justify-center h-64">
      <span class="text-gray-500">Loading...</span>
    </div>

    <div v-else class="bg-white shadow-md rounded-lg p-4 mt-4">
      <div class="flex flex-row items-center justify-between">
        <h3 class="text-lg font-semibold text-gray-800 mb-4">{{ selectedParam.label }}</h3>
        <SelectButton v-model="selectedInterval" :options="intervalOptions" optionLabel="name" />
      </div>
      
      <div class="relative">
        <ApexCharts
          type="area" 
          height="350" 
          :options="chartOptions" 
          :series="series" 
        />
        <div v-if="!series[0].data.length" class="absolute inset-0 flex items-center justify-center bg-white bg-opacity-80">
          <span class="text-gray-500">No data available for the chart.</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import api from "@/api";
import DatePicker from 'primevue/datepicker';
import FloatLabel from 'primevue/floatlabel';
import SelectButton from 'primevue/selectbutton';
import Select from 'primevue/select';
import { HistoryEntry, Param } from "@/services/apiService";

import ApexCharts from "vue3-apexcharts";

interface SeriesData {
  name: string;
  data: Array<{
    x: number;
    y: number;
  }>;
}

const series = ref<SeriesData[]>([
  {
    name: "",
    data: [],
  },
]);

const chartOptions = ref({
  chart: {
    type: 'area' as const,
    toolbar: { show: false },
    zoom: { enabled: false },
    animations: {
      enabled: true,
      easing: 'easeinout',
      speed: 800,
    }
  },
  dataLabels: {
    enabled: false
  },
  stroke: {
    curve: 'smooth',
    width: 3,
    colors: ['#7C3AED']
  },
  fill: {
    type: 'gradient',
    gradient: {
      shadeIntensity: 1,
      opacityFrom: 0.5,
      opacityTo: 0,
      stops: [0, 90, 100],
      colorStops: [
        { offset: 0, color: "#7C3AED", opacity: 0.4 },
        { offset: 100, color: "#ffffff", opacity: 0 }
      ]
    }
  },
  xaxis: {
    categories: [] as string[],
    labels: {
      style: { fontSize: '14px' }
    },
    type: 'datetime',
    tooltip: {
      enabled: false
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
  markers: {
    size: 6,
    colors: ['#fff'],
    strokeColors: '#7C3AED',
    strokeWidth: 3,
    hover: { size: 8 },
  },
  grid: {
    borderColor: '#e0e6ed',
    strokeDashArray: 4,
  },
  tooltip: {
    x: {
      format: 'dd MMM HH:mm'
    },
    y: {
      formatter: (val: number) => val.toFixed(1)
    }
  }
});

const intervalOptions = ref([
  { name: "Minute", value: "minute" },
  { name: "Hour", value: "hour" },
  { name: "Day", value: "day" },
]);
const parametersOptions = ref<Param[]>([
  { label: "Device Speed", name: "device_speed", unit: "%" }
]);

const route = useRoute();
const roomId = Number(route.params.roomId);

const isLoading = ref(false);
const selectedParam = ref<Param>(parametersOptions.value[0]);
const selectedInterval = ref(intervalOptions.value[1])
const fromDate = ref<Date>(new Date(    
    new Date().getFullYear(),
    new Date().getMonth(), 
    new Date().getDate() - 1,
    new Date().getHours(),
    new Date().getMinutes()
  )
);
const toDate = ref<Date>(new Date());

const getLabel = (name: string) => {
  const labels: Record<string, string> = {
    temperature: "Temperature",
    humidity: "Humidity",
    pressure: "Pressure",
    co2: "CO₂",
    device_speed: "Ventilation speed",
  };
  return labels[name] || name;
};

const loadChartData = async () => {
  isLoading.value = true;
  series.value[0].data = [];
  chartOptions.value.xaxis.categories = [];

  try {
    const from = fromDate.value.getTime();
    const to = toDate.value.getTime();

    let res;

    if (selectedParam.value.name === "device_speed") {
      res = await api.get(`/room/${roomId}/history`, {
        params: { from, to, interval: selectedInterval.value.value },
      });
    } else {
      res = await api.get(
          `/room/${roomId}/${selectedParam.value.name}/history`,
          { params: { from, to, interval: selectedInterval.value.value } }
      );
    }

    const history: HistoryEntry[] = res.data?.data?.[0]?.history || [];

    if (history.length > 0) {
      const unit = parametersOptions.value.find(p => p.name === selectedParam.value.name)?.unit || "";
      const label = `${getLabel(selectedParam.value.name)} (${unit})`;

      series.value[0].name = label;
      series.value[0].data = history.map(h => ({
        x: h.timestamp * 1000,
        y: h.value
      }));
    }
  } catch (err) {
    console.error("Error loading chart data:", err);
  } finally {
    isLoading.value = false;
  }
};

const loadParams = async () => {
  try {
    const res = await api.get(`/room/${roomId}`);
    const fetched: Param[] = res.data;

    for (const p of fetched) {
      if (!parametersOptions.value.find(x => x.name === p.name)) {
        parametersOptions.value.push({
          name: p.name,
          label: getLabel(p.name),
          unit: p.unit,
        });
      }
    }
  } catch (err) {
    console.error("Error loading parameters:", err);
  }
};

onMounted(async () => {
  await loadParams();
  await loadChartData();
});

watch(
    [selectedParam, selectedInterval, fromDate, toDate],
    loadChartData
);
</script>
