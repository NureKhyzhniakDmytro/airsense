import { defineStore } from "pinia";
import { logout as firebaseLogout, signInWithGoogle, getFirebaseAuth, isFirebaseConfigured } from "@/firebase";
import {
  createUserWithEmailAndPassword,
  signInWithEmailAndPassword,
  updateProfile,
  getIdToken,
  onAuthStateChanged,
} from "firebase/auth";
import type { User } from "firebase/auth";
import { register } from "@/services/apiService";
import { decodeToken } from "@/utils/jwt";
import type { DecodedToken } from "@/utils/jwt";
import { AUTH_TOKEN_COOKIE } from "@/constants/auth";
import api from "@/api";

let authReadyPromise: Promise<User | null> | null = null;
let refreshTimer: ReturnType<typeof setInterval> | null = null;

const readBrowserToken = () => {
  if (import.meta.server) return null;
  return localStorage.getItem("token");
};

const clearRefreshTimer = () => {
  if (refreshTimer) {
    clearInterval(refreshTimer);
    refreshTimer = null;
  }
};

export const useAuthStore = defineStore("auth", {
  state: () => {
    const tokenCookie = useCookie<string | null>(AUTH_TOKEN_COOKIE);
    const token = readBrowserToken() || tokenCookie.value || null;

    return {
      user: null as User | null,
      token,
      claims: token ? decodeToken(token) : null as DecodedToken | null,
    };
  },

  getters: {
    isAuthenticated: (state) => Boolean(state.token),
    currentUserEmail: (state) => state.user?.email || state.claims?.email || null,
    currentUserId: (state) => state.claims?.id || null,
  },

  actions: {
    hydrateFromCookie() {
      const tokenCookie = useCookie<string | null>(AUTH_TOKEN_COOKIE);
      const token = readBrowserToken() || tokenCookie.value || null;
      if (token !== this.token) {
        this.setToken(token);
      } else {
        this.claims = token ? decodeToken(token) : null;
      }
    },

    async startAuthListener() {
      if (import.meta.server) {
        this.hydrateFromCookie();
        return this.user;
      }

      if (authReadyPromise) {
        return authReadyPromise;
      }

      if (!isFirebaseConfigured()) {
        this.hydrateFromCookie();
        authReadyPromise = Promise.resolve(null);
        return authReadyPromise;
      }

      authReadyPromise = new Promise<User | null>((resolve) => {
        const auth = getFirebaseAuth();
        let initialStateResolved = false;

        onAuthStateChanged(auth, async (user) => {
          this.user = user;

          if (user) {
            const token = await getIdToken(user);
            this.setToken(token);

            if (!refreshTimer) {
              refreshTimer = setInterval(() => this.refreshToken(), 50 * 60 * 1000);
            }
          } else if (!this.token) {
            this.setToken(null);
            clearRefreshTimer();
          }

          if (!initialStateResolved) {
            initialStateResolved = true;
            resolve(user);
          }
        });
      });

      return authReadyPromise;
    },

    async register(email: string, password: string, name: string): Promise<void> {
      const auth = getFirebaseAuth();
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
      const auth = getFirebaseAuth();
      const userCredential = await signInWithEmailAndPassword(auth, email, password);
      this.user = userCredential.user;

      const token = await getIdToken(userCredential.user);
      this.setToken(token);

      const decoded = decodeToken(token);
      if (!decoded?.id) {
        await this.registerInApi(token);
      }
    },

    async loginWithGoogle() {
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
    },

    async registerInApi(token: string) {
      await register({ notification_token: token });

      if (this.user) {
        const newToken = await getIdToken(this.user, true);
        this.setToken(newToken);
      }
    },

    async refreshToken() {
      if (!this.user) return;
      try {
        const newToken = await getIdToken(this.user, true);
        this.setToken(newToken);
      } catch (error) {
        console.error("Token refresh error:", error);
        await this.logout();
      }
    },

    async logout() {
      await firebaseLogout();
      this.user = null;
      this.setToken(null);
      clearRefreshTimer();
    },

    setToken(token: string | null) {
      const tokenCookie = useCookie<string | null>(AUTH_TOKEN_COOKIE, {
        path: "/",
        sameSite: "lax",
        maxAge: 60 * 60 * 24 * 7,
      });

      this.token = token;
      this.claims = token ? decodeToken(token) : null;
      tokenCookie.value = token;

      if (import.meta.client) {
        if (token) {
          localStorage.setItem("token", token);
          api.defaults.headers.Authorization = `Bearer ${token}`;
        } else {
          localStorage.removeItem("token");
          delete api.defaults.headers.Authorization;
        }
      }
    },
  },
});
