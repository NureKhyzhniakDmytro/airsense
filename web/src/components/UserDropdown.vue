<template>
  <div ref="menuContainer" class="relative">
    <button @click.stop="toggleMenu" class="focus:outline-none">
      <img src="/avatar.svg" alt="User" class="w-10 h-10 rounded-full border cursor-pointer" />
    </button>

    <!-- Выпадающее меню -->
    <transition name="dropdown">
      <div v-if="menuOpen" ref="menuRef" class="absolute right-0 mt-2 w-48 bg-white shadow-lg rounded-lg border border-gray-200">
        <ul class="py-2 text-gray-700">
          <li>
            <router-link to="/profile" class="flex items-center gap-2 px-4 py-2 hover:bg-gray-100">
              <UserIcon class="w-5 h-5 text-gray-500" />
              Мій профіль
            </router-link>
          </li>
          <li>
            <button @click="logout" class="w-full flex items-center gap-2 text-left px-4 py-2 hover:bg-gray-100">
              <ArrowLeftOnRectangleIcon class="w-5 h-5 text-gray-500" />
              Вийти
            </button>
          </li>
        </ul>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from "vue";
import { UserIcon, ArrowLeftOnRectangleIcon } from "@heroicons/vue/24/outline";
import { useAuthStore } from "@/store/authStore";
import { useRouter } from "vue-router";

const authStore = useAuthStore();
const router = useRouter();
const menuOpen = ref(false);
const menuContainer = ref<HTMLElement | null>(null);

const toggleMenu = () => {
  menuOpen.value = !menuOpen.value;
};

const handleClickOutside = (event: MouseEvent) => {
  if (menuContainer.value && !menuContainer.value.contains(event.target as Node)) {
    menuOpen.value = false;
  }
};

onMounted(() => document.addEventListener("click", handleClickOutside));
onUnmounted(() => document.removeEventListener("click", handleClickOutside));

const logout = async () => {
  await authStore.logout();
  await router.push("/login");
};
</script>

<style scoped>
.dropdown-enter-active,
.dropdown-leave-active {
  transition: transform 0.15s ease-out, opacity 0.15s ease-out;
}
.dropdown-enter-from,
.dropdown-leave-to {
  transform: scale(0.95);
  opacity: 0;
}
</style>
