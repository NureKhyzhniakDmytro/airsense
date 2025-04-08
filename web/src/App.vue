<template>
  <div class="min-h-screen flex flex-col bg-gray-100 text-gray-900">
    <!-- Навбар -->
    <Navbar />

    <!-- Основной контент -->
    <main class="flex-grow p-6 flex flex-col items-center">
      <div class="w-full max-w-7xl">
        <Breadcrumbs v-if="showBreadcrumbs" />
        <router-view />
      </div>
    </main>

    <!-- Футер -->
    <footer class="bg-white shadow p-4 text-center text-gray-600">
      &copy; 2025 AirSense
    </footer>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useAuthStore } from "@/store/authStore";
import { useRouter, useRoute } from "vue-router";
import Breadcrumbs from "@/components/Breadcrumbs.vue";
import Navbar from "@/components/Navbar.vue";

const authStore = useAuthStore();
const router = useRouter();
const route = useRoute();

const hiddenRoutes = ["/login", "/register"];

const showBreadcrumbs = computed(() => !hiddenRoutes.includes(route.path));

const logout = async () => {
  await authStore.logout();
  await router.push("/auth");
};
</script>
