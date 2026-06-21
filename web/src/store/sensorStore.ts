import { defineStore } from 'pinia';
import { reactive, computed } from 'vue';
import type { Sensor, SensorsResponse } from '@/types/sensor';
import api from '@/api';

export const useSensorStore = defineStore('sensorStore', () => {
  const state = reactive<{
    sensors: Map<number, Sensor>;
    isLoading: Map<number, boolean>;
  }>({
    sensors: new Map(),
    isLoading: new Map(),
  });

  // Кешируем результаты постраничных запросов
  const pagesData = new Map<string, SensorsResponse>();
  const pagePromises = new Map<string, Promise<SensorsResponse>>();
  const pageSizeByRoom = new Map<number, number>();

  // Дедупликация запросов по конкретному sensorId
  const fetchSensorPromises = new Map<string, Promise<Sensor | null>>();

  function pageCacheKey(roomId: number, skip: number) {
    return `${roomId}:${skip}`;
  }

  function sensorCacheKey(roomId: number, sensorId: number) {
    return `${roomId}:${sensorId}`;
  }

  // Получить и закешировать одну страницу (skip = смещение)
  function fetchPage(roomId: number, skip: number): Promise<SensorsResponse> {
    const key = pageCacheKey(roomId, skip);

    if (pagesData.has(key)) {
      // Уже есть в кеше — сразу возвращаем
      return Promise.resolve(pagesData.get(key)!);
    }
    if (pagePromises.has(key)) {
      // Уже в процессе загрузки — возвращаем тот же промис
      return pagePromises.get(key)!;
    }

    const p = api
      .get<SensorsResponse>(`/room/${roomId}/sensor?skip=${skip}`)
      .then(res => {
        const resp = res.data;
        // Сохраняем в кеш страницы
        pagesData.set(key, resp);
        // Сохраняем все сенсоры этой страницы в общий Map
        resp.data.forEach(sensor => {
          state.sensors.set(sensor.id, sensor);
        });
        // Фиксируем размер страницы
        if (!pageSizeByRoom.has(roomId)) {
          pageSizeByRoom.set(roomId, resp.data.length);
        }
        return resp;
      })
      .finally(() => {
        pagePromises.delete(key);
      });

    pagePromises.set(key, p);
    return p;
  }

  // Основная функция: ищем сенсор по id, подгружая по страницам
  function fetchSensor(
    roomId: number,
    sensorId: number
  ): Promise<Sensor | null> {
    // Если уже есть в кеше — сразу возвращаем
    if (state.sensors.has(sensorId)) {
      return Promise.resolve(state.sensors.get(sensorId)!);
    }
    // Если уже запрошен — возвращаем существующий промис
    const key = sensorCacheKey(roomId, sensorId);
    if (fetchSensorPromises.has(key)) {
      return fetchSensorPromises.get(key)!;
    }

    state.isLoading.set(sensorId, true);

    const p = (async (): Promise<Sensor | null> => {
      try {
        let skip = 0;
        let total = Infinity;
        // Подгружаем страницу за страницей
        while (skip < total) {
          const resp = await fetchPage(roomId, skip);
          total = resp.pagination.total;
          // Ищем нужный сенсор в этой странице
          const found = resp.data.find(s => s.id === sensorId);
          if (found) {
            return found;
          }
          // Если не нашли и страница гарантированно пуста или размер не определён — выходим
          const pageSize = pageSizeByRoom.get(roomId);
          if (!pageSize || resp.data.length === 0) {
            break;
          }
          skip += pageSize;
        }
        return null;
      } catch (err) {
        console.error(`Ошибка загрузки сенсора ${sensorId}:`, err);
        return null;
      } finally {
        state.isLoading.set(sensorId, false);
        fetchSensorPromises.delete(key);
      }
    })();

    fetchSensorPromises.set(key, p);
    return p;
  }

  return {
    fetchSensor,
    isLoading: computed(() => state.isLoading),
  };
});
