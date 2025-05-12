<template>
  <Dialog
    :visible="modelValue"
    @update:visible="emit('update:modelValue', $event)"
    modal 
    :draggable="false" 
    :style="{ width: '25rem' }"
    header="Add Device"
  >
    <div class="flex flex-col gap-4">
      <div class="flex flex-col gap-2">
        <InputText
          id="serialNumber"
          v-model="serialNumber"
          placeholder="Serial number"
          :class="{ 'p-invalid': errorMessage }"
          @keyup.enter="submit"
        />
        <small v-if="errorMessage" class="p-error">{{ errorMessage }}</small>
      </div>
    </div>

    <template #footer>
      <div class="flex justify-end gap-2">
        <Button label="Cancel" @click="close" text />
        <Button label="Add" @click="submit" :loading="isLoading" />
      </div>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";
import api from "@/api";
import Dialog from 'primevue/dialog';
import Button from 'primevue/button';
import InputText from 'primevue/inputtext';

const props = defineProps<{
  modelValue: boolean;
  roomId: number;
}>();

const emit = defineEmits(['update:modelValue', 'added']);

const serialNumber = ref("");
const isLoading = ref(false);
const errorMessage = ref("");

const close = () => {
  emit('update:modelValue', false);
  serialNumber.value = "";
  errorMessage.value = "";
};

const submit = async () => {
  if (!serialNumber.value.trim()) {
    errorMessage.value = "Please enter a serial number.";
    return;
  }

  isLoading.value = true;
  errorMessage.value = "";

  try {
    await api.post(`/room/${props.roomId}/device`, {
      serial_number: serialNumber.value.trim(),
    });

    emit("added");
    close();
  } catch (error) {
    errorMessage.value = "Failed to add device.";
    console.error("Error adding device:", error);
  } finally {
    isLoading.value = false;
  }
};

watch(() => props.modelValue, (newValue) => {
  if (!newValue) {
    close();
  }
});
</script> 