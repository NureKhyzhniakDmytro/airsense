import { defineStore } from "pinia";
import { reactive, computed } from "vue";
import { EnvironmentsResponse, getEnvironmentDetails, getEnvironments } from "@/services/apiService";
import type { Environment } from "@/services/apiService";

export const useEnvironmentStore = defineStore("environmentStore", () => {
  const state = reactive<{
    environments: Map<number, Environment>;
    pages: Map<number, Environment[]>;
    isLoading: Map<number, boolean>;
    isLoadingList: boolean;
    pendingRequests: Map<number, Promise<Environment> | null>;
    pendingListRequest: Map<number, Promise<EnvironmentsResponse> | null>;
    pagination: { total: number; skip: number; count: number };
  }>({
    environments: new Map(),
    pages: new Map(),
    isLoading: new Map(),
    isLoadingList: false,
    pendingRequests: new Map(),
    pendingListRequest: new Map(),
    pagination: { total: 0, skip: 0, count: 6 },
  });

  const fetchEnvironment = async (envId: number): Promise<Environment> => {
    if (state.environments.has(envId)) {
      return state.environments.get(envId)!;
    }

    if (state.pendingRequests.has(envId)) {
      return state.pendingRequests.get(envId)!;
    }

    try {
      state.isLoading.set(envId, true);
      const request = getEnvironmentDetails(envId);
      state.pendingRequests.set(envId, request);

      const environment = await request;
      state.environments.set(envId, environment);

      return environment;
    } catch (error) {
      console.error(`Ошибка загрузки окружения ${envId}:`, error);
      throw error;
    } finally {
      state.isLoading.set(envId, false);
      state.pendingRequests.delete(envId);
    }
  };

  const fetchEnvironments = async (pageNumber = 0): Promise<Environment[]> => {
    if (state.pages.has(pageNumber)) {
      return state.pages.get(pageNumber)!;
    }

    if (state.pendingListRequest.has(pageNumber)) {
      return state.pendingListRequest.get(pageNumber)!;
    }

    try {
      state.isLoadingList = true;
      const skip = pageNumber * state.pagination.count;
      const request = getEnvironments(skip, state.pagination.count);
      state.pendingListRequest.set(pageNumber, request);

      const { data, pagination } = await request;
      state.pages.set(pageNumber, data);
      state.pagination = pagination;

      data.forEach(env => {
        state.environments.set(env.id, env);
      });

      return data;
    } catch (error) {
      console.error("Ошибка загрузки списка окружений:", error);
      throw error;
    } finally {
      state.isLoadingList = false;
      state.pendingListRequest.delete(pageNumber);
    }
  };

  return {
    fetchEnvironment,
    fetchEnvironments,
    environments: computed(() => Array.from(state.environments, ([id, value]) => value)),
    pagination: computed(() => state.pagination),
    isLoading: computed(() => state.isLoading),
    isLoadingList: computed(() => state.isLoadingList),
  };
});
