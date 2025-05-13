<template>
  <div class="flex flex-row gap-4 items-center justify-between">
    <div class="flex justify-between mt-8 flex-col">
      <h1 v-if="sensor" class="text-3xl font-bold text-gray-800">{{ sensor.type_name }}</h1>
      <p class="text-sm text-surface-500 mt-1">Serial number: {{ sensor.serial_number }}</p>
    </div>
    <div v-if="parameters" class="flex flex-row gap-4">
      <div v-for="param in parameters" :key="param.name" class="flex flex-row items-center justify-center">
        <span class="material-symbols-outlined">{{ PARAMETER_ICONS[param.name] }}</span>
        <div class="text-xl font-semibold">{{ param.value?.toFixed(1) }}</div>
        <div class="text-xl font-semibold">{{ param.unit }}</div>
      </div>
    </div>
    <div v-else>
      <span class="text-gray-500">Offline</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { PARAMETER_ICONS } from '@/types/sensor';
import type { Sensor, Parameter } from '@/services/apiService';

defineProps<{
  sensor: Sensor;
  parameters?: Parameter[];
}>();
</script> 