<template>
  <div class="flex flex-col items-center justify-center p-5">
    <div class="w-full max-w-md bg-white p-8 rounded-lg shadow-md border border-gray-200">
      <h2 class="text-2xl font-bold text-center text-gray-900 mb-6">Увійдіть у свій обліковий запис</h2>

      <!-- Поля ввода -->
      <div class="mb-4">
        <label class="block text-gray-700 text-sm mb-1">Електронна пошта</label>
        <input
            v-model="email"
            type="email"
            placeholder="Введіть email"
            class="w-full px-4 py-2 bg-white border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 placeholder-gray-500"
        />
      </div>

      <div class="mb-4">
        <label class="block text-gray-700 text-sm mb-1">Пароль</label>
        <input
            v-model="password"
            type="password"
            placeholder="Введіть пароль"
            class="w-full px-4 py-2 bg-white border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 placeholder-gray-500"
        />
      </div>

      <!-- Запомнить меня + забыли пароль -->
      <div class="flex justify-between items-center mb-6">
        <label class="flex items-center text-gray-600 text-sm">
          <input
              type="checkbox"
              class="mr-2 appearance-none h-4 w-4 border border-gray-300 rounded bg-white checked:bg-blue-600 checked:border-transparent focus:ring-2 focus:ring-blue-500"
          />
          Запам'ятати мене
        </label>
        <a href="#" class="text-blue-500 text-sm hover:underline">Забули пароль?</a>
      </div>


      <!-- Кнопка входа -->
      <button @click="handleSubmit"
              class="w-full bg-blue-600 hover:bg-blue-700 text-white font-semibold py-2 rounded-md transition focus:ring-2 focus:ring-blue-500">
        Увійти
      </button>

      <!-- Разделитель -->
      <div class="flex items-center my-6">
        <div class="flex-grow border-t border-gray-300"></div>
        <span class="px-2 text-gray-500 text-sm">або</span>
        <div class="flex-grow border-t border-gray-300"></div>
      </div>

      <!-- Вход через Google -->
      <button @click="handleGoogleLogin"
              class="w-full flex items-center justify-center gap-2 border border-gray-300 bg-white rounded-md py-2 text-gray-700 hover:bg-gray-100 transition focus:ring-2 focus:ring-blue-500">
        <img src="/google-logo.svg" alt="Google" class="w-5 h-5"> <span>Увійти через Google</span>
      </button>
    </div>

    <p class="text-sm text-center text-gray-600 mt-6">
      Ще не маєте облікового запису?
      <router-link to="/register" class="text-blue-500 hover:underline">Зареєструйтесь</router-link>
    </p>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useAuthStore } from "@/store/authStore";
import { useRouter } from "vue-router";

const authStore = useAuthStore();
const router = useRouter();
const email = ref("");
const password = ref("");

const handleSubmit = async () => {
  try {
    await authStore.login(email.value, password.value);
    await router.push("/");
  } catch (error) {
    console.error("Помилка входу:", (error as Error).message);
  }
};

const handleGoogleLogin = async () => {
  try {
    await authStore.loginWithGoogle();
    await router.push("/");
  } catch (error) {
    console.error("Помилка входу через Google:", error);
  }
};

onMounted(async () => {
  if (authStore.user) {
    await router.push('/');
  }
})
</script>
