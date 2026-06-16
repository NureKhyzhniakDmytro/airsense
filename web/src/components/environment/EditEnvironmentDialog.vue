<template>
  <Dialog v-model:visible="isOpen" modal header="Edit Environment" :draggable="false" :style="{ width: '25rem' }">
    <Form v-slot="$form" :resolver @submit="onFormSubmit" class="flex flex-col gap-4 w-full">
      <div class="flex flex-col gap-1">
        <InputText name="name" type="text" placeholder="Name" fluid :default-value="env?.name" />
        <Message v-if="$form.name?.invalid" severity="error" size="small" variant="simple">{{ $form.name.error?.message }}</Message>
      </div>
      <Message v-if="isError" severity="error" :life="3000">An error occurred while updating the environment</Message>
      <div class="flex justify-end gap-2">
        <Button type="button" label="Cancel" severity="secondary" @click="isOpen = false" />
        <Button type="submit" label="Submit" severity="primary" :loading="isLoading" />
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
import { computed, onMounted, ref } from "vue";
import { updateEnvironment} from "@/services/apiService";
import { FormResolverOptions, FormSubmitEvent } from "@primevue/forms/form";
import { useEnvironmentStore } from "@/store/environmentStore";
import { Environment } from "@/types/environment";

const isLoading = ref(false);
const isError = ref(false);
const env = ref<Environment | null>(null);
const props = defineProps<{
  modelValue: boolean;
  envId: number;
}>();

const environmentStore = useEnvironmentStore();

const emit = defineEmits(['update:modelValue', 'refresh']);

const isOpen = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
});

defineExpose({
  isOpen
});

const resolver = ({ values }: FormResolverOptions) => {
  const errors: Record<string, Record<string, string>[]> = {
    name: []
  };

  if (!values.name) {
    errors.name.push({ message: 'Name is required.' });
  }

  if (values.name?.length < 3) {
    errors.name.push({ type: 'minimum', message: 'Name must be at least 3 characters long.' });
  }

  return {
    values,
    errors
  };
}

const onFormSubmit = ({ valid, values }: FormSubmitEvent) => {
  if (valid) {
    create(values).then(
        isSuccess => {
          if (isSuccess) {
            isOpen.value = false;
          }
        });
  }
};

const create = async (values: Record<string, any>): Promise<boolean> => {
  isLoading.value = true;

  try {
    await updateEnvironment(props.envId, { name: values.name.trim() });
    emit('refresh');
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

onMounted(async () => {
  env.value = await environmentStore.fetchEnvironment(props.envId);
});
</script>
