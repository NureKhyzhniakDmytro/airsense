<template>
  <div class="date-range-selector">
    <div class="date-range-selector__dates">
      <FloatLabel variant="on">
        <DatePicker 
          :modelValue="props.from" 
          inputId="from_date" 
          showIcon 
          showTime 
          hourFormat="24" 
          iconDisplay="input"
          @update:modelValue="(value) => value instanceof Date && emit('update:from', value)" 
        />
        <label for="from_date">{{ fromLabel }}</label>
      </FloatLabel>
      <i class="pi pi-minus date-range-selector__separator"></i>
      <FloatLabel variant="on">
        <DatePicker 
          :modelValue="props.to" 
          inputId="to_date" 
          showIcon 
          showTime 
          hourFormat="24" 
          iconDisplay="input"
          @update:modelValue="(value) => value instanceof Date && emit('update:to', value)" 
        />
        <label for="to_date">{{ toLabel }}</label>
      </FloatLabel>
    </div>
    <SelectButton 
      v-if="intervalOptions?.length"
      :modelValue="props.interval" 
      :options="intervalOptions" 
      optionLabel="name"
      class="date-range-selector__interval"
      @update:modelValue="(value) => emit('update:interval', value)" 
    />
  </div>
</template>

<script setup lang="ts">
import DatePicker from 'primevue/datepicker';
import FloatLabel from 'primevue/floatlabel';
import SelectButton from 'primevue/selectbutton';
import type { DateRangeProps, DateRangeEmits } from '@/types/date';

const props = withDefaults(defineProps<DateRangeProps>(), {
  fromLabel: 'From',
  toLabel: 'To'
});

const emit = defineEmits<DateRangeEmits>();
</script> 

<style scoped>
.date-range-selector {
  align-items: center;
  display: flex;
  flex-wrap: wrap;
  gap: var(--app-gap-sm);
  justify-content: flex-end;
  min-width: 0;
}

.date-range-selector__dates {
  align-items: center;
  display: flex;
  flex-wrap: wrap;
  gap: var(--app-gap-sm);
  min-width: 0;
}

.date-range-selector__separator {
  color: var(--app-muted);
  font-size: 0.875rem;
}

.date-range-selector__interval {
  flex: 0 0 auto;
}

.date-range-selector :deep(.p-datepicker-input) {
  height: var(--app-control-height);
  width: 10.5rem;
}

.date-range-selector :deep(.p-datepicker) {
  max-width: 10.5rem;
}

.date-range-selector :deep(.p-floatlabel label) {
  font-size: 0.72rem;
}

.date-range-selector :deep(.p-selectbutton) {
  display: inline-flex;
}

.date-range-selector :deep(.p-togglebutton) {
  min-height: var(--app-control-height);
  padding: 0.35rem 0.65rem;
}

@media (max-width: 760px) {
  .date-range-selector,
  .date-range-selector__dates {
    align-items: stretch;
    justify-content: flex-start;
    width: 100%;
  }

  .date-range-selector :deep(.p-datepicker),
  .date-range-selector :deep(.p-inputwrapper),
  .date-range-selector :deep(.p-selectbutton) {
    width: 100%;
  }

  .date-range-selector__separator {
    display: none;
  }
}
</style>
