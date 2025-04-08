<template>
  <div class="grid md:grid-cols-2 gap-4">
    <div>
      <label class="text-gray-700 font-medium block mb-1">Параметр</label>
      <select
          v-model="selected"
          class="w-full px-4 py-2 rounded-lg border border-gray-300 bg-gray-100 text-gray-800 focus:outline-none focus:ring-2 focus:ring-blue-500"
      >
        <option
            v-for="param in availableParams"
            :key="param.name"
            :value="param.name"
        >
          {{ getLabel(param.name) }} ({{ param.unit }})
        </option>
      </select>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";

defineProps<{
  availableParams: { name: string; unit: string; min_value: number; max_value: number }[];
  getLabel: (name: string) => string;
}>();

const selected = ref<string>("");

watch(selected, (newVal) => {
  emit("update:modelValue", newVal);
});

const emit = defineEmits(["update:modelValue"]);
</script>