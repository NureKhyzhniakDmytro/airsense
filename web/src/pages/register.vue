<template>
  <div class="auth-page h-screen flex flex-col overflow-hidden bg-surface-100 text-color items-center">
    <div class="auth-shell flex-grow flex flex-col items-center place-content-center w-full max-w-md">
      <div class="auth-card w-full max-w-md bg-white p-8 rounded-lg shadow-md border border-gray-200">
        <h2 class="text-2xl font-bold text-center text-gray-900 mb-6">Create an account</h2>

        <div class="mb-4">
          <label class="block text-gray-700 text-sm mb-1">Name</label>
          <input
              v-model="name"
              type="text"
              placeholder="Enter name"
              class="w-full px-4 py-2 bg-white border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 placeholder-gray-500"
          />
        </div>

        <div class="mb-4">
          <label class="block text-gray-700 text-sm mb-1">Email</label>
          <input
              v-model="email"
              type="email"
              placeholder="Enter email"
              class="w-full px-4 py-2 bg-white border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 placeholder-gray-500"
          />
        </div>

        <div class="mb-4">
          <label class="block text-gray-700 text-sm mb-1">Password</label>
          <input
              v-model="password"
              type="password"
              placeholder="Enter password"
              class="w-full px-4 py-2 bg-white border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2"
              :class="passwordTooShort ? 'border-red-500 focus:ring-red-500' : 'focus:ring-blue-500'"
          />
          <p v-if="passwordTooShort" class="text-red-500 text-sm mt-1">Password must be at least 6 characters</p>
        </div>

        <div class="mb-4">
          <label class="block text-gray-700 text-sm mb-1">Repeat password</label>
          <input
              v-model="confirmPassword"
              type="password"
              placeholder="Repeat password"
              class="w-full px-4 py-2 bg-white border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2"
              :class="passwordMismatch ? 'border-red-500 focus:ring-red-500' : 'focus:ring-blue-500'"
          />
          <p v-if="passwordMismatch" class="text-red-500 text-sm mt-1">Passwords do not match</p>
        </div>

        <div class="flex items-center text-gray-600 text-sm mb-6">
          <input
              type="checkbox"
              id="terms"
              class="mr-2 appearance-none h-4 w-4 border border-gray-300 rounded bg-white checked:bg-blue-600 checked:border-transparent focus:ring-2 focus:ring-blue-500"
          />
          <label for="terms">
            I agree to the
            <a href="#" class="text-blue-500 hover:underline">terms of use</a>
          </label>
        </div>

        <button
            @click="handleRegister"
            :disabled="passwordMismatch || passwordTooShort || !name || !email || !password || !confirmPassword"
            class="auth-primary-button w-full bg-blue-600 hover:bg-blue-700 text-white font-semibold py-2 rounded-md transition focus:ring-2 focus:ring-blue-500 disabled:bg-gray-400 disabled:cursor-not-allowed"
        >
          Register
        </button>

        <div class="flex items-center my-6">
          <div class="flex-grow border-t border-gray-300"></div>
          <span class="px-2 text-gray-500 text-sm">or</span>
          <div class="flex-grow border-t border-gray-300"></div>
        </div>

        <button @click="handleGoogleLogin"
                class="auth-google-button w-full flex items-center justify-center gap-2 border border-gray-300 bg-white rounded-md py-2 text-gray-700 hover:bg-gray-100 transition focus:ring-2 focus:ring-blue-500">
          <img src="/google-logo.svg" alt="Google" class="auth-google-icon"> <span>Register with Google</span>
        </button>
      </div>

      <p class="auth-footer text-sm text-center text-gray-600 mt-6">
        Already have an account?
        <router-link to="/login" class="text-blue-500 hover:underline">Login</router-link>
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ name: 'register', guestOnly: true })

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
    console.error("Registration error:", (error as Error).message);
  }
};

const handleGoogleLogin = async () => {
  try {
    await authStore.loginWithGoogle();
    await router.push("/");
  } catch (error) {
    console.error("Google login error:", error);
  }
};

onMounted(async () => {
  if (authStore.user) {
    router.back();
  }
})
</script>

<style scoped>
.auth-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f3f4f6;
  color: #111827;
  padding: 24px 16px;
  box-sizing: border-box;
}

.auth-shell {
  width: 100%;
  max-width: 448px;
}

.auth-card {
  width: 100%;
  box-sizing: border-box;
  background: #fff;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  box-shadow: 0 10px 25px rgb(15 23 42 / 0.08);
  padding: 32px;
}

.auth-card h2 {
  margin: 0 0 24px;
  color: #111827;
  font-size: 24px;
  line-height: 32px;
  font-weight: 700;
  text-align: center;
}

.auth-card label {
  display: block;
  color: #374151;
  font-size: 14px;
  line-height: 20px;
  margin-bottom: 4px;
}

.auth-card input[type="email"],
.auth-card input[type="password"],
.auth-card input[type="text"] {
  width: 100%;
  box-sizing: border-box;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: #fff;
  color: #111827;
  font-size: 16px;
  line-height: 24px;
  padding: 8px 16px;
  outline: none;
}

.auth-card input:focus {
  border-color: #3b82f6;
  box-shadow: 0 0 0 2px rgb(59 130 246 / 0.22);
}

.auth-card input[type="checkbox"] {
  width: 16px;
  height: 16px;
  margin: 0 8px 0 0;
  flex: 0 0 16px;
}

.auth-card a,
.auth-footer a {
  color: #3b82f6;
  text-decoration: none;
}

.auth-card a:hover,
.auth-footer a:hover {
  text-decoration: underline;
}

.auth-card button {
  cursor: pointer;
  border-radius: 6px;
  font-size: 16px;
  line-height: 24px;
  transition: background-color 120ms ease, border-color 120ms ease;
}

.auth-primary-button,
.auth-google-button {
  width: 100%;
  min-height: 42px;
  box-sizing: border-box;
}

.auth-primary-button {
  border: 1px solid #2563eb;
  background: #2563eb;
  color: #fff;
  font-weight: 600;
}

.auth-primary-button:hover {
  background: #1d4ed8;
}

.auth-primary-button:disabled {
  cursor: not-allowed;
  border-color: #9ca3af;
  background: #9ca3af;
}

.auth-google-button {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border: 1px solid #d1d5db;
  background: #fff;
  color: #374151;
}

.auth-google-button:hover {
  background: #f9fafb;
}

.auth-google-icon {
  width: 20px;
  height: 20px;
  flex: 0 0 20px;
}

.auth-footer {
  margin: 24px 0 0;
  color: #4b5563;
  font-size: 14px;
  line-height: 20px;
  text-align: center;
}

.auth-card :deep(.text-red-500) {
  color: #ef4444;
}

.auth-card :deep(.mb-4) {
  margin-bottom: 16px;
}

.auth-card :deep(.mb-6) {
  margin-bottom: 24px;
}

.auth-card :deep(.my-6) {
  margin-top: 24px;
  margin-bottom: 24px;
}

.auth-card :deep(.flex) {
  display: flex;
}

.auth-card :deep(.items-center) {
  align-items: center;
}

.auth-card :deep(.justify-between) {
  justify-content: space-between;
}

.auth-card :deep(.flex-grow) {
  flex-grow: 1;
}

.auth-card :deep(.border-t) {
  border-top: 1px solid #d1d5db;
}

.auth-card :deep(.px-2) {
  padding-left: 8px;
  padding-right: 8px;
}

@media (max-width: 480px) {
  .auth-page {
    padding: 20px 12px;
  }

  .auth-card {
    padding: 24px 20px;
  }
}
</style>
