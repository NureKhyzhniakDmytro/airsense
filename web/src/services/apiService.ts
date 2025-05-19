import api from "@/api";
import type { RegisterPayload, UsersResponse } from '@/types/user';
import type { Environment, EnvironmentsResponse, CreateEnvironmentPayload, UpdateEnvironmentPayload } from '@/types/environment';
import type { Room, RoomsResponse, UpdateRoomPayload } from '@/types/room';
import type { Sensor, Device, HistoryEntry, Parameter, HistoryParams, CurveData } from '@/types/sensor';

export const register = async (payload: RegisterPayload) => {
  const response = await api.post("/auth", payload);
  return response.data;
};

export const addUserToEnv = async (envId: number, userId: number) => {
  const response = await api.post(`/env/${envId}/member/${userId}`);
  return response.data;
};

export const addUserToEnvByEmail = async (envId: number, email: string) => {
  const response = await api.post(`/env/${envId}/member/${email}`);
  return response.data;
};

export const changeUserRole = async (envId: number, userId: number, role: string) => {
  const response = await api.patch(`/env/${envId}/member/${userId}`, { role });
  return response.data;
};

export const removeUser = async (envId: number, userId: number) => {
  const response = await api.delete(`/env/${envId}/member/${userId}`);
  return response.data;
};

export const getRooms = async (envId: number, skip = 0, count = 5) => {
  try {
    const response = await api.get<RoomsResponse>(`/env/${envId}/room`, {
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
    console.error("Failed to fetch rooms:", error);
    return { rooms: [], pagination: { total: 0, skip, count } };
  }
};

export const getRoom = async (envId: number, roomId: number) => {
  const response = await api.get(`/env/${envId}/room/${roomId}`);
  return response.data as Room;
};

export const createRoom = async (envId: number, name: string): Promise<Room> => {
  const response = await api.post(`/env/${envId}/room`, { name });
  return response.data;
};

export const updateRoom = async (envId: number, roomId: number, data: UpdateRoomPayload) => {
  const response = await api.patch(`/env/${envId}/room/${roomId}`, data);
  return response.data;
};

export const removeRoom = async (envId: number, roomId: number) => {
  const response = await api.delete(`/env/${envId}/room/${roomId}`);
  return response.data;
};

export const getSensors = async (roomId: number) => {
  const response = await api.get(`/room/${roomId}/sensor`);
  return response.data as Sensor[];
};

export const addSensor = async (roomId: number, serialNumber: string) => {
  const response = await api.post(`/room/${roomId}/sensor`, { serial_number: serialNumber });
  return response.data;
};

export const removeSensor = async (roomId: number, sensorId: number) => {
  const response = await api.delete(`/room/${roomId}/sensor/${sensorId}`);
  return response.data;
};

export const getDevices = async (roomId: number) => {
  const response = await api.get(`/room/${roomId}/device`);
  return response.data as Device[];
};

export const addDevice = async (roomId: number, serialNumber: string) => {
  const response = await api.post(`/room/${roomId}/device`, { serial_number: serialNumber });
  return response.data;
};

export const removeDevice = async (roomId: number, deviceId: number) => {
  const response = await api.delete(`/room/${roomId}/device/${deviceId}`);
  return response.data;
};

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
    console.error("Failed to fetch environments:", error);
    return { data: [], pagination: { total: 0, skip, count } };
  }
};

export const updateEnvironment = async (envId: number, data: UpdateEnvironmentPayload): Promise<Environment> => {
  const response = await api.patch(`/env/${envId}`, data);
  return response.data;
};

export const deleteEnvironment = async (envId: number) => {
  const response = await api.delete(`/env/${envId}`);
  return response.data;
};

export const getEnvironmentDetails = async (envId: number): Promise<Environment> => {
  const response = await api.get(`/env/${envId}`);
  return response.data;
};

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
    console.error("Failed to fetch members:", error);
    return { members: [], pagination: { total: 0, skip, count } };
  }
};

export const createEnvironment = async (data: CreateEnvironmentPayload): Promise<Environment> => {
  try {
    const response = await api.post<Environment>("/env", data);
    return response.data;
  } catch (error) {
    console.error("Failed to create environment:", error);
    throw new Error("Failed to create environment. Please try again.");
  }
};

export async function getAvailableParameters(roomId: number): Promise<Parameter[]> {
  const res = await api.get(`/room/${roomId}`);
  return res.data;
}

export const getRoomCurve = async (roomId: number, paramName: string): Promise<CurveData> => {
  const response = await api.get(`/room/${roomId}/${paramName}/curve`);
  return response.data;
};

export const updateRoomCurve = async (roomId: number, paramName: string, data: CurveData) => {
  const response = await api.patch(`/room/${roomId}/${paramName}/curve`, data);
  return response.data;
};

export const getRoomHistory = async (roomId: number, params: HistoryParams) => {
  const response = await api.get(`/room/${roomId}/history`, { params });
  return response.data;
};

export const getParameterHistory = async (roomId: number, paramName: string, params: HistoryParams) => {
  const response = await api.get(`/room/${roomId}/${paramName}/history`, { params });
  return response.data;
};

export const getRoomDevices = async (roomId: number, skip = 0, count = 5) => {
  const response = await api.get(`/room/${roomId}/device`, {
    params: { skip, count },
  });
  return response.data;
};

export const getRoomSensors = async (roomId: number, skip = 0, count = 5) => {
  const response = await api.get(`/room/${roomId}/sensor`, {
    params: { skip, count },
  });
  return response.data;
};
