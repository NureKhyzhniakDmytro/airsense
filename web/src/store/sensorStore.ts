import { defineStore } from 'pinia';
import { reactive, ref, computed } from 'vue';
import { Sensor, SensorsResponse } from '@/types/sensor';
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
  const pagesData = new Map<number, SensorsResponse>();
  const pagePromises = new Map<number, Promise<SensorsResponse>>();
  const pageSize = ref<number | null>(null);

  // Дедупликация запросов по конкретному sensorId
  const fetchSensorPromises = new Map<number, Promise<Sensor | null>>();

  // Получить и закешировать одну страницу (skip = смещение)
  function fetchPage(roomId: number, skip: number): Promise<SensorsResponse> {
    if (pagesData.has(skip)) {
      // Уже есть в кеше — сразу возвращаем
      return Promise.resolve(pagesData.get(skip)!);
    }
    if (pagePromises.has(skip)) {
      // Уже в процессе загрузки — возвращаем тот же промис
      return pagePromises.get(skip)!;
    }

    const p = api
      .get<SensorsResponse>(`/room/${roomId}/sensor?skip=${skip}`)
      .then(res => {
        const resp = res.data;
        // Сохраняем в кеш страницы
        pagesData.set(skip, resp);
        // Сохраняем все сенсоры этой страницы в общий Map
        resp.data.forEach(sensor => {
          state.sensors.set(sensor.id, sensor);
        });
        // Фиксируем размер страницы
        if (pageSize.value === null) {
          pageSize.value = resp.data.length;
        }
        return resp;
      })
      .finally(() => {
        pagePromises.delete(skip);
      });

    pagePromises.set(skip, p);
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
    if (fetchSensorPromises.has(sensorId)) {
      return fetchSensorPromises.get(sensorId)!;
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
          if (!pageSize.value || resp.data.length === 0) {
            break;
          }
          skip += pageSize.value;
        }
        return null;
      } catch (err) {
        console.error(`Ошибка загрузки сенсора ${sensorId}:`, err);
        return null;
      } finally {
        state.isLoading.set(sensorId, false);
        fetchSensorPromises.delete(sensorId);
      }
    })();

    fetchSensorPromises.set(sensorId, p);
    return p;
  }

  return {
    fetchSensor,
    isLoading: computed(() => state.isLoading),
  };
});
