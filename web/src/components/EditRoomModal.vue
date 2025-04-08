<template>
  <div class="fixed inset-0 flex items-center justify-center bg-black bg-opacity-50 z-50" @click.self="close">
    <div class="bg-white rounded-lg shadow-lg w-96 p-6">
      <h2 class="text-xl font-semibold text-gray-900">Редагувати кімнату</h2>

      <input
          v-model="name"
          type="text"
          placeholder="Назва кімнати"
          class="mt-4 w-full p-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 bg-gray-100"
          @keyup.enter="updateRoom"
      />

      <p v-if="errorMessage" class="text-red-500 text-sm mt-2">{{ errorMessage }}</p>

      <div class="mt-6 flex justify-end gap-2">
        <button @click="close" class="px-4 py-2 bg-gray-300 text-gray-700 rounded-lg hover:bg-gray-400 transition">
          Скасувати
        </button>
        <button
            @click="updateRoom"
            :disabled="isLoading"
            class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition disabled:opacity-50"
        >
          {{ isLoading ? "Оновлення..." : "Оновити" }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, defineProps, defineEmits, watch, onUnmounted } from "vue";
import { updateRoom } from "@/services/apiService";
import type { Room } from "@/services/apiService";

const props = defineProps<{ room: Room | null }>();
const emit = defineEmits(["close", "updated"]);

const name = ref(props.room?.name || "");
const isLoading = ref(false);
const errorMessage = ref("");

const close = () => {
  emit("close");
  name.value = props.room?.name || "";
  errorMessage.value = "";
};

const updateRoom = async () => {
  if (!name.value.trim()) {
    errorMessage.value = "Назва кімнати не може бути порожньою.";
    return;
  }

  isLoading.value = true;
  errorMessage.value = "";

  try {
    await updateRoom(props.room!.id, { name: name.value.trim() });
    emit("updated");
    close();
  } catch (error) {
    errorMessage.value = "Помилка оновлення кімнати.";
  } finally {
    isLoading.value = false;
  }
};

const handleKeydown = (event: KeyboardEvent) => {
  if (event.key === "Escape") close();
};

watch(() => props.room, (newRoom) => {
  if (newRoom) {
    name.value = newRoom.name;
  }
});

onUnmounted(() => document.removeEventListener("keydown", handleKeydown));
</script>
