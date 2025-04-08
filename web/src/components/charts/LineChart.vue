<template>
  <canvas ref="canvasRef" class="w-full h-64"></canvas>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from "vue";
import { Chart, registerables } from "chart.js";
Chart.register(...registerables);

const props = defineProps<{ data: any }>();

const canvasRef = ref<HTMLCanvasElement | null>(null);
let chartInstance: Chart | null = null;

const renderChart = () => {
  if (!canvasRef.value) return;

  if (chartInstance) {
    chartInstance.destroy();
  }
  console.log(props.data);

  chartInstance = new Chart(canvasRef.value, {
    type: "line",
    data: props.data,
    options: {
      responsive: true,
      scales: {
        y: { beginAtZero: false },
      },
    },
  });
};

onMounted(() => {
  renderChart();
});

watch(() => props.data, () => {
  renderChart();
}, { deep: true });
</script>
