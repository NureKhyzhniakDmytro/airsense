<template>
  <header class="bg-surface-0 shadow py-4">
    <div class="max-w-7xl mx-auto flex items-center justify-between">
      <div class="flex items-center gap-6">
        <router-link to="/">
          <img src="/logo.svg" alt="Logo" class="h-10 w-10 object-contain rounded-full shadow-sm" />
        </router-link>

        <nav class="flex gap-4 text-color">
          <router-link
              v-for="item in menu"
              :key="item.name"
              :to="item.path"
              class="relative py-2 px-3 font-medium hover:text-color transition"
              :class="{ 'text-color font-semibold': isActive(item.path) }"
          >
            {{ item.name }}
            <span v-if="isActive(item.path)" class="absolute left-0 bottom-0 w-full h-0.5 bg-primary"></span>
          </router-link>
        </nav>
      </div>

      <div class="flex items-center gap-4 relative">
        <div>
          <Button @click="createEnvironmentDialog = true" label="New Environment" icon="pi pi-plus" />
          <create-environment-dialog v-model="createEnvironmentDialog"/>
        </div>
        <Button
            icon="pi pi-bell"
            severity="secondary"
            variant="text"
            rounded
            aria-label="Notification"
            class="h-full hover:text-color-emphasis"
            iconClass="text-xl"
        />
        <div>
          <Avatar icon="pi pi-user" class="size-[42px] hover:bg-surface-100 active:bg-surface-300" shape="circle" @click.nativa="toggle" />
          <Menu ref="profileMenu" :model="items" id="overlay_menu" :popup="true" />
        </div>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useAuthStore } from "@/store/authStore";
import Button from 'primevue/button';
import Avatar from 'primevue/avatar';
import Menu from 'primevue/menu';
import type { MenuItem, MenuItemCommandEvent } from 'primevue/menuitem';
import CreateEnvironmentDialog from "@/components/evnironment/CreateEnvironmentDialog.vue"

const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();
const createEnvironmentDialog = ref(false);

const menu = [{ name: "Dashboard", path: "/" }];
const isActive = (path: string) => route.path === path || route.path.startsWith(path);

const logout = (event: MenuItemCommandEvent) => {
  async function _logout() {
    profileMenu.value.hide();
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
