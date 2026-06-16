<template>
  <Dialog
    :visible="modelValue"
    @update:visible="emit('update:modelValue', $event)"
    modal 
    :draggable="false" 
    :style="{ width: 'min(26rem, calc(100vw - 2rem))' }"
    header="Add device"
  >
    <div class="entity-dialog-form">
      <div class="entity-dialog-field">
        <label class="entity-dialog-label" for="device-serial-number">Serial number</label>
        <InputText
          id="device-serial-number"
          v-model="serialNumber"
          class="w-full"
          maxlength="20"
          placeholder="Serial number"
          :class="{ 'p-invalid': errorMessage }"
          @keyup.enter="submit"
        />
        <small v-if="errorMessage" class="p-error">{{ errorMessage }}</small>
      </div>
    </div>

    <template #footer>
      <div class="entity-dialog-actions">
        <Button type="button" label="Cancel" severity="secondary" @click="close" />
        <Button type="button" label="Add" severity="primary" :loading="isLoading" @click="submit" />
      </div>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";
import { addDevice } from "@/services/apiService";
import Dialog from "primevue/dialog";
import Button from "primevue/button";
import InputText from "primevue/inputtext";

const props = defineProps<{
  modelValue: boolean;
  roomId: number;
}>();

const emit = defineEmits(["update:modelValue", "added"]);

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
  if (serialNumber.value.trim().length > 20) {
    errorMessage.value = "Serial number must be 20 characters or fewer.";
    return;
  }

  isLoading.value = true;
  errorMessage.value = "";

  try {
    await addDevice(props.roomId, serialNumber.value.trim());
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
