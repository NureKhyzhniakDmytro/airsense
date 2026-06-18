<template>
    <Dialog v-model:visible="isOpen" modal header="Edit room" :draggable="false" :style="{ width: 'min(26rem, calc(100vw - 2rem))' }">
      <Form :key="formKey" v-slot="$form" :resolver @submit="onFormSubmit" class="entity-dialog-form">
        <div class="entity-dialog-field">
          <label class="entity-dialog-label" for="edit-room-name">Room name</label>
          <InputText
            id="edit-room-name"
            v-model="roomName"
            name="name"
            type="text"
            placeholder="Name"
            maxlength="20"
            fluid
          />
          <Message v-if="$form.name?.invalid" severity="error" size="small" variant="simple">{{ $form.name.error?.message }}</Message>
        </div>
        <div class="entity-dialog-field">
          <label class="entity-dialog-label" for="edit-room-icon">Room type</label>
          <Select
            id="edit-room-icon"
            v-model="selectedIcon"
            :options="ROOM_ICON_OPTIONS"
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
        <Message v-if="isError" severity="error" :life="3000">An error occurred while updating the room</Message>
        <div class="entity-dialog-actions">
          <Button type="button" label="Cancel" severity="secondary" @click="isOpen = false" />
          <Button
            type="submit"
            label="Save"
            severity="primary"
            :disabled="!roomName.trim()"
            :loading="isLoading"
          />
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
  import { getRoom, updateRoom} from "@/services/apiService";
  import type { FormResolverOptions, FormSubmitEvent } from "@primevue/forms/form";
  import type { Room } from "@/types/room";
  import PlaceIcon from "@/components/common/PlaceIcon.vue";
  import { getPlaceIconOption, ROOM_ICON_OPTIONS } from "@/config/placeOptions";
  
  const isLoading = ref(false);
  const isError = ref(false);
  const room = ref<Room | null>(null);
  const roomName = ref("");
  const selectedIcon = ref("room");
  const formKey = ref(0);
  const props = defineProps<{
    modelValue: boolean;
    envId: number;
    roomId: number;
  }>();
    
  const emit = defineEmits(['update:modelValue', 'refresh']);
  
  const isOpen = computed({
    get: () => props.modelValue,
    set: (val) => emit('update:modelValue', val)
  });
  
  defineExpose({
    isOpen
  });
  
  const resolver = ({ values }: FormResolverOptions) => {
    const name = String(values.name ?? roomName.value ?? "").trim();
    const errors: Record<string, Record<string, string>[]> = {};
  
    if (!name) {
      errors.name = [{ message: 'Name is required.' }];
    } else if (name.length < 3) {
      errors.name = [{ type: 'minimum', message: 'Name must be at least 3 characters long.' }];
    } else if (name.length > 20) {
      errors.name = [{ type: 'maximum', message: 'Name must be 20 characters or fewer.' }];
    }
  
    return {
      values: { ...values, name },
      errors
    };
  }
  
  const onFormSubmit = ({ valid, values }: FormSubmitEvent) => {
    if (valid) {
      save(values).then(
          isSuccess => {
            if (isSuccess) {
              isOpen.value = false;
            }
      });
    }
  };
  
  const save = async (values: Record<string, any>): Promise<boolean> => {
    isLoading.value = true;
    isError.value = false;
    const name = String(values.name ?? roomName.value ?? "").trim();
  
    try {
      await updateRoom(props.envId, props.roomId, { name, icon: selectedIcon.value });
      emit('refresh');
      return true;
    } catch (error) {
      console.error("Failed to update room:", error);
      isError.value = true;
      setTimeout(() => {
        isError.value = false;
      }, 3500);
      return false;
    } finally {
      isLoading.value = false;
    }
  };
  
  const loadRoom = async () => {
    isError.value = false;
    room.value = await getRoom(props.envId, props.roomId);
    roomName.value = room.value.name || "";
    selectedIcon.value = room.value.icon || "room";
    formKey.value += 1;
  };
  
  onMounted(loadRoom);

  watch(isOpen, async (value) => {
    if (!value) return;
    await loadRoom();
  });
  </script>
  
