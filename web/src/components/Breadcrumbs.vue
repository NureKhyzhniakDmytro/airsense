<template>
  <Breadcrumb :home="home" :model="items" class="app-breadcrumbs">
    <template #item="{ item, props }">
      <div>
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
import { onMounted, ref, watch } from "vue";
import { useRoute } from "vue-router";
import Breadcrumb from 'primevue/breadcrumb';
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
const breadcrumbFetchTimeoutMs = 2500;

const home: BreadcrumbItem = {
  param: "",
  paramValue: null,
  icon: 'pi pi-home',
  route: {
    name: 'dashboard'
  }
};

const getParamKeys = () => Object.keys(route.params);

const getFallbackLabel = (param: string) => {
  const config = breadcrumbConfig[param];
  const value = route.params[param];
  const normalizedValue = Array.isArray(value) ? value[0] : value;

  if (config?.label) return config.label;
  return normalizedValue ? `${param}: ${normalizedValue}` : param;
};

const getItemRoute = (param: string, params: Record<string, any>) => {
  const config = breadcrumbConfig[param];

  return config?.path
    ? {
        name: config.path,
        params,
      }
    : undefined;
};

const createFallbackItems = (): BreadcrumbItem[] => {
  const params = { ...route.params };

  return getParamKeys().map((param) => ({
    param,
    paramValue: route.params[param],
    label: getFallbackLabel(param),
    route: getItemRoute(param, params),
    isLoading: false,
  }));
};

const withTimeout = async <T,>(promise: Promise<T>, timeoutMs: number): Promise<T> => {
  let timeoutId: ReturnType<typeof setTimeout> | undefined;

  return Promise.race([
    promise.finally(() => {
      if (timeoutId) clearTimeout(timeoutId);
    }),
    new Promise<T>((_, reject) => {
      timeoutId = setTimeout(() => reject(new Error("Breadcrumb request timed out")), timeoutMs);
    }),
  ]);
};

const fetchBreadcrumbData = async (param: string): Promise<Partial<BreadcrumbItem>> => {
  const config = breadcrumbConfig[param];
  if (!config) {
    return {
      label: getFallbackLabel(param),
    };
  }

  const params = { ...route.params };
  const fallback = {
    label: getFallbackLabel(param),
    route: getItemRoute(param, params),
  };

  try {
    return {
      label: await withTimeout(config.fetchData(params), breadcrumbFetchTimeoutMs),
      route: fallback.route,
    };
  } catch {
    return fallback;
  }
};

const resolveBreadcrumbItems = async (): Promise<BreadcrumbItem[]> => {
  const paramKeys = getParamKeys();
  const resolvedItems = await Promise.all(paramKeys.map(async (param) => {
    const item = await fetchBreadcrumbData(param);

    return {
      param,
      paramValue: route.params[param],
      label: item.label,
      icon: item.icon,
      route: item.route,
      isLoading: false,
    };
  }));

  return resolvedItems;
};

const items = ref<BreadcrumbItem[]>(createFallbackItems());
let refreshRequestId = 0;

const refreshBreadcrumbs = async () => {
  const requestId = ++refreshRequestId;
  items.value = createFallbackItems();
  const resolvedItems = await resolveBreadcrumbItems();

  if (requestId === refreshRequestId) {
    items.value = resolvedItems;
  }
};

onMounted(() => {
  refreshBreadcrumbs();
});

watch(
  () => route.fullPath,
  () => {
    refreshBreadcrumbs();
  },
);
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
