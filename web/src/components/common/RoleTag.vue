<template>
  <Tag :severity="severity" :value="label" rounded />
</template>

<script setup lang="ts">
import { computed } from 'vue';
import Tag from 'primevue/tag';

const props = defineProps<{
  role?: string | null;
}>();

const label = computed(() => {
  const value = props.role || 'member';
  return value.charAt(0).toUpperCase() + value.slice(1);
});

const severity = computed(() => {
  switch (props.role) {
    case 'owner':
      return 'contrast';
    case 'admin':
      return 'warn';
    default:
      return 'success';
  }
});
</script>
