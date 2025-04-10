<template>
  <Breadcrumb :home="home" :model="items" class="bg-surface-100 p-0">
    <template #item="{ item, props }">
      <Skeleton v-if="item.isLoading" :width="skeletonWidth" />
      <div v-else>
        <router-link v-if="item.route" v-slot="{ href, navigate }" :to="item.route" custom>
          <a :href="href" v-bind="props.action" @click="navigate" class="gap-0">
            <span :class="[item.icon? item.icon : 'hidden']" class="h-6 w-6 text-color p-1 hover:text-color-emphasis" />
            <span class="text-primary font-semibold hover:text-primary-emphasis">{{ item.label }}</span>
          </a>
        </router-link>
        <a v-else :href="item.url" :target="item.target" v-bind="props.action">
          <span class="text-surface-700">{{ item.label }}</span>
        </a>
      </div>
    </template>
  </Breadcrumb>
</template>

<script setup lang="ts">
import { ref, watch, computed } from "vue";
import { useRoute } from "vue-router";
import Breadcrumb from 'primevue/breadcrumb';
import Skeleton from 'primevue/skeleton';
import { breadcrumbConfig } from "@/config/breadcrumbConfig";

type BreadcrumbItem = {
  param: string;
  paramValue: any;
  label?: string;
  icon?: string;
  isLoading?: boolean;
  route?: {
    name: string;
    params?: Record<string, any>;
    query?: Record<string, any>;
  };
};

const route = useRoute();

const home = ref<BreadcrumbItem>({
  param: "",
  paramValue: null,
  icon: 'pi pi-home',
  route: {
    name: 'dashboard'
  }
});
const items = ref<BreadcrumbItem[]>([]);

const skeletonWidth = computed(() => {
  const value = (Math.random() * 2 + 5).toFixed(2);
  return `${value}rem`;
});

const fetchBreadcrumbData = async (param: string) => {
  const config = breadcrumbConfig[param];
  if (!config) return;

  return {
    label: await config.fetchData(route.params),
    route: {
      name: config.path
    }
  }
};

const reformatBreadcrumbData = async (start: number) => {
  for (let i = start; i < Object.keys(route.params).length; i++) {
    items.value[i].isLoading = true;

    const param = Object.keys(route.params)[i];
    const item = await fetchBreadcrumbData(param);
    items.value[i].label = item.label;
    items.value[i].route = item.route;

    items.value[i].isLoading = false;
  }
}

const clearBreadcrumbs = () => {
  if (Object.keys(route.params).length < items.value.length) {
    items.value = items.value.slice(0, Object.keys(route.params).length);
  }
}

watch(() => route.params, async () => {
  if (items.value.length === 0) {
    for (const param of Object.keys(route.params)) {
      items.value.push({
        param: param,
        paramValue: route.params[param],
        isLoading: true
      });
    }
  }

  for (let i = 0; i < Object.keys(route.params).length; i++) {
    const param = Object.keys(route.params)[i];

    if (!items.value[i]) {
      items.value.push({
        param: param,
        paramValue: route.params[param],
        isLoading: true
      });
    }

    if (items.value[i].isLoading === true) {
      reformatBreadcrumbData(i).then();
      return ;
    } else if (items.value[i].param !== param) {
      reformatBreadcrumbData(i).then();
      return ;
    } else if (items.value[i].paramValue !== route.params[param]) {
      reformatBreadcrumbData(i).then();
      return ;
    }
  }

  clearBreadcrumbs();
}, { immediate: true });
</script>
