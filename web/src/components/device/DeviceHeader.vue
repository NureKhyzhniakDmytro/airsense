<template>
    <div class="flex flex-row gap-4 items-center justify-between">
      <div class="flex justify-between mt-8 flex-col">
        <h1 v-if="device" class="text-3xl font-bold text-gray-800">Device №{{ device.id }}</h1>
        <p class="text-sm text-surface-500 mt-1">Serial number: {{ device.serial_number }}</p>
      </div>
      <div v-if="device.fan_speed" class="flex flex-row gap-4">
        <div class="flex flex-row items-center justify-center">
          <span class="material-symbols-outlined">mode_fan</span>
          <div class="text-xl font-semibold">{{ device.fan_speed.toFixed(0) }}%</div>
        </div>
      </div>
      <div v-else>
        <span class="text-gray-500">Offline</span>
      </div>
    </div>
  </template>
  
  <script setup lang="ts">
  import type { Device } from '@/services/apiService';
  
  defineProps<{
    device: Device;
  }>();
  </script> 