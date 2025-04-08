<template>
  <header class="bg-white shadow py-4">
    <div class="max-w-7xl mx-auto flex items-center justify-between">
      <!-- Лого + Меню -->
      <div class="flex items-center gap-6">
        <router-link to="/">
          <img src="/logo.svg" alt="Logo" class="h-10 w-10 object-contain rounded-full shadow-sm" />
        </router-link>

        <!-- Навигация -->
        <nav class="flex gap-4 text-gray-600">
          <router-link
              v-for="item in menu"
              :key="item.name"
              :to="item.path"
              class="relative py-2 px-3 font-medium hover:text-black transition"
              :class="{ 'text-black font-semibold': isActive(item.path) }"
          >
            {{ item.name }}
            <span v-if="isActive(item.path)" class="absolute left-0 bottom-0 w-full h-0.5 bg-blue-500"></span>
          </router-link>
        </nav>
      </div>

      <!-- Правая часть -->
      <div v-if="isAuthenticated" class="flex items-center gap-4 relative">
        <button @click="modalOpen = true" class="bg-blue-600 hover:bg-blue-700 text-white font-semibold px-4 py-2 rounded-lg flex items-center gap-2">
          <PlusIcon class="size-5" />
          New Environment
        </button>

        <button class="text-gray-500 hover:text-gray-800">
          <BellIcon class="w-6 h-6" />
        </button>

        <UserDropdown />
      </div>
    </div>
  </header>

  <CreateEnvironmentModal :isOpen="modalOpen" @close="modalOpen = false" />
</template>

<script setup lang="ts">
import { ref, computed } from "vue";
import { BellIcon, PlusIcon } from "@heroicons/vue/24/outline";
import { useRoute } from "vue-router";
import { useAuthStore } from "@/store/authStore";
import CreateEnvironmentModal from "@/components/CreateEnvironmentModal.vue";
import UserDropdown from "@/components/UserDropdown.vue";

const route = useRoute();
const authStore = useAuthStore();
const modalOpen = ref(false);

const menu = [{ name: "Dashboard", path: "/" }];
const isActive = (path: string) => route.path === path || route.path.startsWith(path);
const isAuthenticated = computed(() => !!authStore.user);
</script>
