<template>
  <Dialog v-model:visible="isOpen" modal header="Create room" :draggable="false" :style="{ width: 'min(26rem, calc(100vw - 2rem))' }">
    <Form v-slot="$form" :resolver @submit="onFormSubmit" class="entity-dialog-form">
      <div class="entity-dialog-field">
        <label class="entity-dialog-label" for="room-name">Room name</label>
        <InputText id="room-name" name="name" type="text" placeholder="Lab A" maxlength="20" fluid />
        <Message v-if="$form.name?.invalid" severity="error" size="small" variant="simple">{{ $form.name.error?.message }}</Message>
      </div>
      <Message v-if="isError" severity="error" :life="3000">An error occurred while creating the room</Message>
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
import { Form } from '@primevue/forms';
import { computed, ref } from "vue";
import { createRoom } from "@/services/apiService";
import type { FormResolverOptions, FormSubmitEvent } from "@primevue/forms/form";
import { useRouter } from "vue-router";

const router = useRouter();
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
  const errors: Record<string, Record<string, string>[]> = {};

  if (!values.name) {
    errors.name = [{ message: 'Name is required.' }];
  } else if (values.name.length < 3) {
    errors.name = [{ type: 'minimum', message: 'Name must be at least 3 characters long.' }];
  } else if (values.name.length > 20) {
    errors.name = [{ type: 'maximum', message: 'Name must be 20 characters or fewer.' }];
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
  try {
    isLoading.value = true;
    const newRoom = await createRoom(props.envId, values.name.trim());
    await router.push({
      name: 'room',
      params: {
        envId: props.envId,
        roomId: newRoom.id,
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
