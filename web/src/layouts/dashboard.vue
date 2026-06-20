<template>
  <div class="app-shell dashboard-shell">
    <dashboard-topbar />

    <section class="dashboard-workbench">
      <div
        v-if="hasBreadcrumbContext"
        class="dashboard-contextbar"
      >
        <Breadcrumbs class="w-full" />
      </div>

      <main
        class="dashboard-main"
        :class="{ 'dashboard-main--room': isRoomRoute }"
      >
        <slot />
      </main>
    </section>

    <ConfirmDialog />
    <Toast class="dashboard-toast" />
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute } from "vue-router";
import Breadcrumbs from "@/components/Breadcrumbs.vue";
import DashboardTopbar from "@/layout/DashboardTopbar.vue";
import ConfirmDialog from 'primevue/confirmdialog';
import Toast from 'primevue/toast';

const route = useRoute();
const isRoomRoute = computed(() => /^\/env\/[^/]+\/room\/[^/]+/.test(route.path));
const hasBreadcrumbContext = computed(() => Object.keys(route.params).length > 0);
</script>

<style scoped>
.dashboard-shell {
  display: grid;
  grid-template-columns: clamp(204px, 22vw, 244px) minmax(0, 1fr);
  height: 100vh;
  overflow: hidden;
  width: 100%;
}

.dashboard-workbench {
  display: flex;
  flex-direction: column;
  min-height: 0;
  min-width: 0;
  overflow: hidden;
}

.dashboard-contextbar {
  align-items: center;
  background: color-mix(in srgb, var(--app-bg) 86%, var(--app-surface));
  border-bottom: 1px solid var(--app-border);
  display: flex;
  flex: 0 0 auto;
  min-height: 42px;
  padding: 0.5rem var(--app-page-padding);
}

.dashboard-main {
  display: flex;
  flex: 1;
  min-height: 0;
  min-width: 0;
  overflow: auto;
  padding: var(--app-page-padding);
  width: 100%;
}

.dashboard-main--room {
  height: 100%;
  padding: var(--app-page-padding);
}

:global(.p-toast.p-toast-top-right) {
  right: var(--app-page-padding) !important;
  top: 4.75rem !important;
}

@media (max-width: 560px) {
  .dashboard-shell {
    grid-template-columns: 62px minmax(0, 1fr);
    grid-template-rows: minmax(0, 1fr);
  }

  .dashboard-main {
    padding: var(--app-gap-sm);
  }

  .dashboard-main--room {
    padding: var(--app-gap-sm);
  }

  .dashboard-contextbar {
    min-height: 38px;
    padding: 0.375rem 0.5rem;
  }

  :global(.p-toast.p-toast-top-right) {
    left: 0.5rem !important;
    right: 0.5rem !important;
    top: 0.5rem !important;
    width: auto !important;
  }
}
</style>
