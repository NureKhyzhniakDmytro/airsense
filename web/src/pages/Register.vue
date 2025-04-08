<template>
  <div class="flex flex-col items-center justify-center p-6">
    <div class="w-full max-w-md bg-white p-8 rounded-lg shadow-md border border-gray-200">
      <h2 class="text-2xl font-bold text-center text-gray-900 mb-6">Створіть обліковий запис</h2>

      <!-- Поля ввода -->
      <div class="mb-4">
        <label class="block text-gray-700 text-sm mb-1">Ім'я</label>
        <input
            v-model="name"
            type="text"
            placeholder="Введіть ім'я"
            class="w-full px-4 py-2 bg-white border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 placeholder-gray-500"
        />
      </div>

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
            class="w-full px-4 py-2 bg-white border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2"
            :class="passwordTooShort ? 'border-red-500 focus:ring-red-500' : 'focus:ring-blue-500'"
        />
        <p v-if="passwordTooShort" class="text-red-500 text-sm mt-1">Пароль має бути не менше 6 символів</p>
      </div>

      <div class="mb-4">
        <label class="block text-gray-700 text-sm mb-1">Повторіть пароль</label>
        <input
            v-model="confirmPassword"
            type="password"
            placeholder="Повторіть пароль"
            class="w-full px-4 py-2 bg-white border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2"
            :class="passwordMismatch ? 'border-red-500 focus:ring-red-500' : 'focus:ring-blue-500'"
        />
        <p v-if="passwordMismatch" class="text-red-500 text-sm mt-1">Паролі не співпадають</p>
      </div>

      <!-- Чекбокс согласия -->
      <div class="flex items-center text-gray-600 text-sm mb-6">
        <input
            type="checkbox"
            id="terms"
            class="mr-2 appearance-none h-4 w-4 border border-gray-300 rounded bg-white checked:bg-blue-600 checked:border-transparent focus:ring-2 focus:ring-blue-500"
        />
        <label for="terms">
          Я погоджуюсь з
          <a href="#" class="text-blue-500 hover:underline">умовами використання</a>
        </label>
      </div>

      <!-- Кнопка регистрации -->
      <button
          @click="handleRegister"
          :disabled="passwordMismatch || passwordTooShort || !name || !email || !password || !confirmPassword"
          class="w-full bg-blue-600 hover:bg-blue-700 text-white font-semibold py-2 rounded-md transition focus:ring-2 focus:ring-blue-500 disabled:bg-gray-400 disabled:cursor-not-allowed"
      >
        Зареєструватися
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
        <img src="/google-logo.svg" alt="Google" class="w-5 h-5"> <span>Зареєструватися через Google</span>
      </button>
    </div>

    <p class="text-sm text-center text-gray-600 mt-6">
      Вже маєте обліковий запис?
      <router-link to="/login" class="text-blue-500 hover:underline">Увійдіть</router-link>
    </p>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useAuthStore } from "@/store/authStore";
import { useRouter } from "vue-router";

const authStore = useAuthStore();
const router = useRouter();
const name = ref("");
const email = ref("");
const password = ref("");
const confirmPassword = ref("");

const passwordMismatch = computed(() => password.value !== confirmPassword.value && confirmPassword.value !== "");

const passwordTooShort = computed(() => password.value.length > 0 && password.value.length < 6);

const handleRegister = async () => {
  if (passwordMismatch.value || passwordTooShort.value) return;
  try {
    await authStore.register(email.value, password.value, name.value);
    await router.push("/");
  } catch (error) {
    console.error("Помилка реєстрації:", (error as Error).message);
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
    router.back();
  }
})
</script>
