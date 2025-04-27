<template>
  <div class="flex flex-col flex-grow">
    <div class="flex justify-between items-center mt-8">
      <h1 v-if="environment" class="text-3xl font-bold text-gray-800">{{ environment.name }}</h1>
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
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { Environment } from "@/services/apiService";
import { useEnvironmentStore } from "@/store/environmentStore"
import Tabs from 'primevue/tabs';
import TabList from 'primevue/tablist';
import Tab from 'primevue/tab';

const route = useRoute();
const router = useRouter();
const envId = Number(route.params.envId);
const environment = ref<Environment | null>(null);
const environmentStore = useEnvironmentStore();
const activeTab = ref<"rooms" | "members">(route.path.split("/").slice(-1)[0] === "members" ? "members" : "rooms");
const isLoading = ref(true);

const items = ref([
  { route: { name: 'environment-rooms' }, label: 'Rooms', icon: 'pi pi-list', value: 'rooms' },
  { route: { name: 'environment-members' }, label: 'Members', icon: 'pi pi-users', value: 'members' },
]);

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
