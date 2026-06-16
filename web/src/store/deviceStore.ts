import { defineStore } from "pinia";
import { reactive, computed } from "vue";
import { Device, DevicesResponse } from "@/services/apiService";
import api from "@/api";

export const useDeviceStore = defineStore("deviceStore", () => {
    const state = reactive<{
        devices: Map<number, Device>;
        isLoading: Map<number, boolean>;
    }>({
        devices: new Map(),
        isLoading: new Map(),
    });

    const fetchDevice = async (roomId: number, deviceId: number) => {
        if (state.devices.has(deviceId)) {
            return state.devices.get(deviceId)!;
        }

        try {
            state.isLoading.set(deviceId, true);

            let response: DevicesResponse;
            let skip: number = 0;
            do {
                response = (await api.get(`/room/${roomId}/device?skip=${skip}`)).data;
                for (const device of response.data)
                    state.devices.set(device.id, device);

                for (const device of response.data) {
                    if (device.id === deviceId) {
                        state.isLoading.set(deviceId, false);
                        return device;
                    }
                }

                skip += 5;
            } while (skip < response.pagination.total);

            state.isLoading.set(deviceId, false);

            return null;
        } catch (error) {
            console.error(`Ошибка загрузки пристрою ${deviceId}:`, error);
            throw error;
        } finally {
            state.isLoading.set(deviceId, false);
        }
    };

    return {
        fetchDevice,
        isLoading: computed(() => state.isLoading),
    };
});
