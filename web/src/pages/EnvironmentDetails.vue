<template>
  <div class="flex flex-col flex-grow">
    <div class="flex justify-between items-center mt-8">
      <h1 v-if="environment" class="text-3xl font-bold text-gray-800">{{ environment.name }}</h1>
      <div class="flex items-center gap-2">
        <Button label="Edit" icon="pi pi-pencil" rounded variant="text" @click="editEnvironmentDialog = true" />
        <Button label="Delete" icon="pi pi-trash" rounded severity="danger" @click="deleteEnvironment" variant="text" />
      </div>
    </div>

    <div v-if="isLoading" class="space-y-2">
      <div class="h-10 bg-gray-300 animate-pulse rounded"></div>
    </div>

    <Tabs :value="activeTab" class="mt-8">
      <TabList qwety="qwety" :pt="{ tabList: 'bg-inherit'}">
        <Tab v-for="tab in items" :key="tab.label" :value="tab.value">
          <router-link v-if="tab.route" v-slot="{ href, navigate }" :to="tab.route" custom>
            <a :href="href" @click="navigate" class="flex items-center gap-2 text-inherit">
              <i :class="tab.icon" />
              <span>{{ tab.label }}</span>
            </a>
          </router-link>
        </Tab>
      </TabList>
    </Tabs>

    <div class="my-6 flex flex-grow">
      <router-view />
    </div>

    <edit-environment-dialog v-model="editEnvironmentDialog" :envId="envId" @refresh="refreshEnvironment" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { Environment } from "@/types/environment";
import { useEnvironmentStore } from "@/store/environmentStore"
import EditEnvironmentDialog from "@/components/environment/EditEnvironmentDialog.vue";
import { deleteEnvironment as deleteEnvironmentApi } from "@/services/apiService";
import Tabs from 'primevue/tabs';
import TabList from 'primevue/tablist';
import Tab from 'primevue/tab';
import Button from 'primevue/button';
import { useConfirm } from "primevue/useconfirm";
import { useToast } from "primevue/usetoast";

const route = useRoute();
const router = useRouter();
const envId = Number(route.params.envId);
const environment = ref<Environment | null>(null);
const environmentStore = useEnvironmentStore();
const activeTab = ref<"rooms" | "members">(route.path.split("/").slice(-1)[0] === "members" ? "members" : "rooms");
const confirm = useConfirm();
const toast = useToast();
const isLoading = ref(true);
const editEnvironmentDialog = ref(false);
const items = ref([
  { route: { name: 'environment-rooms' }, label: 'Rooms', icon: 'pi pi-list', value: 'rooms' },
  { route: { name: 'environment-members' }, label: 'Members', icon: 'pi pi-users', value: 'members' },
]);

const refreshEnvironment = async () => {
  isLoading.value = true;
  environment.value = await environmentStore.fetchEnvironment(envId, true);
  isLoading.value = false;
}

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

onMounted(async () => {
  if (route.name === 'environment') {
    await router.replace({ name: 'environment-rooms' });
    activeTab.value = "rooms";
  }

  isLoading.value = true;
  environment.value = await environmentStore.fetchEnvironment(envId);
  isLoading.value = false;
});
</script>
