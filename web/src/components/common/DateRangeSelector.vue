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
        <label for="from_date">{{ fromLabel }}</label>
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
        <label for="to_date">{{ toLabel }}</label>
      </FloatLabel>
    </div>
    <SelectButton 
      v-if="intervalOptions?.length"
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
import type { DateRangeProps, DateRangeEmits } from '@/types/date';

const props = withDefaults(defineProps<DateRangeProps>(), {
  fromLabel: 'From',
  toLabel: 'To'
});

const emit = defineEmits<DateRangeEmits>();
</script> 