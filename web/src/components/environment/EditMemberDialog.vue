<template>
  <Dialog v-model:visible="isOpen" modal header="Edit member" :draggable="false" :style="{ width: 'min(26rem, calc(100vw - 2rem))' }">
    <Form v-slot="$form" :resolver @submit="onFormSubmit" class="entity-dialog-form">
      <div class="entity-dialog-field">
        <label class="entity-dialog-label" for="member-role">Role</label>
        <Select inputId="member-role" v-model="selectedValue" name="role" :options="roles" optionLabel="name" placeholder="Select a role" fluid />
        <Message v-if="$form.role?.invalid" severity="error" size="small" variant="simple">{{ $form.role.error?.message }}</Message>
      </div>
      <Message v-if="isError" severity="error" :life="3000">An error occurred while updating the member</Message>
      <div class="entity-dialog-actions">
        <Button type="button" label="Cancel" severity="secondary" @click="isOpen = false" />
        <Button type="submit" severity="primary" label="Save" :loading="isLoading" />
      </div>
    </Form>
  </Dialog>
</template>
  
<script setup lang="ts">
import Dialog from "primevue/dialog";
import Button from "primevue/button";
import Select from 'primevue/select';
import Message from 'primevue/message';
import { Form } from '@primevue/forms';
import { computed, onMounted, ref } from "vue";
import { changeUserRole } from "@/services/apiService";
import type { FormResolverOptions, FormSubmitEvent } from "@primevue/forms/form";
import type { User } from "@/types/user";

const roles = ref([
  { name: 'Admin', value: 'admin' },
  { name: 'Member', value: 'user' },
]);
const isLoading = ref(false);
const isError = ref(false);
const props = defineProps({
  modelValue: Boolean,
  envId: {
    type: Number,
    required: true
  },
  member: {
    type: Object as () => User,
    required: true
  }
});
const selectedValue = ref();

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

  if (!selectedValue.value) {
    errors.role = [{ message: 'Role is required' }]
  } else if (selectedValue.value.value === props.member.role) {
    errors.role = [{ message: 'The selected role is the same as the current role' }]
  }

  return {
    values,
    errors
  };
}

const onFormSubmit = ({ valid, values }: FormSubmitEvent) => {
  if (valid) {
    update(values).then(
      isSuccess => {
        if (isSuccess) {
          isOpen.value = false;
        }
      }
    );
  }
};

const update = async (values: Record<string, any>): Promise<boolean> => {
  isLoading.value = true;

  try {
    await changeUserRole(props.envId, props.member.id, values.role.value) 
    props.member.role = values.role.value;
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

onMounted(() => {
  selectedValue.value = roles.value.find(role => role.value === props.member.role);
});
</script>
