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
import { computed, ref } from "vue";
import Dialog from "primevue/dialog";
import Button from "primevue/button";
import InputText from "primevue/inputtext";
import Message from "primevue/message";
import FloatLabel from "primevue/floatlabel";
import { Form } from "@primevue/forms";
import { FormResolverOptions, FormSubmitEvent } from "@primevue/forms/form";
import { useToast } from "primevue/usetoast";
import api from "@/api";

const toast = useToast();
const isLoading = ref(false);
const isError = ref(false);

const props = defineProps({
  modelValue: Boolean,
  roomId: {
    type: Number,
    required: true
  }
});

const emit = defineEmits(['update:modelValue', 'added']);

const isOpen = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
});

const resolver = ({ values }: FormResolverOptions) => {
  const errors: Record<string, Record<string, string>[]> = {
    serialNumber: []
  };

  if (!values.serialNumber) {
    errors.serialNumber.push({ message: 'Serial number is required' });
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

const create = async (values: Record<string, any>): Promise<boolean> => {
  try {
    isLoading.value = true;
    try {
      await api.post(`/room/${props.roomId}/sensor`, {
        serial_number: values.serialNumber.trim()
      });
      toast.add({ severity: 'success', summary: 'Success', detail: 'Sensor added', life: 3000 });
      return true;
    } catch {
      return false;
    }
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
