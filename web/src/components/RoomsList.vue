<template>
  <div>
    <div class="flex justify-between items-center">
      <h2 class="text-xl font-semibold text-gray-800">Кімнати</h2>

      <button
          @click="isCreateModalOpen = true"
          class="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition"
      >
        Додати кімнату
      </button>
    </div>

    <!-- Skeleton Loader -->
    <div v-if="isLoading" class="mt-4 space-y-2">
      <div v-for="i in pagination.count" :key="i" class="h-12 bg-gray-200 animate-pulse rounded"></div>
    </div>

    <!-- Сообщение, если комнат нет -->
    <div v-else-if="rooms.length === 0" class="text-center mt-10">
      <p class="text-gray-500 text-lg">Поки немає кімнат</p>
    </div>

    <!-- Список комнат -->
    <ul v-else class="mt-2 border border-gray-300 rounded-lg divide-y divide-gray-300">
      <li v-for="room in rooms" :key="room.id" class="p-4 hover:bg-gray-100 transition relative">
        <div class="flex justify-between items-center">
          <span class="text-lg font-medium text-gray-800 cursor-pointer" @click="goToRoom(room)">
            {{ room.name }}
          </span>

          <!-- Действия -->
          <div class="relative">
            <button @click.stop="toggleMenu(room.id)" class="text-gray-500 hover:text-gray-700">
              <EllipsisVerticalIcon class="w-5 h-5" />
            </button>

            <div v-if="activeMenu === room.id" class="absolute right-0 bg-white border border-gray-300 rounded-lg shadow-lg z-10">
              <button @click="editRoom(room)" class="block w-full text-left px-4 py-2 hover:bg-gray-100">Редагувати</button>
              <button @click="confirmDelete(room)" class="block w-full text-left px-4 py-2 text-red-600 hover:bg-red-100">Видалити</button>
            </div>
          </div>
        </div>

        <!-- Скорость вентиляции -->
        <div v-if="room.device_speed !== undefined" class="mt-2 text-sm text-gray-600">
          Швидкість вентиляції:
          <span class="font-medium text-gray-800">{{ room.device_speed }}% |</span>
        </div>
        
        <!-- Параметры комнаты -->
        <div v-if="room.parameters" class="mt-2 flex flex-wrap gap-2">
          <div
              v-for="param in room.parameters"
              :key="param.name"
              class="bg-gray-200 px-2 py-1 rounded-lg text-sm text-gray-700"
          >
            <span class="font-medium">{{ getParameterLabel(param.name) }}:</span>
            <span class="ml-1">{{ param.value }}{{ param.unit }}</span>
          </div>
        </div>
      </li>
    </ul>

    <!-- Пагинация -->
    <div v-if="!isLoading && pagination.total > pagination.count" class="mt-4 flex justify-center">
      <Pagination :totalPages="totalPages" :currentPage="currentPage" @prev="prevPage" @next="nextPage" @page-change="goToPage" />
    </div>

    <!-- Модальное окно создания комнаты -->
    <CreateRoomModal v-if="isCreateModalOpen" :isOpen="isCreateModalOpen" :envId="envId" @close="isCreateModalOpen = false" @added="loadRooms" />

    <!-- Модальное окно редактирования комнаты -->
    <EditRoomModal v-if="isEditModalOpen" :room="selectedRoom" @close="isEditModalOpen = false" @updated="loadRooms" />

    <!-- Модальное окно подтверждения удаления -->
    <ConfirmModal
        v-if="isDeleteModalOpen"
        :title="'Видалити кімнату?'"
        :message="`Ви впевнені, що хочете видалити '${roomToDelete?.name}'?`"
        :confirmText="'Так, видалити'"
        :cancelText="'Скасувати'"
        :onConfirm="() => deleteRoom(roomToDelete)"
        @close="isDeleteModalOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useEnvironmentStore } from "@/store/environmentStore";
import { getRooms, removeRoom } from "@/services/apiService";
import { EllipsisVerticalIcon } from "@heroicons/vue/24/outline";
import type { Room, Environment } from "@/services/apiService";
import Pagination from "@/components/Pagination.vue";
import CreateRoomModal from "@/components/CreateRoomModal.vue";
import EditRoomModal from "@/components/EditRoomModal.vue";
import ConfirmModal from "@/components/ConfirmModal.vue";

const router = useRouter();
const route = useRoute();
const environmentStore = useEnvironmentStore();
const envId = Number(route.params.envId);
const rooms = ref<Room[]>([]);
const pagination = ref({ total: 0, skip: 0, count: 5 });
const isLoading = ref(true);
const environment = ref<Environment>();
const isCreateModalOpen = ref(false);
const isEditModalOpen = ref(false);
const isDeleteModalOpen = ref(false);
const selectedRoom = ref<Room | null>(null);
const activeMenu = ref<number | null>(null);
let refreshInterval: ReturnType<typeof setInterval> | null = null;

const totalPages = computed(() => Math.ceil(pagination.value.total / pagination.value.count));
const currentPage = computed(() => Math.floor(pagination.value.skip / pagination.value.count) + 1);

const loadRooms = async (withLoading: boolean = true) => {
  if (!environment.value) return;

  isLoading.value = withLoading;
  const { rooms: roomList, pagination: pag } = await getRooms(envId, pagination.value.skip, pagination.value.count);
  rooms.value = roomList;
  pagination.value = pag;
  isLoading.value = false;
};

const getParameterLabel = (name: string) => {
  const labels: Record<string, string> = {
    temperature: "Температура",
    humidity: "Вологість",
    co2: "CO₂",
  };
  return labels[name] || name;
};

const toggleMenu = (roomId: number) => {
  activeMenu.value = activeMenu.value === roomId ? null : roomId;
};

const editRoom = (room: Room) => {
  selectedRoom.value = room;
  isEditModalOpen.value = true;
  activeMenu.value = null;
};

const confirmDelete = (room: Room) => {
  selectedRoom.value = room;
  isDeleteModalOpen.value = true;
  activeMenu.value = null;
};

const startAutoRefresh = () => {
  refreshInterval = setInterval(() => loadRooms(false), 10_000);
};

const stopAutoRefresh = () => {
  if (refreshInterval) {
    clearInterval(refreshInterval);
    refreshInterval = null;
  }
};

const prevPage = () => {
  if (pagination.value.skip > 0) {
    pagination.value.skip -= pagination.value.count;
    loadRooms();
  }
};

const nextPage = () => {
  if (pagination.value.skip + pagination.value.count < pagination.value.total) {
    pagination.value.skip += pagination.value.count;
    loadRooms();
  }
};

const goToPage = (page: number) => {
  pagination.value.skip = (page - 1) * pagination.value.count;
  loadRooms();
};

const goToRoom = (room: Room) => {
  router.push(`/env/${envId}/room/${room.id}`);
};

const deleteRoom = async (room: Room) => {
  try {
    await removeRoom(environment.value.id, room.id);
  } catch (error) {
    console.error("Помилка видалення кімнати:", (error as Error).message);
  }
};

watch(() => pagination.value.skip, () => loadRooms());

onMounted(async () => {
  if (!environment.value) {
    environment.value = await environmentStore.fetchEnvironment(envId);
  }
  await loadRooms();
  startAutoRefresh();
});

onUnmounted(stopAutoRefresh);
</script>
