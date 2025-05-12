import api from "@/api";

interface Pagination {
  total: number;
  skip: number;
  count: number;
}

interface RegisterPayload {
  notification_token: string;
}

export interface User {
  id: number;
  email: string;
  role: string;
  name: string;
}

export interface Parameter {
  name: string;
  value: number | null;
  unit: string;
  min_value: number;
  max_value: number;
  critical_value: number;
}

export interface Room {
  id: number;
  name: string;
  parameters: Parameter[] | null;
  device_speed: number | null;
}

interface RoomsResponse {
  data: Room[];
  pagination: Pagination;
}

export interface Sensor {
  id: number;
  type_name: string;
  serial_number: string;
  types: string[];
  parameters?: Parameter[];
}

export interface Device {
  id: number;
  serial_number: string;
  fan_speed: number;
  active_at: number;
}

export interface Param {
  name: string;
  label: string;
  unit: string;
}

export interface HistoryEntry {
  value: number;
  timestamp: number;
}

export interface DevicesResponse {
  data: Device[];
  pagination: Pagination;
}

export interface Environment {
  id: number;
  name: string;
  role: string;
}

export interface EnvironmentsResponse {
  data: Environment[];
  pagination: Pagination;
}

interface UsersResponse {
  data: User[];
  pagination: Pagination;
}

export const register = async (payload: RegisterPayload) => {
  const response = await api.post("/auth", payload);
  return response.data;
};

// Добавление пользователя по ID
export const addUserToEnv = async (envId: number, userId: number) => {
  const response = await api.post(`/env/${envId}/member/${userId}`);
  return response.data;
};

// Добавление пользователя по email
export const addUserToEnvByEmail = async (envId: number, email: string) => {
  const response = await api.post(`/env/${envId}/member/${email}`);
  return response.data;
};

// Изменение роли пользователя
export const changeUserRole = async (envId: number, userId: number, role: string) => {
  const response = await api.patch(`/env/${envId}/member/${userId}`, { role });
  return response.data;
};

// Удаление пользователя
export const removeUser = async (envId: number, userId: number) => {
  const response = await api.delete(`/env/${envId}/member/${userId}`);
  return response.data;
};

// Получение списка комнат
export const getRooms = async (envId: number, skip = 0, count = 5) => {
  try {
    const response = await api.get<RoomsResponse>(`/env/${envId}/room`,{
      params: { skip, count },
    });

    if (response.status === 204) {
      return { rooms: [], pagination: { total: 0, skip, count } };
    }

    return {
      rooms: response.data.data,
      pagination: response.data.pagination,
    };
  } catch (error) {
    console.error("Помилка отримання кімнат:", error);
    return { rooms: [], pagination: { total: 0, skip, count } };
  }
};

// Получение информации о конкретной комнате
export const getRoom = async (envId: number, roomId: number) => {
  const response = await api.get(`/env/${envId}/room/${roomId}`);
  return response.data as Room;
};

// Создание комнаты
export const createRoom = async (envId: number, name: string): Promise<Room>  =>  {
  const response = await api.post(`/env/${envId}/room`, { name });
  return response.data;
};

// Обновление комнаты
export const updateRoom = async (envId: number, roomId: number, name: string) => {
  const response = await api.patch(`/env/${envId}/room/${roomId}`, { name });
  return response.data;
};

// Удаление комнаты
export const removeRoom = async (envId: number, roomId: number) => {
  const response = await api.delete(`/env/${envId}/room/${roomId}`);
  return response.data;
};

export interface SensorsResponse {
    data: Sensor[];
    pagination: Pagination;
}

// Получение списка датчиков в комнате
export const getSensors = async (roomId: number) => {
  const response = await api.get(`/room/${roomId}/sensor`);
  return response.data as Sensor[];
};

// Добавление датчика
export const addSensor = async (roomId: number, serialNumber: string) => {
  const response = await api.post(`/room/${roomId}/sensor`, { serial_number: serialNumber });
  return response.data;
};

// Удаление датчика
export const removeSensor = async (roomId: number, sensorId: number) => {
  const response = await api.delete(`/room/${roomId}/sensor/${sensorId}`);
  return response.data;
};

// Получение списка устройств в комнате
export const getDevices = async (roomId: number) => {
  const response = await api.get(`/room/${roomId}/device`);
  return response.data as Device[];
};

// Добавление устройства
export const addDevice = async (roomId: number, serialNumber: string) => {
  const response = await api.post(`/room/${roomId}/device`, { serial_number: serialNumber });
  return response.data;
};

// Удаление устройства
export const removeDevice = async (roomId: number, deviceId: number) => {
  const response = await api.delete(`/room/${roomId}/device/${deviceId}`);
  return response.data;
};

// Получение истории всех устройств в комнате
export const getAllDevicesHistory = async (roomId: number, from?: string, to?: string) => {
  const response = await api.get(`/room/${roomId}/history`, {
    params: { interval: "minute", from, to },
  });
  return response.data as HistoryEntry[];
};

export const getDeviceHistory = async (roomId: number, deviceId: number, from?: string, to?: string) => {
  const response = await api.get(`/room/${roomId}/history/${deviceId}`, {
    params: { interval: "minute", from, to },
  });
  return response.data as HistoryEntry[];
};

export const getEnvironments = async (skip = 0, count = 5): Promise<EnvironmentsResponse> => {
  try {
    const response = await api.get<EnvironmentsResponse>(`/env`, {
      params: { skip, count },
    });

    if (response.status === 204) {
      return { data: [], pagination: { total: 0, skip, count } };
    }

    return {
      data: response.data.data,
      pagination: response.data.pagination,
    };
  } catch (error) {
    console.error("Помилка отримання середовищ:", error);
    return { data: [], pagination: { total: 0, skip, count } };
  }
};

export const getEnvironmentDetails = async (envId: number): Promise<Environment> => {
  const response = await api.get(`/env/${envId}`);
  return response.data;
};

// Получение списка участников окружения
export const getMembers = async (envId: number, skip = 0, count = 5) => {
  try {
    const response = await api.get<UsersResponse>(`/env/${envId}/member`, {
      params: { skip, count },
    });

    if (response.status === 204) {
      return { members: [], pagination: { total: 0, skip, count } };
    }

    return {
      members: response.data.data,
      pagination: response.data.pagination,
    };
  } catch (error) {
    console.error("Помилка отримання учасників:", error);
    return { members: [], pagination: { total: 0, skip, count } };
  }
};

export const createEnvironment = async (data: { name: string }): Promise<Environment> => {
  try {
    const response = await api.post<Environment>("/env", data);
    return response.data;
  } catch (error) {
    console.error("Помилка створення середовища:", error);
    throw new Error("Не вдалося створити середовище. Спробуйте ще раз.");
  }
};

export async function getAvailableParameters(roomId: number): Promise<Parameter[]> {
  const res = await api.get(`/room/${roomId}`);
  return res.data;
}
