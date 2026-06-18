<template>
  <div class="entity-page">
    <AppPageHeader
      :title="environment?.name || 'Environment'"
      description="Rooms, members, and operating access for this AirSense environment."
      eyebrow="Environment"
    >
      <template #badge>
        <span class="entity-page__badges">
          <PlaceIcon :name="environment?.icon" size="sm" />
          <RoleTag v-if="environment?.role" :role="environment.role" />
        </span>
      </template>

      <template #actions>
        <Button label="Edit" icon="pi pi-pencil" rounded variant="text" @click="editEnvironmentDialog = true" />
        <Button label="Delete" icon="pi pi-trash" rounded severity="danger" @click="deleteEnvironment" variant="text" />
      </template>
    </AppPageHeader>

    <div v-if="isLoading" class="entity-page__loading">
      <Skeleton width="12rem" height="2rem" />
    </div>

    <div class="entity-page__content">
      <NuxtPage />
    </div>

    <edit-environment-dialog v-model="editEnvironmentDialog" :envId="envId" @refresh="refreshEnvironment" />
  </div>
</template>

<script setup lang="ts">
definePageMeta({ name: 'environment', layout: 'dashboard', requiresAuth: true })

import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useEnvironmentStore } from "@/store/environmentStore"
import EditEnvironmentDialog from "@/components/environment/EditEnvironmentDialog.vue";
import { deleteEnvironment as deleteEnvironmentApi } from "@/services/apiService";
import AppPageHeader from "@/components/common/AppPageHeader.vue";
import RoleTag from "@/components/common/RoleTag.vue";
import PlaceIcon from "@/components/common/PlaceIcon.vue";
import Button from 'primevue/button';
import Skeleton from 'primevue/skeleton';
import { useConfirm } from "primevue/useconfirm";
import { useToast } from "primevue/usetoast";

const route = useRoute();
const router = useRouter();
const envId = Number(route.params.envId);
const environmentStore = useEnvironmentStore();
const confirm = useConfirm();
const toast = useToast();
const isRefreshing = ref(false);
const editEnvironmentDialog = ref(false);

if (route.name === 'environment') {
  await navigateTo({ name: 'environment-rooms', params: { envId } }, { replace: true });
}

const { data: environmentData, pending } = await useAsyncData(
  `environment-${envId}`,
  () => environmentStore.fetchEnvironment(envId),
);

const environment = computed(() => (
  environmentData.value
    ?? environmentStore.environments.find((item) => item.id === envId)
    ?? null
));
const isLoading = computed(() => pending.value || isRefreshing.value);

const refreshEnvironment = async () => {
  isRefreshing.value = true;
  environmentData.value = await environmentStore.fetchEnvironment(envId, true);
  isRefreshing.value = false;
}

onMounted(async () => {
  if (!environmentData.value) {
    environmentData.value = await environmentStore.fetchEnvironment(envId, true);
  }
});

const deleteEnvironment = async () => {
  confirm.require({
        message: 'Are you sure you want to delete this environment?',
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
          await deleteEnvironmentApi(envId);
          router.push({ name: 'dashboard' });
          toast.add({ severity: 'success', summary: 'Success', detail: 'Environment successfully deleted', life: 3000 });
        },
      });
}
</script>

<style scoped>
.entity-page {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: var(--app-gap-md);
  min-height: 0;
  min-width: 0;
  width: 100%;
}

.entity-page__loading {
  min-height: 36px;
}

.entity-page__badges {
  align-items: center;
  display: inline-flex;
  gap: 8px;
}

.entity-page__content {
  display: flex;
  flex: 1;
  max-width: 100%;
  min-height: 0;
  min-width: 0;
}
</style>
