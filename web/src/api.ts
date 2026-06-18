import axios from "axios";
import type { AxiosRequestConfig } from "axios";
import { AUTH_TOKEN_COOKIE } from "@/constants/auth";

const api = axios.create({
  headers: {
    "Content-Type": "application/json",
  },
});

const readCookie = (cookieHeader: string | undefined, name: string) => {
  if (!cookieHeader) return null;

  const cookie = cookieHeader
    .split(";")
    .map((item) => item.trim())
    .find((item) => item.startsWith(`${name}=`));

  if (!cookie) return null;

  const value = cookie.slice(name.length + 1);
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
};

const getRuntimeApiBaseUrl = () => {
  try {
    const runtimeConfig = useRuntimeConfig();
    return import.meta.server
      ? runtimeConfig.apiInternalBaseUrl || runtimeConfig.public.apiBaseUrl
      : runtimeConfig.public.apiBaseUrl;
  } catch {
    if (import.meta.server) {
      return process.env.NUXT_API_INTERNAL_BASE_URL || process.env.NUXT_PUBLIC_API_BASE_URL || process.env.VITE_API_BASE_URL || "";
    }

    const nuxtPayload = globalThis.window?.__NUXT__;
    return nuxtPayload?.config?.public?.apiBaseUrl || "";
  }
};

const readLocalStorageToken = () => {
  if (!import.meta.client || typeof window === "undefined") return null;

  try {
    return window.localStorage?.getItem("token") ?? null;
  } catch {
    return null;
  }
};

const getAuthToken = () => {
  if (import.meta.client) {
    try {
      const tokenCookie = useCookie<string | null>(AUTH_TOKEN_COOKIE);
      return readLocalStorageToken() || tokenCookie.value;
    } catch {
      return readLocalStorageToken();
    }
  }

  try {
    const tokenCookie = useCookie<string | null>(AUTH_TOKEN_COOKIE);
    if (tokenCookie.value) {
      return tokenCookie.value;
    }

    const headers = useRequestHeaders(["cookie"]);
    return readCookie(headers.cookie, AUTH_TOKEN_COOKIE);
  } catch {
    return null;
  }
};

export const createApiRequestConfig = (config: AxiosRequestConfig = {}): AxiosRequestConfig => {
  const token = getAuthToken();

  return {
    ...config,
    baseURL: config.baseURL || getRuntimeApiBaseUrl(),
    headers: {
      ...config.headers,
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  };
};

api.interceptors.request.use((config) => {
  config.baseURL ||= getRuntimeApiBaseUrl();

  const token = getAuthToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  } else {
    delete config.headers.Authorization;
  }

  return config;
});

export const apiGet = <T = any>(url: string, config?: AxiosRequestConfig) =>
  api.get<T>(url, createApiRequestConfig(config));

export const apiPost = <T = any>(url: string, data?: any, config?: AxiosRequestConfig) =>
  api.post<T>(url, data, createApiRequestConfig(config));

export const apiPatch = <T = any>(url: string, data?: any, config?: AxiosRequestConfig) =>
  api.patch<T>(url, data, createApiRequestConfig(config));

export const apiDelete = <T = any>(url: string, config?: AxiosRequestConfig) =>
  api.delete<T>(url, createApiRequestConfig(config));

export default api;
