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
const authReadyTimeoutMs = 1500;

const readBrowserToken = () => {
  if (import.meta.server || typeof window === "undefined") return null;

  try {
    return window.localStorage?.getItem("token") ?? null;
  } catch {
    return null;
  }
};

const clearRefreshTimer = () => {
  if (refreshTimer) {
    clearInterval(refreshTimer);
    refreshTimer = null;
  }
};

const isExpired = (claims: DecodedToken | null) => (
  Boolean(claims?.exp && claims.exp * 1000 <= Date.now())
);

const parseUsableToken = (token: string | null) => {
  if (!token) return { token: null, claims: null };

  const claims = decodeToken(token);
  if (!claims || isExpired(claims)) {
    return { token: null, claims: null };
  }

  return { token, claims };
};

const writeBrowserTokenCookie = (token: string | null) => {
  if (!import.meta.client) return;

  if (token) {
    document.cookie = `${AUTH_TOKEN_COOKIE}=${encodeURIComponent(token)}; Path=/; Max-Age=${60 * 60 * 24 * 7}; SameSite=Lax`;
  } else {
    document.cookie = `${AUTH_TOKEN_COOKIE}=; Path=/; Max-Age=0; SameSite=Lax`;
  }
};

export const useAuthStore = defineStore("auth", {
  state: () => {
    const tokenCookie = useCookie<string | null>(AUTH_TOKEN_COOKIE);
    const auth = parseUsableToken(readBrowserToken() || tokenCookie.value || null);

    return {
      user: null as User | null,
      token: auth.token,
      claims: auth.claims as DecodedToken | null,
    };
  },

  getters: {
    isAuthenticated: (state) => Boolean(state.token && state.claims?.id),
    currentUserEmail: (state) => state.user?.email || state.claims?.email || null,
    currentUserId: (state) => state.claims?.id || null,
  },

  actions: {
    hydrateFromCookie() {
      const tokenCookie = useCookie<string | null>(AUTH_TOKEN_COOKIE);
      const { token } = parseUsableToken(readBrowserToken() || tokenCookie.value || null);
      this.setToken(token);
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
        let initialStateTimer: ReturnType<typeof setTimeout> | undefined;

        const resolveInitialState = (user: User | null) => {
          if (initialStateResolved) return;

          initialStateResolved = true;
          if (initialStateTimer) clearTimeout(initialStateTimer);
          resolve(user);
        };

        initialStateTimer = setTimeout(() => {
          resolveInitialState(auth.currentUser);
        }, authReadyTimeoutMs);

        onAuthStateChanged(auth, async (user) => {
          this.user = user;

          if (user) {
            const token = await getIdToken(user);
            this.setToken(token);

            await this.registerInApi(token);

            if (!refreshTimer) {
              refreshTimer = setInterval(() => this.refreshToken(), 50 * 60 * 1000);
            }
          } else {
            const existingToken = this.token;
            this.setToken(existingToken);
            clearRefreshTimer();

            if (existingToken) {
              await this.registerInApi(existingToken);
            }
          }

          resolveInitialState(user);
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

    setToken(nextToken: string | null) {
      const tokenCookie = useCookie<string | null>(AUTH_TOKEN_COOKIE, {
        path: "/",
        sameSite: "lax",
        maxAge: 60 * 60 * 24 * 7,
      });

      const { token, claims } = parseUsableToken(nextToken);

      this.token = token;
      this.claims = claims;
      tokenCookie.value = token;

      if (import.meta.client) {
        if (token) {
          try {
            window.localStorage?.setItem("token", token);
          } catch {
            // Browser storage can be unavailable in embedded or privacy-restricted contexts.
          }
          api.defaults.headers.Authorization = `Bearer ${token}`;
        } else {
          try {
            window.localStorage?.removeItem("token");
          } catch {
            // Browser storage can be unavailable in embedded or privacy-restricted contexts.
          }
          delete api.defaults.headers.Authorization;
        }

        writeBrowserTokenCookie(token);
      }
    },
  },
});
