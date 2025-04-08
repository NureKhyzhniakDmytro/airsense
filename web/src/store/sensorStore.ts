import { defineStore } from "pinia";
import { reactive, computed } from "vue";
import { Sensor, SensorsResponse } from "@/services/apiService";
import api from "@/api";

export const useSensorStore = defineStore("sensorStore", () => {
    const state = reactive<{
        sensors: Map<number, Sensor>;
        isLoading: Map<number, boolean>;
    }>({
        sensors: new Map(),
        isLoading: new Map(),
    });

    const fetchSensor = async (roomId: number, sensorId: number) => {
        if (state.sensors.has(sensorId)) {
            return state.sensors.get(sensorId)!;
        }

        try {
            state.isLoading.set(sensorId, true);

            let response: SensorsResponse;
            let skip: number = 0;
            do {
                response = (await api.get(`/room/${roomId}/sensor?skip=${skip}`)).data;
                for (const sensor of response.data)
                    state.sensors.set(sensor.id, sensor);

                for (const sensor of response.data) {
                    if (sensor.id === sensorId) {
                        state.isLoading.set(sensorId, false);
                        return sensor;
                    }
                }

                skip += 5;
            } while (skip < response.pagination.total);

            state.isLoading.set(sensorId, false);

            return null;
        } catch (error) {
            console.error(`Ошибка загрузки сенсора ${sensorId}:`, error);
            throw error;
        } finally {
            state.isLoading.set(sensorId, false);
        }
    };

    return {
        fetchSensor,
        isLoading: computed(() => state.isLoading),
    };
});
