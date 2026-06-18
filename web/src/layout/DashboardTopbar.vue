<template>
  <aside class="app-sidebar" aria-label="Application navigation">
    <div class="app-sidebar__top">
      <router-link to="/dashboard" class="app-sidebar__brand" aria-label="AirSense dashboard">
        <img src="/logo.svg" alt="" class="app-sidebar__logo" />
        <span>AirSense</span>
      </router-link>

    </div>

    <nav class="app-sidebar__nav" aria-label="Main navigation">
      <router-link
        v-for="item in mainMenu"
        :key="item.name"
        :to="item.path"
        class="app-sidebar__link"
        :class="{ 'app-sidebar__link--active': isActive(item.path, item.exact) }"
        :aria-label="item.name"
      >
        <i :class="item.icon" />
        <span>{{ item.name }}</span>
      </router-link>
    </nav>

    <nav v-if="contextMenu.length" class="app-sidebar__nav app-sidebar__nav--context" aria-label="Context navigation">
      <span class="app-sidebar__section-label">{{ contextLabel }}</span>
      <router-link
        v-for="item in contextMenu"
        :key="item.name"
        :to="item.path"
        class="app-sidebar__link"
        :class="{ 'app-sidebar__link--active': isActive(item.path, item.exact) }"
        :aria-label="item.name"
      >
        <i :class="item.icon" />
        <span>{{ item.name }}</span>
      </router-link>
    </nav>

    <div class="app-sidebar__spacer" />

    <div class="app-sidebar__account">
      <Button
        icon="pi pi-bell"
        severity="secondary"
        variant="text"
        aria-label="Notifications"
        class="app-sidebar__icon-button"
      />

      <Button
        severity="secondary"
        variant="text"
        class="app-sidebar__profile"
        aria-haspopup="true"
        aria-controls="profile_menu"
        @click="toggle"
      >
        <Avatar icon="pi pi-user" shape="circle" />
        <span class="app-sidebar__email">{{ authStore.currentUserEmail || 'Account' }}</span>
      </Button>
      <Menu ref="profileMenu" :model="items" id="profile_menu" :popup="true" />
    </div>
  </aside>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useAuthStore } from "@/store/authStore";
import Button from 'primevue/button';
import Avatar from 'primevue/avatar';
import Menu from 'primevue/menu';
import type { MenuItem, MenuItemCommandEvent } from 'primevue/menuitem';

const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();

type SidebarItem = {
  name: string;
  path: string;
  icon: string;
  exact?: boolean;
};

const mainMenu: SidebarItem[] = [
  { name: "Dashboard", path: "/dashboard", icon: "pi pi-th-large", exact: true },
];

const envId = computed(() => route.params.envId ? Number(route.params.envId) : null);
const roomId = computed(() => route.params.roomId ? Number(route.params.roomId) : null);

const contextLabel = computed(() => roomId.value ? "Room" : "Environment");

const contextMenu = computed<SidebarItem[]>(() => {
  if (!envId.value) return [];

  if (roomId.value) {
    return [
      { name: "Telemetry", path: `/env/${envId.value}/room/${roomId.value}/parameters`, icon: "pi pi-chart-line", exact: true },
      { name: "Layout", path: `/env/${envId.value}/room/${roomId.value}/layout`, icon: "pi pi-map", exact: true },
      { name: "Sensors", path: `/env/${envId.value}/room/${roomId.value}/sensors`, icon: "pi pi-bullseye" },
      { name: "Devices", path: `/env/${envId.value}/room/${roomId.value}/devices`, icon: "pi pi-slack" },
      { name: "Automation", path: `/env/${envId.value}/room/${roomId.value}/settings`, icon: "pi pi-cog", exact: true },
    ];
  }

  return [
    { name: "Rooms", path: `/dashboard/env/${envId.value}/rooms`, icon: "pi pi-list", exact: true },
    { name: "Members", path: `/dashboard/env/${envId.value}/members`, icon: "pi pi-users", exact: true },
  ];
});

const isActive = (path: string, exact = false) => (
  exact
    ? route.path === path
    : route.path === path || route.path.startsWith(`${path}/`)
);

const logout = (_event: MenuItemCommandEvent) => {
  async function _logout() {
    profileMenu.value?.hide();
    await authStore.logout();
    await router.push("/login");
  }

  _logout().then();
};

const profileMenu = ref();
const items = ref<MenuItem[]>([
  {
    label: 'Profile',
    icon: 'pi pi-user'
  },
  {
    label: 'Log out',
    icon: 'pi pi-sign-out',
    command: logout
  }
]);

const toggle = (event: Event) => {
  profileMenu.value.toggle(event);
};
</script>

<style scoped>
.app-sidebar {
  background:
    linear-gradient(180deg, var(--app-sidebar-bg) 0%, var(--app-sidebar-bg-strong) 100%);
  border-right: 1px solid var(--app-sidebar-border);
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-height: 0;
  min-width: 0;
  overflow: hidden;
  padding: 12px;
}

.app-sidebar__top {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.app-sidebar__brand {
  align-items: center;
  color: var(--app-sidebar-text);
  display: inline-flex;
  gap: 10px;
  font-weight: 800;
  min-height: 42px;
  text-decoration: none;
}

.app-sidebar__logo {
  background: var(--app-surface-raised);
  border-radius: 5px;
  height: 36px;
  object-fit: contain;
  padding: 3px;
  width: 36px;
}

.app-sidebar__nav {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.app-sidebar__nav--context {
  border-top: 1px solid var(--app-sidebar-subtle-strong);
  padding-top: 12px;
}

.app-sidebar__section-label {
  color: color-mix(in srgb, var(--app-sidebar-text) 48%, transparent);
  font-family: var(--app-mono);
  font-size: 0.72rem;
  font-weight: 800;
  letter-spacing: 0;
  line-height: 1rem;
  padding: 0 8px;
  text-transform: uppercase;
}

.app-sidebar__link {
  align-items: center;
  border: 1px solid transparent;
  border-radius: 5px;
  color: var(--app-sidebar-muted);
  display: flex;
  gap: 8px;
  min-height: 42px;
  padding: 9px 10px;
  text-decoration: none;
  transition:
    background-color 140ms ease,
    border-color 140ms ease,
    color 140ms ease,
    transform 120ms ease;
}

.app-sidebar__link i {
  flex: 0 0 auto;
}

@media (hover: hover) and (pointer: fine) {
  .app-sidebar__link:hover {
    background: var(--app-sidebar-subtle);
    border-color: var(--app-sidebar-subtle-strong);
    color: var(--app-sidebar-text);
  }
}

.app-sidebar__link:active {
  transform: scale(0.985);
}

.app-sidebar__link--active {
  background: color-mix(in srgb, var(--app-primary) 26%, transparent);
  border-color: color-mix(in srgb, var(--app-primary) 56%, transparent);
  color: var(--app-sidebar-active);
}

.app-sidebar__spacer {
  flex: 1;
  min-height: 12px;
}

.app-sidebar__account {
  border-top: 1px solid var(--app-sidebar-subtle-strong);
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding-top: 12px;
}

.app-sidebar__icon-button,
.app-sidebar__profile {
  color: var(--app-sidebar-muted);
  justify-content: flex-start;
  width: 100%;
}

.app-sidebar__icon-button:hover,
.app-sidebar__profile:hover {
  background: var(--app-sidebar-subtle);
  color: var(--app-sidebar-text);
}

.app-sidebar__profile {
  gap: 8px;
  min-width: 0;
}

.app-sidebar__email {
  color: color-mix(in srgb, var(--app-sidebar-text) 62%, transparent);
  font-size: 0.875rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (max-width: 560px) {
  .app-sidebar {
    border-right: 1px solid var(--app-sidebar-border);
    gap: 10px;
    overflow: hidden auto;
    padding: 10px;
  }

  .app-sidebar__top {
    align-items: center;
    flex-direction: column;
  }

  .app-sidebar__brand {
    justify-content: center;
    min-height: 40px;
    width: 100%;
  }

  .app-sidebar__brand span {
    display: none;
  }

  .app-sidebar__logo {
    height: 34px;
    width: 34px;
  }

  .app-sidebar__nav,
  .app-sidebar__nav--context {
    flex-direction: column;
    overflow: visible;
    padding-bottom: 0;
  }

  .app-sidebar__nav--context {
    border-top: 1px solid var(--app-sidebar-subtle-strong);
    padding-top: 10px;
  }

  .app-sidebar__section-label,
  .app-sidebar__spacer,
  .app-sidebar__icon-button {
    display: none;
  }

  .app-sidebar__link {
    justify-content: center;
    min-height: 40px;
    padding: 8px 0;
    width: 100%;
  }

  .app-sidebar__link span {
    display: none;
  }

  .app-sidebar__account {
    border-top: 0;
    padding-top: 0;
  }

  .app-sidebar__profile {
    justify-content: center;
    padding-left: 0;
    padding-right: 0;
    width: 100%;
  }

  .app-sidebar__email {
    display: none;
  }
}
</style>
