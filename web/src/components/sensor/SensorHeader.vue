<template>
  <div class="entity-detail-header">
    <div>
      <h1>{{ sensor.type_name }}</h1>
      <p>Serial number: {{ sensor.serial_number }}</p>
    </div>

    <div v-if="parameters?.length" class="entity-detail-header__tags">
      <Tag
        v-for="param in parameters"
        :key="param.name"
        severity="secondary"
        :value="`${getLabel(param.name)} ${param.value?.toFixed(1) ?? '-'}${param.unit}`"
        icon="pi pi-chart-line"
        rounded
      />
    </div>
    <Tag v-else severity="secondary" value="No telemetry" rounded />
  </div>
</template>

<script setup lang="ts">
import Tag from 'primevue/tag';
import { PARAMETER_LABELS } from '@/types/sensor';
import type { Sensor, Parameter } from '@/types/sensor';

defineProps<{
  sensor: Sensor;
  parameters?: Parameter[];
}>();

const getLabel = (name: string) => PARAMETER_LABELS[name] || name;
</script>

<style scoped>
.entity-detail-header {
  align-items: center;
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  gap: 16px;
  justify-content: space-between;
  padding: 12px;
}

.entity-detail-header h1 {
  color: var(--app-text-strong);
  font-size: 1.1rem;
  font-weight: 780;
  line-height: 1.45rem;
  margin: 0;
}

.entity-detail-header p {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.72rem;
  margin: 4px 0 0;
}

.entity-detail-header__tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  justify-content: flex-end;
}

@media (max-width: 640px) {
  .entity-detail-header {
    align-items: flex-start;
    flex-direction: column;
  }

  .entity-detail-header__tags {
    justify-content: flex-start;
  }
}
</style>
