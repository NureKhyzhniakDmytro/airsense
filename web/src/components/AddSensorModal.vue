<template>
  <div class="fixed inset-0 bg-black bg-opacity-50 flex justify-center items-center z-50" @click.self="close">
    <div class="bg-white p-6 rounded-lg w-96 shadow-lg">
      <h2 class="text-lg font-semibold text-gray-800 mb-4">Додати сенсор</h2>

      <input
          v-model="serial"
          type="text"
          placeholder="Серійний номер"
          class="w-full px-3 py-2 bg-gray-100 rounded-md border border-gray-300 focus:outline-none focus:ring"
      />

      <p v-if="error" class="text-red-500 text-sm mt-2">{{ error }}</p>

      <div class="flex justify-end mt-6 gap-2">
        <button @click="close" class="px-4 py-2 bg-gray-300 rounded hover:bg-gray-400">Скасувати</button>
        <button @click="submit" class="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700">Додати</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, defineProps, defineEmits } from "vue";
import api from "@/api";

const props = defineProps<{ roomId: number; isOpen: boolean }>();
const emit = defineEmits(["close", "added"]);

const serial = ref("");
const error = ref("");

const close = () => {
  serial.value = "";
  error.value = "";
  emit("close");
};

const submit = async () => {
  if (!serial.value.trim()) {
    error.value = "Введіть серійний номер";
    return;
  }

  try {
    await api.post(`/room/${props.roomId}/sensor`, {
      serial_number: serial.value.trim(),
    });
    emit("added");
    close();
  } catch (err) {
    error.value = "Помилка додавання сенсора";
  }
};
</script>
