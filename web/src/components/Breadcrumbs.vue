<template>
  <Breadcrumb :home="home" :model="items" class="app-breadcrumbs">
    <template #item="{ item, props }">
      <Skeleton v-if="item.isLoading" :width="skeletonWidth" />
      <div v-else>
        <router-link v-if="item.route" v-slot="{ href, navigate }" :to="item.route" custom>
          <a :href="href" v-bind="props.action" @click="navigate" class="app-breadcrumbs__item">
            <span v-if="item.icon" :class="item.icon" class="app-breadcrumbs__icon" />
            <span class="app-breadcrumbs__label">{{ item.label }}</span>
          </a>
        </router-link>
        <a v-else :href="item.url" :target="item.target" v-bind="props.action" class="app-breadcrumbs__item">
          <span class="app-breadcrumbs__plain">{{ item.label }}</span>
        </a>
      </div>
    </template>
  </Breadcrumb>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";
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
const skeletonWidth = "6rem";

const fetchBreadcrumbData = async (param: string) => {
  const config = breadcrumbConfig[param];
  if (!config) return;

  return {
    label: await config.fetchData(route.params),
    route: {
      name: config.path,
      params: route.params,
    }
  }
};

const reformatBreadcrumbData = async (start: number) => {
  for (let i = start; i < Object.keys(route.params).length; i++) {
    items.value[i].isLoading = true;

    const param = Object.keys(route.params)[i];
    const item = await fetchBreadcrumbData(param);
    if (!item) continue;

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

<style scoped>
.app-breadcrumbs {
  background: transparent;
  border: 0;
  padding: 0;
}

.app-breadcrumbs :deep(.p-breadcrumb-list) {
  gap: 4px;
}

.app-breadcrumbs :deep(.p-breadcrumb-separator) {
  color: var(--app-muted);
  font-size: 0.7rem;
}

.app-breadcrumbs__item {
  align-items: center;
  border-radius: 4px;
  color: var(--app-text);
  display: inline-flex;
  gap: 5px;
  min-height: 28px;
  padding: 3px 5px;
  text-decoration: none;
  transition: background-color 120ms var(--app-ease-out), color 120ms var(--app-ease-out);
}

.app-breadcrumbs__item:hover {
  background: var(--app-surface-soft);
  color: var(--app-text-strong);
}

.app-breadcrumbs__icon {
  color: var(--app-muted);
  font-size: 0.82rem;
}

.app-breadcrumbs__label,
.app-breadcrumbs__plain {
  color: inherit;
  font-family: var(--app-mono);
  font-size: 0.72rem;
  font-weight: 650;
  letter-spacing: 0;
  line-height: 1rem;
}
</style>
