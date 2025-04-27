<template>
  <Dialog v-model:visible="isOpen" modal header="Invite member" :draggable="false" :style="{ width: '25rem' }">
    <Form v-slot="$form" :resolver @submit="onFormSubmit" class="flex flex-col gap-4 w-full">
      <div class="flex flex-col gap-1">
        <InputText name="email" type="text" placeholder="Email" fluid />
        <Message v-if="$form.email?.invalid" severity="error" size="small" variant="simple">{{ $form.email.error?.message }}</Message>
      </div>
      <Message v-if="isError" severity="error" :life="3000">An error occurred while inviting a participant</Message>
      <div class="flex justify-end gap-2">
        <Button type="button" label="Cancel" severity="secondary" @click="isOpen = false" />
        <Button type="submit" severity="primary" label="Submit" :loading="isLoading" />
      </div>
    </Form>
  </Dialog>
</template>

<script setup lang="ts">
import Dialog from "primevue/dialog";
import Button from "primevue/button";
import InputText from "primevue/inputtext";
import Message from 'primevue/message';
import { Form } from '@primevue/forms';
import { computed, ref } from "vue";
import { addUserToEnvByEmail } from "@/services/apiService";
import { FormResolverOptions, FormSubmitEvent } from "@primevue/forms/form";
import { useToast } from "primevue/usetoast";

const toast = useToast();
const isLoading = ref(false);
const isError = ref(false);
const props = defineProps({
  modelValue: Boolean,
  envId: {
    type: Number,
    required: true
  }
});

const emit = defineEmits(['update:modelValue']);

const isOpen = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
});

defineExpose({
  isOpen
});

const resolver = ({ values }: FormResolverOptions) => {
  const errors: Record<string, Record<string, string>[]> = {
    email: []
  };

  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (!values.email) {
    errors.email.push({ message: 'Email is required.' });
  } else if (!emailRegex.test(values.email)) {
    errors.email.push({ type: 'format', message: 'Email is invalid.' });
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
        }
      }
    );
  }
};

const create = async (values: Record<string, any>): Promise<boolean> => {
  try {
    isLoading.value = true;
    try {
      await addUserToEnvByEmail(props.envId, values.email.trim());
      toast.add({ severity: 'success', summary: 'Success', detail: 'User invited', life: 3000 });
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
</script>