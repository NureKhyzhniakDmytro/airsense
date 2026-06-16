<template>
  <main class="auth-page">
    <section class="auth-shell">
      <div class="auth-brand">
        <img src="/logo.svg" alt="" />
        <span>AirSense</span>
      </div>

      <Card class="auth-card">
        <template #title>
          <div class="auth-title">Sign in</div>
        </template>

        <template #content>
          <form class="auth-form" @submit.prevent="handleSubmit">
            <Message v-if="errorMessage" severity="error" size="small">
              {{ errorMessage }}
            </Message>

            <div class="auth-field">
              <label for="email">Email</label>
              <InputText
                id="email"
                v-model="email"
                type="email"
                autocomplete="email"
                placeholder="name@example.com"
                fluid
              />
            </div>

            <div class="auth-field">
              <label for="password">Password</label>
              <Password
                id="password"
                v-model="password"
                autocomplete="current-password"
                placeholder="Enter password"
                :feedback="false"
                toggle-mask
                fluid
              />
            </div>

            <div class="auth-row">
              <label class="auth-check" for="remember">
                <Checkbox v-model="rememberMe" input-id="remember" binary />
                <span>Remember me</span>
              </label>
              <a href="#" aria-disabled="true">Forgot password?</a>
            </div>

            <Button
              type="submit"
              label="Login"
              icon="pi pi-arrow-right"
              icon-pos="right"
              :loading="isLoading"
              :disabled="!canSubmit"
              fluid
            />
          </form>

          <Divider align="center" class="auth-divider">
            <span>or</span>
          </Divider>

          <Button
            type="button"
            severity="secondary"
            outlined
            :loading="isGoogleLoading"
            fluid
            @click="handleGoogleLogin"
          >
            <img src="/google-logo.svg" alt="" class="auth-google-icon">
            <span>Login with Google</span>
          </Button>
        </template>
      </Card>

      <p class="auth-footer">
        Don't have an account?
        <NuxtLink to="/register">Register</NuxtLink>
      </p>
    </section>
  </main>
</template>

<script setup lang="ts">
definePageMeta({ name: 'login', guestOnly: true })

import { computed, ref } from "vue";
import { useAuthStore } from "@/store/authStore";
import { useRouter } from "vue-router";
import Button from 'primevue/button';
import Card from 'primevue/card';
import Checkbox from 'primevue/checkbox';
import Divider from 'primevue/divider';
import InputText from 'primevue/inputtext';
import Message from 'primevue/message';
import Password from 'primevue/password';

const authStore = useAuthStore();
const router = useRouter();
const email = ref("");
const password = ref("");
const rememberMe = ref(true);
const isLoading = ref(false);
const isGoogleLoading = ref(false);
const errorMessage = ref("");

const canSubmit = computed(() => Boolean(email.value && password.value) && !isLoading.value);

const handleSubmit = async () => {
  if (!canSubmit.value) return;

  errorMessage.value = "";
  isLoading.value = true;
  try {
    await authStore.login(email.value.trim(), password.value);
    await router.push("/");
  } catch (error) {
    errorMessage.value = "Unable to sign in with these credentials.";
    console.error("Error logging in:", (error as Error).message);
  } finally {
    isLoading.value = false;
  }
};

const handleGoogleLogin = async () => {
  errorMessage.value = "";
  isGoogleLoading.value = true;
  try {
    await authStore.loginWithGoogle();
    await router.push("/");
  } catch (error) {
    errorMessage.value = "Google sign-in failed. Please try again.";
    console.error("Error logging in with Google:", error);
  } finally {
    isGoogleLoading.value = false;
  }
};

</script>

<style scoped>
.auth-page {
  align-items: center;
  background:
    linear-gradient(90deg, rgb(27 34 41 / 0.04) 1px, transparent 1px),
    linear-gradient(180deg, rgb(27 34 41 / 0.035) 1px, transparent 1px),
    var(--app-bg);
  background-size: 24px 24px;
  box-sizing: border-box;
  color: var(--app-text-strong);
  display: flex;
  justify-content: center;
  min-height: 100vh;
  padding: 28px 16px;
}

.auth-shell {
  max-width: 420px;
  width: 100%;
}

.auth-brand {
  align-items: center;
  display: flex;
  gap: 10px;
  justify-content: flex-start;
  margin-bottom: 18px;
  font-size: 1.05rem;
  font-weight: 800;
}

.auth-brand img {
  height: 38px;
  border-radius: 5px;
  width: 38px;
}

.auth-card {
  border: 1px solid var(--app-border);
  box-shadow: none;
  width: 100%;
}

.auth-title {
  color: var(--app-text-strong);
  font-size: 1.25rem;
  font-weight: 780;
  line-height: 1.7rem;
  text-align: left;
}

.auth-form,
.auth-field {
  display: flex;
  flex-direction: column;
}

.auth-form {
  gap: 16px;
}

.auth-field {
  gap: 6px;
}

.auth-field label,
.auth-check {
  color: var(--app-muted);
  font-size: 0.8125rem;
  font-weight: 650;
}

.auth-row {
  align-items: center;
  display: flex;
  justify-content: space-between;
  gap: 12px;
}

.auth-check {
  align-items: center;
  display: inline-flex;
  gap: 8px;
}

.auth-row a,
.auth-footer a {
  color: var(--app-primary);
  font-size: 0.875rem;
  font-weight: 650;
  text-decoration: none;
}

.auth-row a:hover,
.auth-footer a:hover {
  color: var(--app-primary-strong);
  text-decoration: underline;
}

.auth-divider {
  margin-block: 20px;
}

.auth-divider span {
  color: var(--app-muted);
  font-size: 0.8125rem;
}

.auth-google-icon {
  height: 18px;
  width: 18px;
}

.auth-footer {
  color: var(--app-muted);
  font-size: 0.875rem;
  margin: 18px 0 0;
  text-align: left;
}
</style>
