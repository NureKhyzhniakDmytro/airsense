<template>
  <div class="flex min-w-0 w-full flex-col flex-grow"
    :class="{ 'place-content-center': environment?.role === 'user' }"
  >
    <div
      v-if="environment?.role === 'user'"
      class="flex flex-col flex-grow justify-center items-center max-w-lg self-center"
    >
      <EmptyState
        title="Room access is restricted"
        description="Please contact your administrator if you believe this is an error."
        icon="pi pi-lock"
      />
    </div>

    <div v-else class="room-page">
      <section class="room-panel">
        <header class="room-panel__header">
          <div class="room-panel__title">
            <PlaceIcon :name="room?.icon" size="md" />
            <div class="room-panel__copy">
              <span class="room-panel__eyebrow">Room</span>
              <h1>{{ room?.name || 'Room' }}</h1>
            </div>
          </div>

          <div class="room-panel__actions">
            <Skeleton v-if="isLoading" width="8rem" height="2rem" />
            <template v-else>
              <Button label="Edit" icon="pi pi-pencil" severity="secondary" variant="text" @click="editRoomDialog = true" />
              <Button label="Delete" icon="pi pi-trash" severity="danger" variant="text" @click="deleteRoom" />
            </template>
          </div>
        </header>

        <div class="room-panel__content">
          <NuxtPage />
        </div>
      </section>
    </div>

    <edit-room-dialog v-model="editRoomDialog" :envId="envId" :roomId="roomId" @refresh="refreshRoom" />
  </div>
</template>

<script setup lang="ts">
definePageMeta({ name: 'room', layout: 'dashboard', requiresAuth: true })

import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useEnvironmentStore } from "@/store/environmentStore";
import { getRoom, removeRoom as deleteRoomApi } from "@/services/apiService";
import Button from 'primevue/button';
import Skeleton from 'primevue/skeleton';
import EditRoomDialog from "@/components/room/EditRoomDialog.vue";
import EmptyState from "@/components/common/EmptyState.vue";
import PlaceIcon from "@/components/common/PlaceIcon.vue";
import { useConfirm } from "primevue/useconfirm";
import { useToast } from "primevue/usetoast";

const route = useRoute();
const router = useRouter();
const envId = Number(route.params.envId);
const roomId = Number(route.params.roomId);

const environmentStore = useEnvironmentStore();
const editRoomDialog = ref(false);
const isRefreshing = ref(false);
const confirm = useConfirm();
const toast = useToast();

if (route.name === "room") {
  await navigateTo({ name: "room-parameters", params: { envId, roomId } }, { replace: true });
}

const { data: environmentData, pending: environmentPending } = await useAsyncData(
  `environment-${envId}`,
  () => environmentStore.fetchEnvironment(envId),
);

const { data: roomData, pending: roomPending } = await useAsyncData(
  `room-${envId}-${roomId}`,
  async () => {
    const env = environmentData.value ?? await environmentStore.fetchEnvironment(envId);
    if (env?.role === "user") return null;
    return getRoom(envId, roomId);
  },
);

const environment = computed(() => environmentData.value ?? null);
const room = computed(() => roomData.value ?? null);
const isLoading = computed(() => environmentPending.value || roomPending.value || isRefreshing.value);

const refreshRoom = async () => {
  if (environment.value?.role === "user") return;
  isRefreshing.value = true;
  roomData.value = await getRoom(envId, roomId);
  isRefreshing.value = false;
}

onMounted(async () => {
  if (!environmentData.value) {
    environmentData.value = await environmentStore.fetchEnvironment(envId, true);
  }

  await refreshRoom();
});

const deleteRoom = async () => {
  confirm.require({
        message: 'Are you sure you want to delete this room?',
        header: 'Confirmation',
        icon: 'pi pi-exclamation-triangle',
        rejectProps: {
          label: 'Cancel',
          severity: 'secondary',
          outlined: true
        },
        acceptProps: {
          label: 'Delete',
          severity: 'danger'
        },
        accept: async () => {
          await deleteRoomApi(envId, roomId);
          router.push({ name: 'environment-rooms', params: { envId } });
          toast.add({ severity: 'success', summary: 'Success', detail: 'Room successfully deleted', life: 3000 });
        },
      });
}
</script>

<style scoped>
.room-page {
  display: flex;
  flex: 1;
  height: 100%;
  min-height: 0;
  min-width: 0;
  width: 100%;
}

.room-panel {
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  min-width: 0;
  overflow: hidden;
  width: 100%;
}

.room-panel__header {
  align-items: center;
  background: var(--app-surface);
  border-bottom: 1px solid var(--app-border);
  display: flex;
  flex: 0 0 auto;
  gap: 12px;
  justify-content: space-between;
  min-width: 0;
  padding: 10px var(--app-panel-padding);
}

.room-panel__title {
  align-items: center;
  display: flex;
  gap: 12px;
  min-width: 0;
}

.room-panel__copy {
  min-width: 0;
}

.room-panel__eyebrow {
  color: var(--app-muted);
  display: block;
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 600;
  line-height: 1rem;
  text-transform: uppercase;
}

.room-panel h1 {
  color: var(--app-text-strong);
  font-size: 1rem;
  font-weight: 800;
  line-height: 1.35rem;
  margin: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.room-panel__actions {
  align-items: center;
  display: flex;
  gap: 6px;
  flex: 0 0 auto;
}

.room-panel__content {
  display: flex;
  flex: 1;
  min-height: 0;
  min-width: 0;
  overflow: auto;
  padding: 10px;
  scrollbar-gutter: stable;
}

@media (max-width: 520px) {
  .room-panel__header {
    align-items: flex-start;
    flex-direction: column;
  }

  .room-panel__actions {
    grid-template-columns: 1fr 1fr;
    width: 100%;
  }

  .room-panel__actions :deep(.p-button) {
    flex: 1 1 0;
  }

  .room-panel__content {
    padding: 10px;
  }
}
</style>
