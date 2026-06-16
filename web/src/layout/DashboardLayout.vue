<template>
  <div class="h-screen flex flex-col overflow-hidden bg-surface-100 text-color">
    <dashboard-topbar />

    <Divider class="m-0"/>

    <div class="flex justify-center p-px bg-surface-0">
      <div class="w-full max-w-7xl my-3 flex items-center">
        <Breadcrumbs class="w-full !bg-surface-0" />
      </div>
    </div>

    <div class="flex-grow flex flex-col items-center min-h-0 overflow-y-auto">
      <main class="w-full max-w-7xl flex flex-grow p-6">
        <router-view />
      </main>
      <dashboard-footer />
    </div>
    <ConfirmDialog />
    <Toast />
  </div>
</template>

<script setup lang="ts">
import Breadcrumbs from "@/components/Breadcrumbs.vue";
import DashboardTopbar from "@/layout/DashboardTopbar.vue";
import DashboardFooter from "@/layout/DashboardFooter.vue";
import Divider from 'primevue/divider';
import ConfirmDialog from 'primevue/confirmdialog';
import Toast from 'primevue/toast';
</script>
