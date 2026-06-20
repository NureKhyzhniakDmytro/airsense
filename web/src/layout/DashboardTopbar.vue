<template>
  <aside class="app-sidebar" aria-label="Application navigation">
    <div class="app-sidebar__top">
      <router-link to="/dashboard" class="app-sidebar__brand" aria-label="AirSense dashboard">
        <img src="/logo.svg" alt="" class="app-sidebar__logo" />
        <span>AirSense</span>
      </router-link>

    </div>

    <nav class="app-sidebar__tree" aria-label="Workspace navigation">
      <router-link
        to="/dashboard"
        class="app-sidebar__tree-node app-sidebar__tree-node--root"
        :class="{ 'app-sidebar__tree-node--active': isActive('/dashboard', true) }"
        aria-label="Dashboard"
      >
        <i class="pi pi-th-large" />
        <span>Dashboard</span>
      </router-link>

      <router-link
        to="/dashboard/demo-data"
        class="app-sidebar__tree-node app-sidebar__tree-node--root"
        :class="{ 'app-sidebar__tree-node--active': isActive('/dashboard/demo-data', true) }"
        aria-label="Demo data"
      >
        <i class="pi pi-database" />
        <span>Demo data</span>
      </router-link>

      <div v-if="envId" class="app-sidebar__tree-branch">
        <router-link
          :to="environmentRootPath"
          class="app-sidebar__tree-node app-sidebar__tree-node--parent"
          :class="{ 'app-sidebar__tree-node--active': isEnvironmentActive }"
          :aria-label="`Environment ${envId}`"
        >
          <i class="pi pi-building" />
          <span>ENV-{{ envId }}</span>
        </router-link>

        <div class="app-sidebar__tree-children">
          <router-link
            v-for="item in environmentMenu"
            :key="item.name"
            :to="item.path"
            class="app-sidebar__tree-node app-sidebar__tree-node--child"
            :class="{ 'app-sidebar__tree-node--active': isActive(item.path, item.exact) }"
            :aria-label="item.name"
          >
            <i :class="item.icon" />
            <span>{{ item.name }}</span>
          </router-link>

          <div v-if="roomId" class="app-sidebar__tree-branch app-sidebar__tree-branch--room">
            <router-link
              :to="roomRootPath"
              class="app-sidebar__tree-node app-sidebar__tree-node--parent"
              :class="{ 'app-sidebar__tree-node--active': isRoomActive }"
              :aria-label="`Room ${roomId}`"
            >
              <i class="pi pi-home" />
              <span>ROOM-{{ roomId }}</span>
            </router-link>

            <div class="app-sidebar__tree-children app-sidebar__tree-children--room">
              <router-link
                v-for="item in roomMenu"
                :key="item.name"
                :to="item.path"
                class="app-sidebar__tree-node app-sidebar__tree-node--child"
                :class="{ 'app-sidebar__tree-node--active': isActive(item.path, item.exact) }"
                :aria-label="item.name"
              >
                <i :class="item.icon" />
                <span>{{ item.name }}</span>
              </router-link>
            </div>
          </div>
        </div>
      </div>
    </nav>

    <div class="app-sidebar__spacer" />

    <div class="app-sidebar__account">
      <NotificationsPopover />

      <button
        type="button"
        class="app-sidebar__profile"
        aria-haspopup="true"
        aria-controls="profile_menu"
        @click="toggle"
      >
        <Avatar icon="pi pi-user" shape="circle" />
        <span class="app-sidebar__email">{{ authStore.currentUserEmail || 'Account' }}</span>
      </button>
      <Menu ref="profileMenu" :model="items" id="profile_menu" :popup="true" class="app-sidebar__profile-menu" />
    </div>
  </aside>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useAuthStore } from "@/store/authStore";
import Avatar from 'primevue/avatar';
import Menu from 'primevue/menu';
import type { MenuItem, MenuItemCommandEvent } from 'primevue/menuitem';
import NotificationsPopover from "@/components/notification/NotificationsPopover.vue";

const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();

type SidebarItem = {
  name: string;
  path: string;
  icon: string;
  exact?: boolean;
};

const envId = computed(() => route.params.envId ? Number(route.params.envId) : null);
const roomId = computed(() => route.params.roomId ? Number(route.params.roomId) : null);

const environmentRootPath = computed(() => (
  envId.value ? `/dashboard/env/${envId.value}/rooms` : "/dashboard"
));
const roomRootPath = computed(() => (
  envId.value && roomId.value
    ? `/env/${envId.value}/room/${roomId.value}/parameters`
    : environmentRootPath.value
));

const environmentMenu = computed<SidebarItem[]>(() => {
  if (!envId.value) return [];

  return [
    { name: "Rooms", path: `/dashboard/env/${envId.value}/rooms`, icon: "pi pi-list", exact: true },
    { name: "Members", path: `/dashboard/env/${envId.value}/members`, icon: "pi pi-users", exact: true },
  ];
});

const roomMenu = computed<SidebarItem[]>(() => {
  if (!envId.value || !roomId.value) return [];

  return [
    { name: "Telemetry", path: `/env/${envId.value}/room/${roomId.value}/parameters`, icon: "pi pi-chart-line", exact: true },
    { name: "Layout", path: `/env/${envId.value}/room/${roomId.value}/layout`, icon: "pi pi-map", exact: true },
    { name: "Sensors", path: `/env/${envId.value}/room/${roomId.value}/sensors`, icon: "pi pi-bullseye" },
    { name: "Devices", path: `/env/${envId.value}/room/${roomId.value}/devices`, icon: "pi pi-slack" },
    { name: "Automation", path: `/env/${envId.value}/room/${roomId.value}/settings`, icon: "pi pi-cog", exact: true },
  ];
});

const isEnvironmentActive = computed(() => (
  Boolean(envId.value) && (
    route.path.startsWith(`/dashboard/env/${envId.value}`) ||
    route.path.startsWith(`/env/${envId.value}`)
  )
));

const isRoomActive = computed(() => (
  Boolean(envId.value && roomId.value) &&
  route.path.startsWith(`/env/${envId.value}/room/${roomId.value}`)
));

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

.app-sidebar__tree {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.app-sidebar__tree-branch,
.app-sidebar__tree-children {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
  position: relative;
}

.app-sidebar__tree-branch {
  margin-left: 13px;
  padding-left: 13px;
}

.app-sidebar__tree-branch::before {
  background: var(--app-sidebar-subtle-strong);
  bottom: 18px;
  content: "";
  left: 0;
  position: absolute;
  top: -2px;
  width: 1px;
}

.app-sidebar__tree-node {
  align-items: center;
  border: 1px solid transparent;
  border-radius: 5px;
  color: var(--app-sidebar-muted);
  display: flex;
  gap: 8px;
  min-height: 36px;
  min-width: 0;
  padding: 7px 9px;
  position: relative;
  text-decoration: none;
  transition:
    background-color 140ms ease,
    border-color 140ms ease,
    color 140ms ease;
}

.app-sidebar__tree-node::before {
  background: var(--app-sidebar-subtle-strong);
  content: "";
  height: 1px;
  left: -13px;
  position: absolute;
  top: 50%;
  width: 13px;
}

.app-sidebar__tree-node--root::before {
  display: none;
}

.app-sidebar__tree-node--parent {
  color: color-mix(in srgb, var(--app-sidebar-text) 82%, transparent);
  font-family: var(--app-mono);
  font-size: 0.76rem;
  font-weight: 760;
}

.app-sidebar__tree-node--child {
  min-height: 34px;
  padding-left: 9px;
}

.app-sidebar__tree-node i {
  flex: 0 0 1rem;
  font-size: 0.95rem;
}

.app-sidebar__tree-node span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
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
    color 140ms ease;
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

  .app-sidebar__tree-node:hover {
    background: var(--app-sidebar-subtle);
    border-color: var(--app-sidebar-subtle-strong);
    color: var(--app-sidebar-text);
  }
}

.app-sidebar__link:active {
  transform: translateY(1px);
}

.app-sidebar__link--active {
  background: color-mix(in srgb, var(--app-primary) 26%, transparent);
  border-color: color-mix(in srgb, var(--app-primary) 56%, transparent);
  color: var(--app-sidebar-active);
}

.app-sidebar__tree-node--active {
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
  display: grid;
  grid-template-columns: 36px minmax(0, 1fr);
  gap: 8px;
  padding-top: 12px;
}

.app-sidebar__icon-button {
  color: var(--app-sidebar-muted);
  height: 36px;
  justify-content: center;
  min-height: 36px;
  padding: 0;
  width: 36px;
}

.app-sidebar__profile {
  align-items: center;
  background: transparent;
  border: 1px solid transparent;
  border-radius: var(--app-radius);
  color: var(--app-sidebar-muted);
  cursor: pointer;
  display: flex;
  font: inherit;
  gap: 8px;
  min-height: 36px;
  min-width: 0;
  padding: 5px 7px;
  text-align: left;
  transition:
    background-color 140ms var(--app-ease-out),
    border-color 140ms var(--app-ease-out),
    color 140ms var(--app-ease-out);
  width: 100%;
}

.app-sidebar__icon-button:hover,
.app-sidebar__profile:hover {
  background: var(--app-sidebar-subtle);
  color: var(--app-sidebar-text);
}

.app-sidebar__profile:active {
  transform: translateY(1px);
}

.app-sidebar__profile:focus-visible {
  outline: 2px solid color-mix(in srgb, var(--app-primary) 72%, white);
  outline-offset: 2px;
}

.app-sidebar__profile :deep(.p-avatar) {
  flex: 0 0 28px;
  height: 28px;
  min-width: 28px;
  overflow: hidden;
  width: 28px;
}

.app-sidebar__profile :deep(.p-avatar-icon) {
  font-size: 0.9rem;
}

.app-sidebar__profile-menu {
  grid-column: 1 / -1;
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
  .app-sidebar__nav--context,
  .app-sidebar__tree,
  .app-sidebar__tree-children {
    flex-direction: column;
    overflow: visible;
    padding-bottom: 0;
  }

  .app-sidebar__nav--context {
    border-top: 1px solid var(--app-sidebar-subtle-strong);
    padding-top: 10px;
  }

  .app-sidebar__section-label,
  .app-sidebar__spacer {
    display: none;
  }

  .app-sidebar__tree-branch {
    margin-left: 0;
    padding-left: 0;
  }

  .app-sidebar__tree-branch::before,
  .app-sidebar__tree-node::before {
    display: none;
  }

  .app-sidebar__link,
  .app-sidebar__tree-node {
    justify-content: center;
    min-height: 40px;
    padding: 8px 0;
    width: 100%;
  }

  .app-sidebar__link span,
  .app-sidebar__tree-node span {
    display: none;
  }

  .app-sidebar__account {
    border-top: 0;
    display: flex;
    flex-direction: column;
    padding-top: 0;
  }

  .app-sidebar__icon-button {
    display: inline-flex;
    height: 40px;
    min-height: 40px;
    width: 100%;
  }

  .app-sidebar__profile {
    justify-content: center;
    padding-left: 0;
    padding-right: 0;
    width: 100%;
  }

  .app-sidebar__profile :deep(.p-avatar) {
    flex-basis: 30px;
    height: 30px;
    min-width: 30px;
    width: 30px;
  }

  .app-sidebar__email {
    display: none;
  }
}
</style>
