<template>
  <div class="min-h-screen flex flex-col bg-surface-100 text-color">
    <dashboard-topbar />

    <main class="flex-grow p-6 flex flex-col items-center">
      <div class="w-full max-w-7xl">
        <Breadcrumbs />
        <router-view />
      </div>
    </main>

    <dashboard-footer />
  </div>
</template>

<script setup lang="ts">
import Breadcrumbs from "@/components/Breadcrumbs.vue";
import DashboardTopbar from "@/layout/DashboardTopbar.vue";
import DashboardFooter from "@/layout/DashboardFooter.vue";
</script>
