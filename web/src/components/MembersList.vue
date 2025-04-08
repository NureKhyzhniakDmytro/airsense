<template>
  <div class="p-6 bg-white shadow-md rounded-lg">
    <!-- Заголовок -->
    <div class="flex justify-between items-center mb-4">
      <div>
        <h2 class="text-xl font-semibold text-gray-800">Учасники</h2>
        <p class="text-gray-500 text-sm">Список всіх користувачів у вашому середовищі.</p>
      </div>
      <!-- Кнопка добавления участника -->
      <button
          v-if="!isUser"
          @click="isInviteModalOpen = true"
          class="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition"
      >
        Додати учасника
      </button>
    </div>

    <!-- Skeleton Loader -->
    <div v-if="isLoading" class="mt-4 space-y-2">
      <div v-for="i in pagination.count" :key="i" class="h-12 bg-gray-200 animate-pulse rounded"></div>
    </div>

    <!-- Сообщение, если участников нет -->
    <p v-else-if="members.length === 0" class="text-gray-500 mt-4 text-center">
      Поки немає учасників
    </p>

    <!-- Таблица участников -->
    <div v-else class="overflow-x-auto mt-4">
      <table class="w-full border border-gray-300 rounded-lg shadow-sm border-separate border-spacing-0 overflow-hidden">
        <thead class="bg-gray-100 text-gray-700 rounded-t-lg">
        <tr>
          <th class="p-3 text-left border-b">Ім'я</th>
          <th class="p-3 text-left border-b">Email</th>
          <th class="p-3 text-left border-b">Роль</th>
          <th v-if="!isUser" class="p-3 text-left border-b">Дії</th>
        </tr>
        </thead>
        <tbody>
        <tr v-for="user in members" :key="user.id" class="hover:bg-gray-50">
          <td class="p-3 border-b font-medium">{{ user.name || "—" }}</td>
          <td class="p-3 border-b">{{ user.email }}</td>
          <td class="p-3 border-b">
            <span
                class="px-3 py-1 rounded-full text-xs font-medium"
                :class="getRoleBadge(user.role)"
            >
              {{ user.role }}
            </span>
          </td>
          <td v-if="!isUser && canEditOrDelete(user)" class="p-3 border-b flex gap-3">
            <button @click="editUser(user)" class="text-blue-600 hover:underline">Редагувати</button>
            <button @click="confirmDelete(user)" class="text-red-600 hover:underline">Видалити</button>
          </td>
          <td v-else class="p-3 border-b flex gap-3">
            <span class="text-gray-600">Немає дій</span>
          </td>
        </tr>
        </tbody>
      </table>
    </div>

    <!-- Пагинация -->
    <div v-if="!isLoading && pagination.total > pagination.count" class="mt-6 flex justify-center">
      <Pagination
          :totalPages="totalPages"
          :currentPage="currentPage"
          @prev="prevPage"
          @next="nextPage"
          @page-change="goToPage"
      />
    </div>

    <!-- Модальное окно добавления участника -->
    <InviteMemberModal
        :isOpen="isInviteModalOpen"
        :envId="envId"
        @close="isInviteModalOpen = false"
        @added="loadMembers"
    />

    <!-- Модальное окно подтверждения удаления -->
    <div v-if="isDeleteModalOpen" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center">
      <div class="bg-white p-6 rounded-lg shadow-lg max-w-sm text-center">
        <h3 class="text-lg font-semibold text-gray-800">Видалити користувача?</h3>
        <p class="text-gray-600 text-sm mt-2">
          Ви впевнені, що хочете видалити {{ userToDelete?.name || userToDelete?.email }}?
        </p>
        <div class="mt-4 flex gap-4 justify-center">
          <button @click="deleteUser" class="bg-red-600 text-white px-4 py-2 rounded-md hover:bg-red-700">Так, видалити</button>
          <button @click="isDeleteModalOpen = false" class="bg-gray-300 px-4 py-2 rounded-md hover:bg-gray-400">Скасувати</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRoute } from "vue-router";
import { getMembers, removeUser } from "@/services/apiService";
import type { User, Environment } from "@/services/apiService";
import Pagination from "@/components/Pagination.vue";
import { getRoleBadge } from "@/utils/environment";
import { useEnvironmentStore } from "@/store/environmentStore";
import { useAuthStore } from "@/store/authStore";
import InviteMemberModal from "@/components/InviteMemberModal.vue";

const route = useRoute();
const authStore = useAuthStore();
const members = ref<User[]>([]);
const pagination = ref({ total: 0, skip: 0, count: 5 });
const environment = ref<Environment | null>(null);
const environmentStore = useEnvironmentStore();
const envId = Number(route.params.envId);
const isLoading = ref(true);
const isInviteModalOpen = ref(false);

const currentUser = computed(() => authStore.user);
const isUser = computed(() => environment.value?.role === "user");

const isDeleteModalOpen = ref(false);
const userToDelete = ref<User | null>(null);

const loadMembers = async () => {
  isLoading.value = true;
  if (!environment.value) return;

  const { members: memberList, pagination: pag } = await getMembers(
      environment.value.id,
      pagination.value.skip,
      pagination.value.count
  );
  members.value = memberList;
  pagination.value.total = pag.total;
  isLoading.value = false;
};

const canEditOrDelete = (user: User) => {
  return user.role !== "owner" && user.email !== currentUser.value?.email;
};

const totalPages = computed(() => Math.ceil(pagination.value.total / pagination.value.count));
const currentPage = computed(() => Math.floor(pagination.value.skip / pagination.value.count) + 1);

const prevPage = () => {
  if (pagination.value.skip > 0) {
    pagination.value.skip -= pagination.value.count;
    loadMembers();
  }
};

const nextPage = () => {
  if (pagination.value.skip + pagination.value.count < pagination.value.total) {
    pagination.value.skip += pagination.value.count;
    loadMembers();
  }
};

const goToPage = (page: number) => {
  pagination.value.skip = (page - 1) * pagination.value.count;
  loadMembers();
};

const editUser = (user: User) => {
  console.log("Редагування користувача:", user);
};

const confirmDelete = (user: User) => {
  userToDelete.value = user;
  isDeleteModalOpen.value = true;
};

const deleteUser = async () => {
  if (!userToDelete.value) return;

  try {
    await removeUser(envId, userToDelete.value.id);
    members.value = members.value.filter(user => user.id !== userToDelete.value!.id);
  } catch (error) {
    console.error("Помилка видалення користувача:", (error as Error).message);
  }

  isDeleteModalOpen.value = false;
};

onMounted(async () => {
  environment.value = await environmentStore.fetchEnvironment(envId);
  await loadMembers();
});
</script>
