<template>
  <div class="container max-w-7xl mx-auto py-6">
    <h1 class="text-3xl font-bold text-gray-800 mt-4 text-center">Панель управління</h1>

    <div class="mt-4">
      <h2 class="text-xl font-semibold text-gray-700">Список середовищ</h2>

      <!-- Skeleton Loader -->
      <div v-if="isLoading" class="mt-4 space-y-2">
        <div v-for="i in 5" :key="i" class="h-16 bg-gray-300 animate-pulse rounded"></div>
      </div>

      <!-- Заглушка, если окружений нет -->
      <div v-else-if="environments.length === 0" class="mt-10 flex flex-col items-center">
        <div class="flex flex-col items-center">
          <PlusIcon class="w-12 h-12 text-gray-400" />
          <h3 class="text-lg font-semibold text-gray-800 mt-4">Немає середовищ</h3>
          <p class="text-gray-500 text-sm mt-2 text-center">
            Почніть, створивши нове середовище.
          </p>
          <button
              @click="modalOpen = true"
              class="mt-4 px-4 py-2 bg-blue-600 text-white flex items-center font-semibold rounded-lg hover:bg-blue-700 transition"
          >
            <PlusIcon class="size-5 mr-2" />
            Створити середовище
          </button>
        </div>
      </div>

      <!-- Список окружений -->
      <ul v-else class="mt-4 border border-gray-200 rounded-lg divide-y divide-gray-200">
        <li
            v-for="env in environments"
            :key="env.id"
            class="p-4 flex justify-between items-center cursor-pointer hover:bg-gray-100 transition"
            @click="goToEnvironment(env.id)"
        >
          <div>
            <h3 class="text-lg font-semibold text-gray-800">{{ env.name }}</h3>
            <p class="text-gray-500 text-sm mt-1">{{ env.description || "Немає опису" }}</p>
          </div>
          <span
              class="px-3 py-1 rounded-full text-xs font-medium"
              :class="getRoleBadge(env.role)"
          >
            {{ env.role }}
          </span>
        </li>
      </ul>

      <!-- Пагинация -->
      <div class="mt-6 flex justify-center">
        <Pagination
            :totalPages="totalPages"
            :currentPage="currentPage"
            @prev="prevPage"
            @next="nextPage"
            @page-change="changePage"
        />
      </div>
    </div>

    <!-- Модальное окно создания окружения -->
    <CreateEnvironmentModal :isOpen="modalOpen" @close="modalOpen = false" @created="changePage(currentPage)" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRouter } from "vue-router";
import { useEnvironmentStore } from "@/store/environmentStore";
import type { Environment } from "@/services/apiService";
import Pagination from "@/components/Pagination.vue";
import { getRoleBadge } from "@/utils/environment";
import { PlusIcon } from "@heroicons/vue/24/outline";
import CreateEnvironmentModal from "@/components/CreateEnvironmentModal.vue";

const router = useRouter();
const environmentStore = useEnvironmentStore();
const modalOpen = ref(false);

const environments = ref<Environment[]>([]);
const pagination = ref({ total: 0, skip: 0, count: 5 });
const isLoading = ref(true);

const totalPages = computed(() => Math.ceil(pagination.value.total / pagination.value.count));
const currentPage = computed(() => Math.floor(pagination.value.skip / pagination.value.count) + 1);

onMounted(async () => {
  isLoading.value = true;
  environments.value = await environmentStore.fetchEnvironments();
  isLoading.value = false;
});

const prevPage = async () => {
  if (pagination.value.skip > 0) {
    pagination.value.skip -= pagination.value.count;
    await changePage(currentPage.value - 1);
  }
};

const nextPage = async () => {
  if (pagination.value.skip + pagination.value.count < pagination.value.total) {
    pagination.value.skip += pagination.value.count;
    await changePage(currentPage.value + 1);
  }
};

const changePage = async (page: number) => {
  isLoading.value = true;
  environments.value = await environmentStore.fetchEnvironments(page - 1);
  isLoading.value = false;
};

const goToEnvironment = (envId: number) => {
  router.push(`/env/${envId}`);
};
</script>
