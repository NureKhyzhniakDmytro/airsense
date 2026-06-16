<template>
  <div class="entity-detail-header">
    <div>
      <h1>Device #{{ device.id }}</h1>
      <p>Serial number: {{ device.serial_number }}</p>
    </div>

    <Tag
      v-if="device.fan_speed !== null && device.fan_speed !== undefined"
      severity="info"
      :value="`${device.fan_speed.toFixed(0)}% fan`"
      icon="pi pi-slack"
      rounded
    />
    <Tag v-else severity="secondary" value="No telemetry" rounded />
  </div>
</template>

<script setup lang="ts">
import Tag from 'primevue/tag';
import type { Device } from '@/types/sensor';

defineProps<{
  device: Device;
}>();
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

@media (max-width: 640px) {
  .entity-detail-header {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
