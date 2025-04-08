<template>
  <div class="fixed inset-0 flex items-center justify-center bg-black bg-opacity-50 z-50" @click.self="close">
    <div class="bg-white p-6 rounded-lg shadow-lg max-w-sm text-center">
      <h3 class="text-lg font-semibold text-gray-800">{{ title }}</h3>
      <p v-if="message" class="text-gray-600 text-sm mt-2">
        {{ message }}
      </p>
      <div class="mt-4 flex gap-4 justify-center">
        <button @click="confirm" class="bg-red-600 text-white px-4 py-2 rounded-md hover:bg-red-700">
          {{ confirmText }}
        </button>
        <button @click="close" class="bg-gray-300 px-4 py-2 rounded-md hover:bg-gray-400">
          {{ cancelText }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { defineProps, defineEmits } from "vue";

const props = defineProps({
  title: { type: String, required: true },
  message: { type: String, default: "" },
  confirmText: { type: String, default: "Підтвердити" },
  cancelText: { type: String, default: "Скасувати" },
  onConfirm: { type: Function, required: true },
});

const emit = defineEmits(["close", "confirmed"]);

const close = () => {
  emit("close");
};

const confirm = async () => {
  try {
    await props.onConfirm();
    emit("confirmed");
  } catch (error) {
    console.error("Помилка виконання дії:", (error as Error).message);
  } finally {
    close();
  }
};
</script>
