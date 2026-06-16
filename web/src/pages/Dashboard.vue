<template>
  <div
      class="items-center flex-grow"
      :class="{ 'place-content-center': environmentStore.environments.length === 0 }"
  >
    <div v-if="environmentStore.environments.length !== 0" class="mt-8">
      <h1 class="text-3xl font-bold text-gray-800">Environments</h1>

      <DataView
          :value="environmentStore.environments"
          :total-records="environmentStore.pagination.total"
          @page="changePage"
          paginator
          :rows="environmentStore.pagination.count"
          :pt="{ content: 'rounded-t-xl', pcPaginator: {
            root: 'rounded-b-xl rounded-none'
          } }"
          class = "mt-8"
      >
        <template #list="slotProps">
          <div class="flex flex-col rounded-md">
            <div
                v-if="!isLoading"
                v-for="(item, index) in slotProps.items"
                :key="index"
                class="p-6 flex justify-between items-center cursor-pointer hover:bg-surface-200 transition"
                :class="{
                  'border-t border-surface-200': index !== 0,
                  'rounded-t-xl': index === 0,
                  }"
                @click="goToEnvironment(item.id)"
            >
              <div>
                <h3 class="text-lg font-semibold text-color">{{ item.name }}</h3>
                <p class="text-muted-color text-sm mt-1">{{ item.description || "Немає опису" }}</p>
              </div>
              <div class="gap-4 flex">
                <span
                    class="px-3 py-1 rounded-full text-xs font-medium"
                    :class="getRoleBadge(item.role)"
                >
                  {{ item.role }}
                </span>
                <i class="pi pi-angle-right place-self-center text-muted-color group-hover:text-color" />
              </div>
            </div>

            <div
                v-if="isLoading"
                v-for="index in environmentStore.pagination.count"
                :key="index"
                class="p-6 flex justify-between items-center cursor-pointer hover:bg-surface-200 transition"
                :class="{
                  'border-t border-surface-200': index !== 1,
                  'rounded-t-xl': index === 1,
                  }"
            >
              <div class="w-fit">
                <Skeleton width="8rem" height="1.25rem" class="my-2" />
                <Skeleton width="5rem" height="1rem" />
              </div>
              <div class="gap-4 flex">
                <Skeleton width="3rem" height="1.5rem" class="rounded-full" />
                <i class="pi pi-angle-right place-self-center text-muted-color group-hover:text-color" />
              </div>
            </div>
          </div>
        </template>
      </DataView>
    </div>

    <div v-else class="flex flex-col items-center">
      <i class="pi pi-plus text-5xl text-gray-400" />
      <h3 class="text-lg font-semibold text-gray-800 mt-4">No environments</h3>
      <p class="text-gray-500 text-sm mt-2 text-center">
        Start by creating a new environment.
      </p>
      <div class="mt-4">
        <Button @click="createEnvironmentDialog = true" label="New Environment"  />
        <create-environment-dialog v-model="createEnvironmentDialog"/>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRouter } from "vue-router";
import { useEnvironmentStore } from "@/store/environmentStore";
import { getRoleBadge } from "@/utils/environment";
import DataView from 'primevue/dataview';
import type { DataViewPageEvent } from 'primevue/dataview';
import Button from "primevue/button";
import Skeleton from 'primevue/skeleton';
import CreateEnvironmentDialog from "@/components/environment/CreateEnvironmentDialog.vue"

const router = useRouter();
const environmentStore = useEnvironmentStore();
const createEnvironmentDialog = ref(false);
const isLoading = ref(true);

onMounted(async () => await changePage({ page: 0 } as DataViewPageEvent));

const changePage = async (event: DataViewPageEvent) => {
  isLoading.value = true;
  await environmentStore.fetchEnvironments(event.page);
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
