<template>
  <div v-if="isOpen" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50" @click.self="close">
    <transition name="fade">
      <div class="bg-white rounded-lg shadow-lg w-96 p-6">
        <h2 class="text-xl font-semibold text-gray-900">Створити кімнату</h2>

        <input
            v-model="name"
            type="text"
            placeholder="Назва кімнати"
            class="mt-4 w-full p-2 bg-gray-100 border border-gray-300 rounded-lg
                   focus:ring-2 focus:ring-blue-500 focus:bg-white
                   hover:bg-gray-200 transition"
            @keyup.enter="create"
        />

        <p v-if="errorMessage" class="text-red-500 text-sm mt-2">{{ errorMessage }}</p>

        <div class="mt-6 flex justify-end gap-2">
          <button @click="close" class="px-4 py-2 bg-gray-300 text-gray-700 rounded-lg hover:bg-gray-400 transition">
            Відмінити
          </button>
          <button
              @click="create"
              :disabled="isLoading"
              class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition disabled:opacity-50"
          >
            {{ isLoading ? "Створення..." : "Створити" }}
          </button>
        </div>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { ref, defineProps, defineEmits } from "vue";
import { useRouter } from "vue-router";
import { createRoom } from "@/services/apiService";

const props = defineProps({
  isOpen: { type: Boolean, default: false },
  envId: { type: Number, required: true }
});
const emit = defineEmits(["close", "added"]);
const router = useRouter();
const name = ref("");
const isLoading = ref(false);
const errorMessage = ref("");

const close = () => {
  emit("close");
  name.value = "";
};

const create = async () => {
  if (!name.value.trim()) return;
  isLoading.value = true;
  const newRoom = await createRoom(props.envId, name.value.trim());
  emit("added");
  close();
  await router.push("/env/" + props.envId + "/room/" + newRoom.id);
};
</script>
