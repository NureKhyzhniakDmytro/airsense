<template>
  <div class="flex flex-row gap-4 items-center">
    <div class="flex flex-row gap-4 items-center">
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
        <label for="from_date">From</label>
      </FloatLabel>
      <i class="pi pi-minus my-auto" style="font-size: 1rem"></i>
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
        <label for="to_date">To</label>
      </FloatLabel>
    </div>
    <SelectButton 
      :modelValue="props.interval" 
      :options="intervalOptions" 
      optionLabel="name"
      @update:modelValue="(value) => emit('update:interval', value)" 
    />
  </div>
</template>

<script setup lang="ts">
import DatePicker from 'primevue/datepicker';
import FloatLabel from 'primevue/floatlabel';
import SelectButton from 'primevue/selectbutton';
import type { ChartOptions } from '@/types/sensor';

const props = defineProps<{
  from: Date;
  to: Date;
  interval: ChartOptions;
  intervalOptions: ChartOptions[];
}>();

const emit = defineEmits<{
  (e: 'update:from', value: Date): void;
  (e: 'update:to', value: Date): void;
  (e: 'update:interval', value: ChartOptions): void;
}>();
</script> 