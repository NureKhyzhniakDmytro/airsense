<template>
  <nav class="mb-4">
    <div class="max-w-7xl mx-auto flex items-center gap-2 text-gray-500 text-sm">
      <!-- Home -->
      <router-link to="/" class="hover:text-gray-700 flex items-center gap-1">
        <HomeIcon class="w-5 h-5" />
      </router-link>

      <!-- Skeleton Loader -->
      <span v-if="isLoading" class="flex items-center gap-2">
        <template v-for="(_, index) in expectedBreadcrumbs" :key="index">
          <ChevronRightIcon class="w-5 h-5" />
          <div class="h-5 w-20 bg-gray-300 rounded animate-pulse"></div>
        </template>
      </span>

      <!-- Breadcrumbs -->
      <span v-else v-for="(crumb, index) in breadcrumbs" :key="index" class="flex items-center gap-2">
        <ChevronRightIcon class="w-5 h-5" />

        <router-link
            v-if="crumb.path && index !== breadcrumbs.length - 1"
            :to="crumb.path"
            class="hover:text-gray-700"
        >
          {{ crumb.label }}
        </router-link>

        <span v-else class="text-gray-700 font-medium">{{ crumb.label }}</span>
      </span>
    </div>
  </nav>
</template>

<script setup lang="ts">
import { computed, ref, watchEffect } from "vue";
import { useRoute } from "vue-router";
import { HomeIcon, ChevronRightIcon } from "@heroicons/vue/24/outline";
import { breadcrumbConfig } from "@/config/breadcrumbConfig";

const route = useRoute();
const breadcrumbLabels = ref<Record<string, string>>({});
const breadcrumbPaths = ref<Record<string, string | null>>({});
const isLoading = ref(true);

const expectedBreadcrumbs = computed(() => Object.keys(route.params).length);

const fetchBreadcrumbData = async (param: string, params: number[]) => {
  const config = breadcrumbConfig[param];
  if (!config) return;

  if (config.dependsOn) {
    const parentId = Number(route.params[config.dependsOn]);
    if (!isNaN(parentId)) {
      params.unshift(parentId);
      await fetchBreadcrumbData(config.dependsOn, params);
    }
  }
  breadcrumbLabels.value[param] = await config.fetchData(params);
  breadcrumbPaths.value[param] = config.path ? config.path(params) : null;
};

watchEffect(async () => {
  isLoading.value = true;
  breadcrumbLabels.value = {};
  breadcrumbPaths.value = {};

  for (const param of Object.keys(breadcrumbConfig)) {
    const id = Number(route.params[param]);
    if (!isNaN(id)) {
      await fetchBreadcrumbData(param, [id]);
    }
  }

  isLoading.value = false;
});

const breadcrumbs = computed(() =>
    Object.entries(breadcrumbLabels.value).map(([param, label]) => ({
      label,
      path: breadcrumbPaths.value[param] || null,
    }))
);
</script>
