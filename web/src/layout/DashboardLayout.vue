<template>
  <div class="min-h-screen flex flex-col bg-surface-100 text-color">
    <dashboard-topbar />

    <Divider class="m-0"/>

    <div class="flex justify-center p-px bg-surface-0">
      <div class="w-full max-w-7xl my-3">
        <div class="flex items-center flex-col">
          <Breadcrumbs class="w-full !bg-surface-0"  />
        </div>
      </div>
    </div>

    <main class="flex-grow p-6 flex flex-col items-center">
      <div class="w-full max-w-7xl flex flex-grow">
        <router-view />
      </div>
    </main>

    <dashboard-footer />

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
