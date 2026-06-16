<template>
  <div class="h-screen flex flex-col overflow-hidden bg-surface-100 text-color items-center">
    <div class="flex-grow flex flex-col items-center place-content-center w-full max-w-md">
      <div class="w-full bg-white p-8 rounded-lg shadow-md border border-gray-200">
      <h2 class="text-2xl font-bold text-center text-gray-900 mb-6">Login to your account</h2>

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
            class="w-full px-4 py-2 bg-white border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 placeholder-gray-500"
        />
      </div>

      <div class="flex justify-between items-center mb-6">
        <label class="flex items-center text-gray-600 text-sm">
          <input
              type="checkbox"
              class="mr-2 appearance-none h-4 w-4 border border-gray-300 rounded bg-white checked:bg-blue-600 checked:border-transparent focus:ring-2 focus:ring-blue-500"
          />
          Remember me
        </label>
        <a href="#" class="text-blue-500 text-sm hover:underline">Forgot password?</a>
      </div>


      <button @click="handleSubmit"
              class="w-full bg-blue-600 hover:bg-blue-700 text-white font-semibold py-2 rounded-md transition focus:ring-2 focus:ring-blue-500">
          Login
      </button>

      <div class="flex items-center my-6">
        <div class="flex-grow border-t border-gray-300"></div>
        <span class="px-2 text-gray-500 text-sm">or</span>
        <div class="flex-grow border-t border-gray-300"></div>
      </div>

      <button @click="handleGoogleLogin"
              class="w-full flex items-center justify-center gap-2 border border-gray-300 bg-white rounded-md py-2 text-gray-700 hover:bg-gray-100 transition focus:ring-2 focus:ring-blue-500">
        <img src="/google-logo.svg" alt="Google" class="w-5 h-5"> <span>Login with Google</span>
      </button>
    </div>

    <p class="text-sm text-center text-gray-600 mt-6">
      Don't have an account?
      <router-link to="/register" class="text-blue-500 hover:underline">Register</router-link>
    </p>
    </div>
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
    console.error("Error logging in:", (error as Error).message);
  }
};

const handleGoogleLogin = async () => {
  try {
    await authStore.loginWithGoogle();
    await router.push("/");
  } catch (error) {
    console.error("Error logging in with Google:", error);
  }
};

onMounted(async () => {
  if (authStore.user) {
    await router.push('/');
  }
})
</script>
