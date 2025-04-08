<template>
  <div class="container max-w-7xl mx-auto py-6">
    <!-- Заголовок -->
    <div class="flex justify-between items-center">
      <h1 v-if="environment" class="text-3xl font-bold text-gray-800">{{ environment.name }}</h1>
    </div>

    <!-- Skeleton Loader -->
    <div v-if="isLoading" class="space-y-2">
      <div class="h-10 bg-gray-300 animate-pulse rounded"></div>
    </div>

    <div>
      <!-- Навигация по вкладкам -->
      <div class="mt-6 flex gap-6 border-b border-gray-300">
        <router-link
            @click="activeTab = 'rooms'"
            :to="{ name: 'rooms' }"
            :class="['py-2 px-4 text-lg font-medium transition',
                   activeTab === 'rooms' ? 'border-b-2 border-blue-500 text-blue-600' : 'text-gray-500 hover:text-gray-700']"
        >
          Кімнати
        </router-link>
        <router-link
            @click="activeTab = 'members'"
            :to="{ name: 'members' }"
            :class="['py-2 px-4 text-lg font-medium transition',
                   activeTab === 'members' ? 'border-b-2 border-blue-500 text-blue-600' : 'text-gray-500 hover:text-gray-700']"
        >
          Учасники
        </router-link>
      </div>

      <!-- Контент -->
      <div class="mt-6">
        <router-view />
<!--        <RoomsList v-if="activeTab === 'rooms' && environment" :env="environment" />-->
<!--        <MembersList v-if="activeTab === 'members'" :env="environment" />-->
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { Environment } from "@/services/apiService";
import { useEnvironmentStore } from "@/store/environmentStore"

const route = useRoute();
const router = useRouter();
const envId = Number(route.params.envId);
const environment = ref<Environment | null>(null);
const environmentStore = useEnvironmentStore();
const activeTab = ref<"rooms" | "members">(route.path.split("/").slice(-1)[0]);
const isLoading = ref(true);

onMounted(async () => {
  if (route.path === `/env/${envId}`) {
    await router.replace(`/env/${envId}/rooms`);
    activeTab.value = "rooms";
  }

  isLoading.value = true;
  environment.value = await environmentStore.fetchEnvironment(envId);
  isLoading.value = false;
});
</script>
