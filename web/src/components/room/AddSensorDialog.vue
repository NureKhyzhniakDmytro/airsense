<template>
  <Dialog
    v-model:visible="isOpen"
    modal
    header="Add sensor"
    :draggable="false"
    :style="{ width: 'min(26rem, calc(100vw - 2rem))' }"
  >
    <div class="entity-dialog-form">
      <div class="entity-dialog-field">
        <label class="entity-dialog-label" for="sensor-serial-number">Serial number</label>
        <InputText
          id="sensor-serial-number"
          v-model="serialNumber"
          class="w-full"
          maxlength="20"
          placeholder="Sensor serial number"
          :class="{ 'p-invalid': errorMessage }"
          @keyup.enter="submit"
        />
        <small v-if="errorMessage" class="p-error">{{ errorMessage }}</small>
      </div>
    </div>

    <template #footer>
      <div class="entity-dialog-actions">
        <Button type="button" label="Cancel" severity="secondary" @click="close" />
        <Button type="button" severity="primary" label="Add" :loading="isLoading" @click="submit" />
      </div>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import Dialog from "primevue/dialog";
import Button from "primevue/button";
import InputText from "primevue/inputtext";
import { useToast } from "primevue/usetoast";
import { addSensor } from "@/services/apiService";

const toast = useToast();
const isLoading = ref(false);
const serialNumber = ref("");
const errorMessage = ref("");

const props = defineProps<{
  modelValue: boolean;
  roomId: number;
}>();

const emit = defineEmits<{
  (e: "update:modelValue", value: boolean): void;
  (e: "added"): void;
}>();

const isOpen = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
});

const reset = () => {
  serialNumber.value = "";
  errorMessage.value = "";
};

const close = () => {
  isOpen.value = false;
  reset();
};

const submit = async () => {
  const value = serialNumber.value.trim();
  if (!value) {
    errorMessage.value = "Serial number is required.";
    return;
  }
  if (value.length !== 20) {
    errorMessage.value = "Serial number must be exactly 20 characters.";
    return;
  }

  try {
    isLoading.value = true;
    errorMessage.value = "";
    await addSensor(props.roomId, value);
    toast.add({ severity: 'success', summary: 'Success', detail: 'Sensor added', life: 3000 });
    emit('added');
    close();
  } catch (error) {
    errorMessage.value = "Failed to add sensor.";
  } finally {
    isLoading.value = false;
  }
};

watch(() => props.modelValue, (open) => {
  if (!open) reset();
});

defineExpose({
  isOpen
});
</script>
