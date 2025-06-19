<template>
  <Dialog v-model:visible="isOpen" modal header="Add Sensor" :draggable="false" :style="{ width: '25rem' }">
    <Form v-slot="$form" :resolver="resolver" @submit="onFormSubmit" class="flex flex-col gap-4 w-full">
      <div class="flex flex-col gap-1">
        <InputText 
          name="serialNumber" 
          type="text" 
          class="w-full" 
          placeholder="Serial number"
          :class="{ 'p-invalid': $form.serialNumber?.invalid }"
        />
        <Message v-if="$form.serialNumber?.invalid" severity="error" size="small" variant="simple">
          {{ $form.serialNumber.error?.message }}
        </Message>
      </div>

      <Message v-if="isError" severity="error" :life="3000">
        Error adding sensor
      </Message>

      <div class="flex justify-end gap-2">
        <Button type="button" label="Cancel" severity="secondary" @click="isOpen = false" />
        <Button type="submit" severity="primary" label="Add" :loading="isLoading" />
      </div>
    </Form>
  </Dialog>
</template>

<script setup lang="ts">
import { computed, ref, onMounted } from "vue";
import Dialog from "primevue/dialog";
import Button from "primevue/button";
import InputText from "primevue/inputtext";
import Message from "primevue/message";
import FloatLabel from "primevue/floatlabel";
import { Form } from "@primevue/forms";
import type { FormResolverOptions, FormSubmitEvent, FormValues } from "@/types/form";
import { useToast } from "primevue/usetoast";
import { addSensor } from "@/services/apiService";

const toast = useToast();
const isLoading = ref(false);
const isError = ref(false);

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

const resolver = ({ values }: FormResolverOptions) => {
  const errors: Record<string, Array<{ message: string }>> = {};

  if (!values.serialNumber) {
    errors.serialNumber = [{ message: "Serial number is required" }];
  }

  return {
    values,
    errors
  };
};

const onFormSubmit = ({ valid, values }: FormSubmitEvent) => {
  if (valid) {
    create(values).then(
      isSuccess => {
        if (isSuccess) {
          isOpen.value = false;
          emit('added');
        }
      }
    );
  }
};

const create = async (values: FormValues): Promise<boolean> => {
  try {
    isLoading.value = true;
    await addSensor(props.roomId, values.serialNumber.trim());
    toast.add({ severity: 'success', summary: 'Success', detail: 'Sensor added', life: 3000 });
    return true;
  } catch (error) {
    isError.value = true;
    setTimeout(() => {
      isError.value = false;
    }, 3500);
    return false;
  } finally {
    isLoading.value = false;
  }
};

defineExpose({
  isOpen
});
</script>
