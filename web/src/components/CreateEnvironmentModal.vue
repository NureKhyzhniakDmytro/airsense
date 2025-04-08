<template>
  <div
      v-if="isOpen"
      class="fixed inset-0 flex items-center justify-center bg-black bg-opacity-50 z-50"
      @click.self="close"
  >
    <transition name="modal">
      <div class="modal-content">
        <h2 class="text-xl font-semibold text-gray-900">Створити середовище</h2>

        <input
            v-model="name"
            type="text"
            placeholder="Назва середовища"
            class="mt-4 w-full p-2 bg-gray-100 border border-gray-300 rounded-lg
                   focus:ring-2 focus:ring-blue-500 focus:bg-white
                   hover:bg-gray-200 transition"
            @keyup.enter="create"
        />

        <p v-if="errorMessage" class="text-red-500 text-sm mt-2">{{ errorMessage }}</p>

        <div class="mt-6 flex justify-end gap-2">
          <button @click="close" class="modal-btn cancel">Відмінити</button>
          <button @click="create" :disabled="isLoading" class="modal-btn confirm">
            {{ isLoading ? "Створення..." : "Створити" }}
          </button>
        </div>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { ref, defineProps, defineEmits, watch, onUnmounted } from "vue";
import { useRouter } from "vue-router";
import { createEnvironment } from "@/services/apiService";

const props = defineProps<{ isOpen: boolean }>();
const emit = defineEmits(["close", "created"]);
const router = useRouter();

const name = ref("");
const isLoading = ref(false);
const errorMessage = ref("");

const close = () => {
  emit("close");
  name.value = "";
  errorMessage.value = "";
};

const create = async () => {
  if (!name.value.trim()) {
    errorMessage.value = "Назва середовища не може бути порожньою.";
    return;
  }

  isLoading.value = true;
  errorMessage.value = "";

  try {
    const newEnv = await createEnvironment({ name: name.value.trim() });
    emit("created", newEnv);
    close();
    await router.push("/env/" + newEnv.id);
  } catch (error) {
    errorMessage.value = "Помилка створення середовища.";
  } finally {
    isLoading.value = false;
  }
};

const handleKeydown = (event: KeyboardEvent) => {
  if (event.key === "Escape") close();
};

watch(() => props.isOpen, (open) => {
  if (open) document.addEventListener("keydown", handleKeydown);
  else document.removeEventListener("keydown", handleKeydown);
});

onUnmounted(() => document.removeEventListener("keydown", handleKeydown));
</script>

<style scoped>
/* Анимация появления только для модального окна */
.modal-enter-active,
.modal-leave-active {
  transition: opacity 0.3s ease-out, transform 0.3s ease-out;
}

.modal-enter-from,
.modal-leave-to {
  opacity: 0;
  transform: scale(0.9);
}

/* Основной стиль модального окна */
.modal-content {
  @apply bg-white rounded-lg shadow-lg w-96 p-6 transform transition-all;
}

/* Кнопки */
.modal-btn {
  @apply px-4 py-2 rounded-lg transition;
}

.modal-btn.cancel {
  @apply bg-gray-300 text-gray-700 hover:bg-gray-400;
}

.modal-btn.confirm {
  @apply bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50;
}
</style>
