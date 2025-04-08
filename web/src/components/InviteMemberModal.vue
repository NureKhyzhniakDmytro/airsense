<template>
  <!-- Фон без анимации -->
  <div v-if="isOpen" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50" @click.self="close">
    <!-- Окно с анимацией появления -->
    <transition name="fade">
      <div class="bg-white rounded-lg shadow-lg w-96 p-6">
        <h2 class="text-xl font-semibold text-gray-900">Запросити учасника</h2>

        <!-- Поле ввода с обновленным фоном -->
        <input
            v-model="email"
            type="email"
            placeholder="Введіть email"
            class="mt-4 w-full p-2 bg-gray-100 border border-gray-300 rounded-lg
                   focus:ring-2 focus:ring-blue-500 focus:bg-white
                   hover:bg-gray-200 transition"
            @keyup.enter="invite"
        />

        <p v-if="errorMessage" class="text-red-500 text-sm mt-2">{{ errorMessage }}</p>

        <div class="mt-6 flex justify-end gap-2">
          <button @click="close" class="px-4 py-2 bg-gray-300 text-gray-700 rounded-lg hover:bg-gray-400 transition">
            Відмінити
          </button>
          <button
              @click="invite"
              :disabled="isLoading"
              class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition disabled:opacity-50"
          >
            {{ isLoading ? "Запрошення..." : "Запросити" }}
          </button>
        </div>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { defineProps, defineEmits, ref, watch } from "vue";
import { addUserToEnvByEmail } from "@/services/apiService";

const props = defineProps({
  isOpen: { type: Boolean, default: false },
  envId: { type: Number, required: true }
});

const emit = defineEmits(["close", "added"]);

const email = ref("");
const isLoading = ref(false);
const errorMessage = ref("");

const close = () => {
  emit("close");
  email.value = "";
  errorMessage.value = "";
};

const invite = async () => {
  if (!email.value.trim()) {
    errorMessage.value = "Email не може бути порожнім.";
    return;
  }

  isLoading.value = true;
  errorMessage.value = "";

  try {
    await addUserToEnvByEmail(props.envId, email.value.trim());
    emit("added");
    close();
  } catch (error) {
    errorMessage.value = "Помилка додавання учасника.";
  } finally {
    isLoading.value = false;
  }
};

watch(() => props.isOpen, (open) => {
  if (open) {
    document.addEventListener("keydown", handleKeydown);
  } else {
    document.removeEventListener("keydown", handleKeydown);
  }
});

const handleKeydown = (event: KeyboardEvent) => {
  if (event.key === "Escape") close();
};
</script>

<style scoped>
.fade-enter-active, .fade-leave-active {
  transition: opacity 0.15s ease-out;
}
.fade-enter-from, .fade-leave-to {
  opacity: 0;
}
</style>
