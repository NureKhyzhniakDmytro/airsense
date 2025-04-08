<template>
  <div class="container max-w-7xl mx-auto py-6">
    <!-- Заголовок -->
    <div class="flex justify-between items-center">
      <h1 v-if="room" class="text-3xl font-bold text-gray-800">{{ room.name }}</h1>
    </div>

    <!-- Skeleton Loader -->
    <div v-if="isLoading" class="space-y-2 mt-4">
      <div class="h-10 bg-gray-300 animate-pulse rounded"></div>
    </div>

    <div v-else>
      <!-- Навигация по вкладкам -->
      <div class="mt-6 flex gap-6 border-b border-gray-300">
        <router-link
            :to="{ name: 'room-parameters', params: { envId, roomId } }"
            :class="[
              'py-2 px-4 text-lg font-medium transition',
              activeTab === 'parameters' ? 'border-b-2 border-blue-500 text-blue-600' : 'text-gray-500 hover:text-gray-700'
            ]"
            @click="activeTab = 'parameters'"
        >
          Параметри
        </router-link>
        <router-link
            :to="{ name: 'room-sensors', params: { envId, roomId } }"
            :class="[
              'py-2 px-4 text-lg font-medium transition',
              activeTab === 'sensors' ? 'border-b-2 border-blue-500 text-blue-600' : 'text-gray-500 hover:text-gray-700'
            ]"
            @click="activeTab = 'sensors'"
        >
          Датчики
        </router-link>
        <router-link
            :to="{ name: 'room-devices', params: { envId, roomId } }"
            :class="[
              'py-2 px-4 text-lg font-medium transition',
              activeTab === 'devices' ? 'border-b-2 border-blue-500 text-blue-600' : 'text-gray-500 hover:text-gray-700'
            ]"
            @click="activeTab = 'devices'"
        >
          Пристрої
        </router-link>
        <router-link
            :to="{ name: 'room-settings', params: { envId, roomId } }"
            :class="[
              'py-2 px-4 text-lg font-medium transition',
              activeTab === 'settings' ? 'border-b-2 border-blue-500 text-blue-600' : 'text-gray-500 hover:text-gray-700'
            ]"
            @click="activeTab = 'settings'"
        >
          Налаштування
        </router-link>
      </div>

      <!-- Контент -->
      <div class="mt-6">
        <router-view :room="room" :env="environment" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useEnvironmentStore } from "@/store/environmentStore";
import { getRoom } from "@/services/apiService";
import type { Room, Environment } from "@/services/apiService";

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

onMounted(async () => {
  if (route.path === `/env/${envId}/room/${roomId}`) {
    await router.replace({ name: "room-parameters", params: { envId, roomId } });
    activeTab.value = "parameters";
  }

  isLoading.value = true;
  environment.value = await environmentStore.fetchEnvironment(envId);
  room.value = await getRoom(envId, roomId);
  isLoading.value = false;
});
</script>
