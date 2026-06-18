<template>
  <Dialog v-model:visible="isOpen" modal header="Create environment" :draggable="false" :style="{ width: 'min(26rem, calc(100vw - 2rem))' }">
    <Form v-slot="$form" :resolver @submit="onFormSubmit" class="entity-dialog-form">
      <div class="entity-dialog-field">
        <label class="entity-dialog-label" for="environment-name">Environment name</label>
        <InputText id="environment-name" name="name" type="text" placeholder="AirSense Lab" fluid autofocus />
        <Message v-if="$form.name?.invalid" severity="error" size="small" variant="simple">{{ $form.name.error?.message }}</Message>
      </div>
      <div class="entity-dialog-field">
        <label class="entity-dialog-label" for="environment-icon">Environment type</label>
        <Select
          id="environment-icon"
          v-model="selectedIcon"
          :options="PLACE_ICON_OPTIONS"
          option-label="label"
          option-value="value"
          fluid
        >
          <template #value="{ value }">
            <div class="place-select-value">
              <PlaceIcon :name="value || selectedIcon" size="sm" />
              <span>{{ getPlaceIconOption(value || selectedIcon).label }}</span>
            </div>
          </template>
          <template #option="{ option }">
            <div class="place-select-option">
              <PlaceIcon :name="option.value" size="sm" />
              <span>
                <strong>{{ option.label }}</strong>
                <small>{{ option.description }}</small>
              </span>
            </div>
          </template>
        </Select>
      </div>
      <Message v-if="isError" severity="error" :life="3000">An error occurred while creating the environment</Message>
      <div class="entity-dialog-actions">
        <Button type="button" label="Cancel" severity="secondary" @click="isOpen = false" />
        <Button type="submit" severity="primary" label="Create" :loading="isLoading" />
      </div>
    </Form>
  </Dialog>
</template>

<script setup lang="ts">
import Dialog from "primevue/dialog";
import Button from "primevue/button";
import InputText from "primevue/inputtext";
import Message from 'primevue/message';
import Select from "primevue/select";
import { Form } from '@primevue/forms';
import { computed, ref } from "vue";
import {createEnvironment} from "@/services/apiService";
import type { FormResolverOptions, FormSubmitEvent } from "@primevue/forms/form";
import { useRouter } from "vue-router";
import PlaceIcon from "@/components/common/PlaceIcon.vue";
import { getPlaceIconOption, PLACE_ICON_OPTIONS } from "@/config/placeOptions";

const router = useRouter();
const isLoading = ref(false);
const isError = ref(false);
const selectedIcon = ref("building");
const props = defineProps<{
  modelValue: boolean;
}>();

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
  // errorMessage.value = "";

  try {
    const newEnv = await createEnvironment({ name: values.name.trim(), icon: selectedIcon.value });
    await router.push({
      name: 'environment',
      params: {
        envId: newEnv.id,
      }
    })
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
</script>
