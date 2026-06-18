<template>
  <Dialog v-model:visible="isOpen" modal header="Edit environment" :draggable="false" :style="{ width: 'min(26rem, calc(100vw - 2rem))' }">
    <Form v-slot="$form" :resolver @submit="onFormSubmit" class="entity-dialog-form">
      <div class="entity-dialog-field">
        <label class="entity-dialog-label" for="edit-environment-name">Environment name</label>
        <InputText id="edit-environment-name" name="name" type="text" placeholder="Name" fluid :default-value="env?.name" />
        <Message v-if="$form.name?.invalid" severity="error" size="small" variant="simple">{{ $form.name.error?.message }}</Message>
      </div>
      <div class="entity-dialog-field">
        <label class="entity-dialog-label" for="edit-environment-icon">Environment type</label>
        <Select
          id="edit-environment-icon"
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
      <Message v-if="isError" severity="error" :life="3000">An error occurred while updating the environment</Message>
      <div class="entity-dialog-actions">
        <Button type="button" label="Cancel" severity="secondary" @click="isOpen = false" />
        <Button type="submit" label="Save" severity="primary" :loading="isLoading" />
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
import { computed, onMounted, ref, watch } from "vue";
import { updateEnvironment} from "@/services/apiService";
import type { FormResolverOptions, FormSubmitEvent } from "@primevue/forms/form";
import { useEnvironmentStore } from "@/store/environmentStore";
import type { Environment } from "@/types/environment";
import PlaceIcon from "@/components/common/PlaceIcon.vue";
import { getPlaceIconOption, PLACE_ICON_OPTIONS } from "@/config/placeOptions";

const isLoading = ref(false);
const isError = ref(false);
const env = ref<Environment | null>(null);
const selectedIcon = ref("building");
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
    await updateEnvironment(props.envId, { name: values.name.trim(), icon: selectedIcon.value });
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
  selectedIcon.value = env.value.icon || "building";
});

watch(isOpen, async (value) => {
  if (!value) return;
  env.value = await environmentStore.fetchEnvironment(props.envId, true);
  selectedIcon.value = env.value.icon || "building";
});
</script>
