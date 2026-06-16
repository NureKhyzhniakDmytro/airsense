<template>
  <SelectButton
    :model-value="selectedParam"
    :options="options"
    option-label="label"
    option-value="value"
    class="parameter-selector"
    @update:modelValue="value => value && $emit('select', value)"
  />
</template>

<script setup lang="ts">
import { computed } from 'vue';
import SelectButton from 'primevue/selectbutton';
import { PARAMETER_LABELS } from '@/types/sensor';

const props = defineProps<{
  types: string[];
  selectedParam: string;
}>();

defineEmits<{
  (e: 'select', type: string): void;
}>();

const options = computed(() => props.types.map(type => ({
  label: PARAMETER_LABELS[type] || type,
  value: type,
})));
</script>

<style scoped>
.parameter-selector {
  max-width: 100%;
}

.parameter-selector :deep(.p-togglebutton) {
  min-height: var(--app-control-height);
  padding: 0.35rem 0.7rem;
}

@media (max-width: 760px) {
  .parameter-selector {
    width: 100%;
  }
}
</style>
