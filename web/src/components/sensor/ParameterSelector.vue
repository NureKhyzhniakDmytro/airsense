<template>
  <div class="flex flex-wrap gap-2">
    <span
      v-for="type in types"
      :key="type"
      @click="$emit('select', type)"
      :class="[
        'px-3 py-1 text-sm rounded-full cursor-pointer transition-all',
        PARAMETER_STYLES[type] || 'bg-gray-100 text-gray-700',
        selectedParam === type ? 'ring-2 ring-blue-500 ring-offset-2' : ''
      ]"
    >
      {{ PARAMETER_LABELS[type] || type }}
    </span>
  </div>
</template>

<script setup lang="ts">
import { PARAMETER_LABELS, PARAMETER_STYLES } from '@/types/sensor';

defineProps<{
  types: string[];
  selectedParam: string;
}>();

defineEmits<{
  (e: 'select', type: string): void;
}>();
</script> 