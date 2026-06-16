import { defineStore } from "pinia";
import { auth, logout, signInWithGoogle } from "@/firebase";
import {
  createUserWithEmailAndPassword,
  signInWithEmailAndPassword,
  updateProfile,
  getIdToken,
  User,
  onAuthStateChanged,
} from "firebase/auth";
import { register } from "@/services/apiService";
import { decodeToken } from "@/utils/jwt";
import api from "@/api";

export const useAuthStore = defineStore("auth", {
  state: () => ({
    user: null as User | null,
    token: localStorage.getItem("token") || null as string | null,
  }),

  actions: {
    async register(email: string, password: string, name: string): Promise<void> {
      const userCredential = await createUserWithEmailAndPassword(auth, email, password);
      if (userCredential.user) {
        await updateProfile(userCredential.user, { displayName: name });
        this.user = userCredential.user;

        const token = await getIdToken(userCredential.user, true);
        this.setToken(token);
        await this.registerInApi(token);
      }
    },

    async login(email: string, password: string): Promise<void> {
      try {
        const userCredential = await signInWithEmailAndPassword(auth, email, password);
        this.user = userCredential.user;

        const token = await getIdToken(userCredential.user);
        this.setToken(token);

        const decoded = decodeToken(token);
        if (!decoded?.id) {
          console.log("Користувач не зареєстрований в API, реєструємо...");
          await this.registerInApi(token);
        }
      } catch (error) {
        console.error("Помилка входу:", error);
      }
    },

    async loginWithGoogle() {
      try {
        const user = await signInWithGoogle();
        if (user !== null) {
          this.user = user;

          const token = await getIdToken(user);
          this.setToken(token);

          const decoded = decodeToken(token);
          if (!decoded?.id) {
            await this.registerInApi(token);
          }
        }
      } catch (error) {
        console.error("Помилка входу через Google:", error);
      }
    },

    async registerInApi(token: string) {
      try {
        await register({ notification_token: token });

        const newToken = await getIdToken(this.user as User, true);
        this.setToken(newToken);
      } catch (error) {
        console.error("Помилка реєстрації в API:", error);
      }
    },

    async refreshToken() {
      if (!this.user) return;
      try {
        const newToken = await getIdToken(this.user, true);
        this.setToken(newToken);
        console.log("🔄 Токен успешно обновлен!");
      } catch (error) {
        console.error("⚠ Ошибка обновления токена:", error);
        await this.logout();
      }
    },

    async logout() {
      await logout();
      this.user = null;
      this.setToken(null);
    },

    setToken(token: string | null) {
      this.token = token;
      if (token) {
        localStorage.setItem("token", token);
        api.defaults.headers.Authorization = `Bearer ${token}`;
      } else {
        localStorage.removeItem("token");
        delete api.defaults.headers.Authorization;
      }
    },
  },
});

onAuthStateChanged(auth, async (user) => {
  const authStore = useAuthStore();
  authStore.user = user;
  if (user) {
    const token = await getIdToken(user);
    authStore.setToken(token);
    setInterval(() => authStore.refreshToken(), 50 * 60 * 1000);
  } else {
    authStore.setToken(null);
  }
});
