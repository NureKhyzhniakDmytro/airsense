<template>
  <div class="bg-white shadow-md rounded-lg p-4 relative" ref="container">
    <h3 class="text-lg font-semibold text-gray-800 mb-4">
      Швидкість вентиляції (%) від {{ chartLabel }} ({{ chartUnit }})
    </h3>
    <canvas ref="chartCanvas" class="block w-full h-[400]"></canvas>
    <button
        @click="$emit('clear-points')"
        class="mt-4 px-4 py-2 bg-red-500 text-white rounded hover:bg-red-600"
    >
      Очистити точки
    </button>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue';

interface ChartData {
  labels: number[];
  datasets: { data: number[] }[];
}

const props = defineProps<{
  chartData: ChartData;
  chartMinX: number;
  chartMaxX: number;
  chartLabel: string;
  chartUnit: string;
}>();

defineEmits<{ (e: 'clear-points'): void }>();

const chartCanvas = ref<HTMLCanvasElement | null>(null);
const container = ref<HTMLElement | null>(null);
const draggingPoint = ref<number | null>(null);
let animationFrameId: number | null = null;
let ctx: CanvasRenderingContext2D | null = null;
let resizeObserver: ResizeObserver | null = null;

const updateCanvasSize = () => {
  if (!chartCanvas.value || !container.value) return;

  const { width, height } = container.value.getBoundingClientRect();
  // Вычитаем padding (4 * 16px = 64px по горизонтали, 64px + высота заголовка и кнопки по вертикали)
  const canvasWidth = width - 64;
  const canvasHeight = height - 128; // Примерная высота с учетом заголовка и кнопки

  chartCanvas.value.width = canvasWidth;
  chartCanvas.value.height = canvasHeight;
  chartCanvas.value.style.width = `${canvasWidth}px`;
  chartCanvas.value.style.height = `${canvasHeight}px`;

  drawChart();
};

const drawChart = () => {
  if (!ctx || !chartCanvas.value) return;

  const padding = 50;
  const width = chartCanvas.value.width - padding * 2;
  const height = chartCanvas.value.height - padding * 2;

  ctx.clearRect(0, 0, chartCanvas.value.width, chartCanvas.value.height);

  const getX = (val: number) => padding + ((val - props.chartMinX) / (props.chartMaxX - props.chartMinX)) * width;
  const getY = (val: number) => padding + height - (val / 100) * height;

  const labels = props.chartData.labels;
  const values = props.chartData.datasets[0].data;
  const points = labels.map((x, i) => ({
    x: getX(x),
    y: getY(values[i])
  }));

  ctx.strokeStyle = '#e5e7eb';
  ctx.lineWidth = 1;
  ctx.beginPath();
  for (let i = 0; i <= 5; i++) {
    const y = padding + (i * height) / 5;
    ctx.moveTo(padding, y);
    ctx.lineTo(padding + width, y);

    const x = padding + (i * width) / 5;
    ctx.moveTo(x, padding);
    ctx.lineTo(x, padding + height);
  }
  ctx.stroke();

  const lineGradient = ctx.createLinearGradient(padding, 0, padding + width, 0);
  lineGradient.addColorStop(0, 'rgba(99,102,241,0.8)');
  lineGradient.addColorStop(1, 'rgba(59,130,246,0.8)');

  const fillGradient = ctx.createLinearGradient(0, padding, 0, padding + height);
  fillGradient.addColorStop(0, 'rgba(147,197,253,0.2)');
  fillGradient.addColorStop(1, 'rgba(255,255,255,0)');

  ctx.beginPath();
  ctx.moveTo(points[0].x, points[0].y);
  points.forEach(p => ctx.lineTo(p.x, p.y));
  ctx.lineTo(points[points.length - 1].x, padding + height);
  ctx.lineTo(points[0].x, padding + height);
  ctx.fillStyle = fillGradient;
  ctx.fill();

  ctx.beginPath();
  ctx.moveTo(points[0].x, points[0].y);
  points.forEach(p => ctx.lineTo(p.x, p.y));
  ctx.strokeStyle = lineGradient;
  ctx.lineWidth = 3;
  ctx.stroke();

  ctx.fillStyle = '#3b82f6';
  ctx.shadowColor = 'rgba(0,0,0,0.1)';
  ctx.shadowBlur = 5;
  points.forEach(p => {
    ctx.beginPath();
    ctx.arc(p.x, p.y, 4, 0, Math.PI * 2);
    ctx.fill();
  });
  ctx.shadowBlur = 0;

  ctx.font = '12px sans-serif';
  ctx.fillStyle = '#6b7280';
  ctx.textAlign = 'center';
  for (let i = 0; i <= 5; i++) {
    const xVal = props.chartMinX + ((props.chartMaxX - props.chartMinX) * i) / 5;
    ctx.fillText(xVal.toFixed(0), padding + (i * width) / 5, padding + height + 20);
  }
  ctx.textAlign = 'right';
  for (let i = 0; i <= 5; i++) {
    const yVal = 100 - (i * 100) / 5;
    ctx.fillText(yVal.toFixed(0), padding - 10, padding + (i * height) / 5 + 4);
  }
};

const updatePoint = (e: MouseEvent) => {
  if (draggingPoint.value === null || !chartCanvas.value) return;

  const rect = chartCanvas.value.getBoundingClientRect();
  const x = e.clientX - rect.left;
  const y = e.clientY - rect.top;

  const padding = 50;
  const width = chartCanvas.value.width - padding * 2;
  const height = chartCanvas.value.height - padding * 2;

  const isFirstPoint = draggingPoint.value === 0;
  const isLastPoint = draggingPoint.value === props.chartData.labels.length - 1;

  let xVal = ((x - padding) / width) * (props.chartMaxX - props.chartMinX) + props.chartMinX;
  const yVal = Math.max(0, Math.min(100, 100 - ((y - padding) / height) * 100));

  if (isFirstPoint) xVal = props.chartMinX;
  else if (isLastPoint) xVal = props.chartMaxX;

  props.chartData.labels[draggingPoint.value] = xVal;
  props.chartData.datasets[0].data[draggingPoint.value] = yVal;

  if (!isFirstPoint && !isLastPoint) {
    const combined = props.chartData.labels.map((x, i) => ({
      x,
      y: props.chartData.datasets[0].data[i]
    })).sort((a, b) => a.x - b.x);
    props.chartData.labels = combined.map(p => p.x);
    props.chartData.datasets[0].data = combined.map(p => p.y);
  }

  if (animationFrameId) cancelAnimationFrame(animationFrameId);
  animationFrameId = requestAnimationFrame(drawChart);
};

const getPointIndex = (e: MouseEvent): number | null => {
  if (!chartCanvas.value) return null;

  const rect = chartCanvas.value.getBoundingClientRect();
  const x = e.clientX - rect.left;
  const y = e.clientY - rect.top;

  const padding = 50;
  const width = chartCanvas.value.width - padding * 2;
  const height = chartCanvas.value.height - padding * 2;

  const index = props.chartData.labels.findIndex((label, i) => {
    const px = padding + ((label - props.chartMinX) / (props.chartMaxX - props.chartMinX)) * width;
    const py = padding + height - (props.chartData.datasets[0].data[i] / 100) * height;
    return Math.hypot(px - x, py - y) < 10;
  });
  return index === -1 ? null : index;
};

onMounted(() => {
  ctx = chartCanvas.value!.getContext('2d');
  updateCanvasSize();

  const canvas = chartCanvas.value!;

  const handleClick = (e: MouseEvent) => {
    if (getPointIndex(e) !== null) return;

    const rect = canvas.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;

    const padding = 50;
    const width = canvas.width - padding * 2;
    const height = canvas.height - padding * 2;

    const xVal = ((x - padding) / width) * (props.chartMaxX - props.chartMinX) + props.chartMinX;
    const yVal = 100 - ((y - padding) / height) * 100;

    const index = props.chartData.labels.findIndex(l => l > xVal);
    if (index === -1) {
      props.chartData.labels.push(xVal);
      props.chartData.datasets[0].data.push(yVal);
    } else {
      props.chartData.labels.splice(index, 0, xVal);
      props.chartData.datasets[0].data.splice(index, 0, yVal);
    }
    drawChart();
  };

  const handleMouseDown = (e: MouseEvent) => {
    draggingPoint.value = getPointIndex(e);
  };

  const handleMouseUp = () => {
    draggingPoint.value = null;
    if (animationFrameId) cancelAnimationFrame(animationFrameId);
  };

  canvas.addEventListener('click', handleClick);
  canvas.addEventListener('mousedown', handleMouseDown);
  canvas.addEventListener('mouseup', handleMouseUp);
  canvas.addEventListener('mousemove', updatePoint);

  // Наблюдатель за изменением размеров
  resizeObserver = new ResizeObserver(() => {
    updateCanvasSize();
  });
  if (container.value) {
    resizeObserver.observe(container.value);
  }
});

onUnmounted(() => {
  if (animationFrameId) cancelAnimationFrame(animationFrameId);
  if (resizeObserver) resizeObserver.disconnect();
  const canvas = chartCanvas.value;
  if (canvas) canvas.replaceWith(canvas.cloneNode(true));
});

watch(() => [props.chartData, props.chartMinX, props.chartMaxX], () => {
  if (animationFrameId) cancelAnimationFrame(animationFrameId);
  animationFrameId = requestAnimationFrame(drawChart);
}, { deep: true });
</script>
