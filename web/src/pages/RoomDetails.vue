<template>
  <div class="flex flex-col flex-grow">
    <div class="flex justify-between items-center mt-8">
      <h1 v-if="room" class="text-3xl font-bold text-gray-800">{{ room.name }}</h1>
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
import { useEnvironmentStore } from "@/store/environmentStore";
import { getRoom } from "@/services/apiService";
import type { Room, Environment } from "@/services/apiService";
import Tabs from 'primevue/tabs';
import TabList from 'primevue/tablist';
import Tab from 'primevue/tab';

const route = useRoute();
const router = useRouter();
const envId = Number(route.params.envId);
const roomId = Number(route.params.roomId);

const environmentStore = useEnvironmentStore();
const environment = ref<Environment | null>(null);
const room = ref<Room | null>(null);
const isLoading = ref(true);

const activeTab = ref<"parameters" | "sensors" | "devices" | "settings">(
    route.path.includes("sensors")
        ? "sensors"
        : route.path.includes("devices")
            ? "devices"
            : route.path.includes("settings")
                ? "settings"
                : "parameters"
);

const items = ref([
  { route: { name: 'room-parameters' }, label: 'Parameters', icon: 'pi pi-chart-line', value: 'parameters' },
  { route: { name: 'room-sensors' }, label: 'Sensors', icon: 'pi pi-bullseye', value: 'sensors' },
  { route: { name: 'room-devices' }, label: 'Devices', icon: 'pi pi-slack', value: 'devices' },
  { route: { name: 'room-settings' }, label: 'Settings', icon: 'pi pi-cog', value: 'settings' },
]);

onMounted(async () => {
  if (route.name === "room") {
    await router.replace({ name: "room-parameters" });
    activeTab.value = "parameters";
  }

  isLoading.value = true;
  environment.value = await environmentStore.fetchEnvironment(envId);
  room.value = await getRoom(envId, roomId);
  isLoading.value = false;
});
</script>
