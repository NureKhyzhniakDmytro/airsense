<template>
  <div class="dashboard-page">
    <AppPageHeader
      title="Environments"
      :description="headerDescription"
      eyebrow="Workspace index"
    >
      <template v-if="hasEnvironments" #actions>
        <Button @click="createEnvironmentDialog = true" label="New Environment" icon="pi pi-plus" />
      </template>
    </AppPageHeader>

    <div class="dashboard-metrics">
      <MetricCard
        label="Total environments"
        :value="environmentStore.pagination.total"
        icon="pi pi-building"
        :hint="`${environmentStore.environments.length} listed`"
      />
      <MetricCard
        label="Owner access"
        :value="ownerCount"
        icon="pi pi-shield"
        hint="Full control"
      />
      <MetricCard
        label="Member access"
        :value="memberCount"
        icon="pi pi-users"
        hint="Shared access"
      />
    </div>

    <DataView
      v-if="hasEnvironments"
      :value="environmentStore.environments"
      :total-records="environmentStore.pagination.total"
      @page="changePage"
      paginator
      :rows="environmentStore.pagination.count"
      class="dashboard-list"
    >
      <template #list="slotProps">
        <div class="flex flex-col">
          <button
            v-if="!isLoading"
            v-for="(item, index) in slotProps.items"
            :key="item.id"
            type="button"
            class="environment-row app-clickable"
            :class="{ 'border-t border-surface-200': index !== 0 }"
            @click="goToEnvironment(item.id)"
          >
            <span class="environment-row__index">
              ENV-{{ item.id }}
            </span>

            <span class="environment-row__copy">
              <span class="environment-row__title">{{ item.name }}</span>
              <span class="environment-row__description">{{ item.description || "No description provided" }}</span>
            </span>

            <span class="environment-row__meta">
              <RoleTag :role="item.role" />
              <i class="pi pi-angle-right text-muted-color" />
            </span>
          </button>

          <div
            v-if="isLoading"
            v-for="index in environmentStore.pagination.count"
            :key="index"
            class="environment-row"
            :class="{ 'border-t border-surface-200': index !== 1 }"
          >
            <Skeleton shape="circle" size="2.75rem" />
            <span class="environment-row__copy">
              <Skeleton width="9rem" height="1.25rem" />
              <Skeleton width="14rem" height="1rem" />
            </span>
            <span class="environment-row__meta">
              <Skeleton width="4.5rem" height="1.6rem" />
            </span>
          </div>
        </div>
      </template>
    </DataView>

    <EmptyState
      v-else
      class="dashboard-empty"
      title="No environments"
      description="Create an environment to group rooms, users, sensors, and automation settings."
      icon="pi pi-plus"
      action-label="New Environment"
      action-icon="pi pi-plus"
      @action="createEnvironmentDialog = true"
    />

    <create-environment-dialog v-model="createEnvironmentDialog"/>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ name: 'dashboard', layout: 'dashboard', requiresAuth: true })

import { computed, ref, watch } from "vue";
import { useRouter } from "vue-router";
import { useEnvironmentStore } from "@/store/environmentStore";
import { useAuthStore } from "@/store/authStore";
import DataView from 'primevue/dataview';
import type { DataViewPageEvent } from 'primevue/dataview';
import Button from "primevue/button";
import Skeleton from 'primevue/skeleton';
import CreateEnvironmentDialog from "@/components/environment/CreateEnvironmentDialog.vue"
import AppPageHeader from "@/components/common/AppPageHeader.vue";
import EmptyState from "@/components/common/EmptyState.vue";
import MetricCard from "@/components/common/MetricCard.vue";
import RoleTag from "@/components/common/RoleTag.vue";

const router = useRouter();
const environmentStore = useEnvironmentStore();
const authStore = useAuthStore();
const createEnvironmentDialog = ref(false);
const isLoading = ref(false);

await useAsyncData('dashboard-environments-page-0', () => environmentStore.fetchEnvironments(0));

if (import.meta.client) {
  watch(
    () => authStore.token,
    async (token) => {
      if (!token) return;
      await environmentStore.fetchEnvironments(0, true);
    },
    { immediate: true }
  );
}

const ownerCount = computed(() => environmentStore.environments.filter(env => env.role === "owner").length);
const memberCount = computed(() => environmentStore.environments.filter(env => env.role !== "owner").length);
const hasEnvironments = computed(() => environmentStore.environments.length !== 0);
const headerDescription = computed(() => (
  environmentStore.pagination.total > 0
    ? "Operational spaces available to the current account."
    : "No operational spaces have been registered yet."
));

const changePage = async (event: DataViewPageEvent) => {
  isLoading.value = true;
  await environmentStore.fetchEnvironments(event.page ?? 0);
  isLoading.value = false;
};

const goToEnvironment = (envId: number) => {
  router.push({
    name: 'environment',
    params: {
      envId: envId,
    }
  })
};
</script>

<style scoped>
.dashboard-page {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 12px;
  min-height: 0;
  min-width: 0;
  width: 100%;
}

.dashboard-metrics {
  display: grid;
  gap: 8px;
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.dashboard-list {
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
}

.dashboard-list :deep(.p-dataview-content) {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
}

.dashboard-list :deep(.p-dataview-content > div),
.dashboard-list :deep(.p-dataview-content .flex.flex-col) {
  display: flex;
  flex: 1;
  flex-direction: column;
}

.dashboard-list :deep(.p-paginator) {
  border-top: 1px solid var(--app-border);
  flex: 0 0 auto;
}

.dashboard-empty {
  flex: 1;
}

.environment-row {
  align-items: center;
  background: var(--app-surface);
  border: 0;
  color: inherit;
  display: grid;
  gap: 14px;
  grid-template-columns: 74px minmax(0, 1fr) auto;
  min-height: 60px;
  padding: 10px 12px;
  text-align: left;
  width: 100%;
}

.environment-row:hover {
  background: var(--app-surface-soft);
}

.environment-row__index {
  align-items: center;
  color: var(--app-muted);
  display: inline-flex;
  font-family: var(--app-mono);
  font-size: 0.72rem;
  font-weight: 600;
  height: 100%;
  justify-content: flex-start;
}

.environment-row__copy {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.environment-row__title {
  color: var(--app-text-strong);
  font-size: 0.95rem;
  font-weight: 760;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.environment-row__description {
  color: var(--app-muted);
  font-size: 0.8125rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.environment-row__meta {
  align-items: center;
  display: inline-flex;
  gap: 12px;
}

@media (max-width: 840px) {
  .dashboard-metrics {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 620px) {
  .environment-row {
    grid-template-columns: minmax(0, 1fr);
  }

  .environment-row__index {
    display: none;
  }

  .environment-row__meta {
    justify-content: space-between;
  }
}
</style>
