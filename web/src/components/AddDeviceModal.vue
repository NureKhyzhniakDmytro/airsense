<template>
  <transition name="modal-fade">
    <div
        v-if="isOpen"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50"
        @click.self="close"
    >
      <div class="bg-white p-6 rounded-lg shadow-lg w-full max-w-md">
        <h2 class="text-xl font-semibold text-gray-800">Додати пристрій</h2>

        <input
            v-model="serialNumber"
            type="text"
            placeholder="Серійний номер пристрою"
            class="mt-4 w-full p-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 bg-gray-50"
            @keyup.enter="submit"
        />

        <p v-if="errorMessage" class="text-sm text-red-600 mt-2">{{ errorMessage }}</p>

        <div class="flex justify-end gap-2 mt-6">
          <button
              @click="close"
              class="px-4 py-2 bg-gray-300 text-gray-800 rounded-md hover:bg-gray-400 transition"
          >
            Скасувати
          </button>
          <button
              @click="submit"
              :disabled="isLoading"
              class="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition disabled:opacity-50"
          >
            {{ isLoading ? "Додавання..." : "Додати" }}
          </button>
        </div>
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import { ref, defineProps, defineEmits, watch, onUnmounted } from "vue";
import api from "@/api";

const props = defineProps<{
  isOpen: boolean;
  roomId: number;
}>();

const emit = defineEmits(["close", "added"]);

const serialNumber = ref("");
const isLoading = ref(false);
const errorMessage = ref("");

const close = () => {
  emit("close");
  serialNumber.value = "";
  errorMessage.value = "";
};

const submit = async () => {
  if (!serialNumber.value.trim()) {
    errorMessage.value = "Будь ласка, введіть серійний номер.";
    return;
  }

  isLoading.value = true;
  errorMessage.value = "";

  try {
    await api.post(`/room/${props.roomId}/device`, {
      serial_number: serialNumber.value.trim(),
    });

    emit("added");
    close();
  } catch (error) {
    errorMessage.value = "Не вдалося додати пристрій.";
    console.error("Помилка додавання пристрою:", error);
  } finally {
    isLoading.value = false;
  }
};

const handleEscape = (e: KeyboardEvent) => {
  if (e.key === "Escape") close();
};

watch(
    () => props.isOpen,
    (open) => {
      if (open) document.addEventListener("keydown", handleEscape);
      else document.removeEventListener("keydown", handleEscape);
    }
);

onUnmounted(() => document.removeEventListener("keydown", handleEscape));
</script>

<style scoped>
.modal-fade-enter-active,
.modal-fade-leave-active {
  transition: opacity 0.2s ease;
}
.modal-fade-enter-from,
.modal-fade-leave-to {
  opacity: 0;
}
</style>
