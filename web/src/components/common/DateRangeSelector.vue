<template>
  <div class="date-range-selector">
    <SelectButton
      :modelValue="selectedRangeOption"
      :options="rangeOptions"
      optionLabel="label"
      class="date-range-selector__presets"
      aria-label="Time range"
      @update:modelValue="setRangeOption"
    />

    <div v-if="showCustomFields" class="date-range-selector__dates">
      <FloatLabel variant="on">
        <DatePicker 
          :modelValue="props.from" 
          inputId="from_date" 
          showIcon 
          showTime 
          hourFormat="24" 
          iconDisplay="input"
          @update:modelValue="updateFromDate" 
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
          @update:modelValue="updateToDate" 
        />
        <label for="to_date">{{ toLabel }}</label>
      </FloatLabel>
    </div>

    <div
      v-if="intervalOptions?.length"
      class="date-range-selector__interval"
    >
      <span class="date-range-selector__label">Step</span>
      <Select
        :modelValue="props.interval"
        :options="intervalOptions"
        optionLabel="name"
        aria-label="Time resolution"
        @update:modelValue="(value) => emit('update:interval', value)"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import DatePicker from 'primevue/datepicker';
import FloatLabel from 'primevue/floatlabel';
import Select from 'primevue/select';
import SelectButton from 'primevue/selectbutton';
import { computed, ref } from 'vue';
import type { DateRangeProps, DateRangeEmits, DateRangePresetOption } from '@/types/date';

type DateRangeSelectorOption = DateRangePresetOption | {
  label: string;
  value: 'custom';
  custom: true;
};

const defaultPresetOptions: DateRangePresetOption[] = [
  { label: '1h', value: 'last-1h', amount: 1, unit: 'hour' },
  { label: '6h', value: 'last-6h', amount: 6, unit: 'hour' },
  { label: '24h', value: 'last-24h', amount: 24, unit: 'hour' },
  { label: '7d', value: 'last-7d', amount: 7, unit: 'day' },
  { label: '30d', value: 'last-30d', amount: 30, unit: 'day' },
];

const unitToMs: Record<DateRangePresetOption['unit'], number> = {
  minute: 60 * 1000,
  hour: 60 * 60 * 1000,
  day: 24 * 60 * 60 * 1000,
};

const props = withDefaults(defineProps<DateRangeProps>(), {
  fromLabel: 'From',
  toLabel: 'To',
});

const emit = defineEmits<DateRangeEmits>();
const customRangeOpen = ref(false);
const customRangeOption: DateRangeSelectorOption = {
  label: 'Custom',
  value: 'custom',
  custom: true,
};

const resolvedPresetOptions = computed(() => props.presetOptions ?? defaultPresetOptions);
const rangeOptions = computed<DateRangeSelectorOption[]>(() => [
  ...resolvedPresetOptions.value,
  customRangeOption,
]);

const selectedPreset = computed(() => {
  const rangeMs = props.to.getTime() - props.from.getTime();
  const nowDeltaMs = Math.abs(Date.now() - props.to.getTime());
  if (nowDeltaMs > 2 * 60 * 1000) return null;

  return resolvedPresetOptions.value.find((option) => (
    Math.abs(rangeMs - option.amount * unitToMs[option.unit]) < 2 * 60 * 1000
  )) ?? null;
});

const selectedRangeOption = computed<DateRangeSelectorOption>(() => {
  if (customRangeOpen.value) return customRangeOption;
  return selectedPreset.value ?? customRangeOption;
});

const showCustomFields = computed(() => selectedRangeOption.value.value === 'custom');

const isCustomOption = (value: DateRangeSelectorOption): value is Extract<DateRangeSelectorOption, { custom: true }> => {
  return value.value === 'custom';
};

const applyPreset = (value: DateRangePresetOption | null) => {
  if (!value) return;

  const to = new Date();
  const from = new Date(to.getTime() - value.amount * unitToMs[value.unit]);

  customRangeOpen.value = false;
  emit('update:from', from);
  emit('update:to', to);
};

const setRangeOption = (value: DateRangeSelectorOption | null) => {
  if (!value) return;
  if (isCustomOption(value)) {
    customRangeOpen.value = true;
    return;
  }

  applyPreset(value);
};

const updateFromDate = (value: Date | Date[] | null | undefined) => {
  if (!(value instanceof Date)) return;
  customRangeOpen.value = true;
  emit('update:from', value);
};

const updateToDate = (value: Date | Date[] | null | undefined) => {
  if (!(value instanceof Date)) return;
  customRangeOpen.value = true;
  emit('update:to', value);
};
</script> 

<style scoped>
.date-range-selector {
  align-items: center;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  justify-content: flex-end;
  min-width: 0;
}

.date-range-selector__dates {
  align-items: center;
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  min-width: 0;
}

.date-range-selector__label {
  align-items: center;
  color: var(--app-muted);
  display: inline-flex;
  font-family: var(--app-mono);
  font-size: 0.66rem;
  font-weight: 700;
  height: var(--app-control-height);
  line-height: 1;
  text-transform: uppercase;
}

.date-range-selector__separator {
  color: var(--app-muted);
  font-size: 0.75rem;
}

.date-range-selector__interval {
  align-items: center;
  display: inline-flex;
  flex: 0 0 auto;
  gap: 6px;
}

.date-range-selector__presets {
  flex: 0 0 auto;
}

.date-range-selector :deep(.p-datepicker-input) {
  height: var(--app-control-height);
  width: 9.75rem;
}

.date-range-selector :deep(.p-datepicker) {
  max-width: 9.75rem;
}

.date-range-selector :deep(.p-floatlabel label) {
  font-size: 0.68rem;
}

.date-range-selector :deep(.p-selectbutton) {
  align-items: center;
  background: var(--app-surface-soft);
  border: 1px solid transparent;
  border-radius: var(--app-radius);
  display: inline-flex;
  min-height: var(--app-control-height);
  padding: 2px;
}

.date-range-selector :deep(.p-togglebutton) {
  border: 1px solid transparent;
  border-radius: 5px;
  height: calc(var(--app-control-height) - 4px);
  min-height: calc(var(--app-control-height) - 4px);
  min-width: 58px;
  padding: 0 0.7rem;
}

.date-range-selector :deep(.p-togglebutton.p-togglebutton-checked) {
  background: var(--app-surface-raised);
  border-color: var(--app-border);
  color: var(--app-text-strong);
}

.date-range-selector__interval :deep(.p-select) {
  height: var(--app-control-height);
  min-height: var(--app-control-height);
  min-width: 5.25rem;
}

.date-range-selector__interval :deep(.p-select-label) {
  align-items: center;
  display: inline-flex;
  min-height: calc(var(--app-control-height) - 2px);
  padding-block: 0;
}

@media (max-width: 760px) {
  .date-range-selector,
  .date-range-selector__presets,
  .date-range-selector__dates {
    align-items: stretch;
    justify-content: flex-start;
    width: 100%;
  }

  .date-range-selector__interval,
  .date-range-selector :deep(.p-datepicker),
  .date-range-selector :deep(.p-inputwrapper),
  .date-range-selector :deep(.p-selectbutton) {
    width: 100%;
  }

  .date-range-selector__presets :deep(.p-togglebutton) {
    flex: 1 1 0;
  }

  .date-range-selector__separator {
    display: none;
  }
}
</style>
