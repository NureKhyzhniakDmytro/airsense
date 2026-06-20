import { apiGet, apiPost, apiPatch, apiDelete } from "@/api";
import type { RegisterPayload, UsersResponse } from '@/types/user';
import type { Environment, EnvironmentsResponse, CreateEnvironmentPayload, UpdateEnvironmentPayload } from '@/types/environment';
import type { CreateRoomPayload, Room, RoomLayout, RoomsResponse, UpdateRoomPayload } from '@/types/room';
import type { Sensor, Device, HistoryEntry, Parameter, HistoryParams, CurveData } from '@/types/sensor';
import type { AiRecommendationAudit, RoomAiInsights } from "@/types/ai";
import type { NotificationsResponse, UnreadNotificationsResponse } from "@/types/notification";
import type {
  DemoBackfillPayload,
  DemoBackfillResult,
  DemoBootstrapPayload,
  DemoDataStatus,
  DemoResetResult,
  DemoRoomAssetsPayload,
  DemoRoomCreatePayload,
  DemoRoomProfilePayload,
  DemoRoomReadingsPayload,
  DemoRoomUpdatePayload,
} from "@/types/demoData";

export const register = async (payload: RegisterPayload) => {
  const response = await apiPost("/auth", payload);
  return response.data;
};

export const getNotifications = async (skip = 0, count = 20): Promise<NotificationsResponse> => {
  const response = await apiGet<NotificationsResponse>("/notifications", {
    params: { skip, count },
  });
  return response.data;
};

export const getUnreadNotificationsCount = async (): Promise<UnreadNotificationsResponse> => {
  const response = await apiGet<UnreadNotificationsResponse>("/notifications/unread-count");
  return response.data;
};

export const markNotificationRead = async (notificationId: number): Promise<void> => {
  await apiPatch(`/notifications/${notificationId}/read`, {});
};

export const markAllNotificationsRead = async (): Promise<{ updated: number }> => {
  const response = await apiPatch<{ updated: number }>("/notifications/read-all", {});
  return response.data;
};

export const getDemoDataStatus = async (): Promise<DemoDataStatus> => {
  const response = await apiGet("/demo-data/status");
  return response.data;
};

export const bootstrapDemoData = async (payload: DemoBootstrapPayload): Promise<DemoDataStatus> => {
  const response = await apiPost("/demo-data/bootstrap", payload);
  return response.data;
};

export const backfillDemoData = async (payload: DemoBackfillPayload): Promise<DemoBackfillResult> => {
  const response = await apiPost("/demo-data/backfill", payload);
  return response.data;
};

export const resetDemoDataHistory = async (): Promise<DemoResetResult> => {
  const response = await apiDelete("/demo-data/history");
  return response.data;
};

export const createDemoRoom = async (payload: DemoRoomCreatePayload): Promise<DemoDataStatus> => {
  const response = await apiPost("/demo-data/rooms", payload);
  return response.data;
};

export const updateDemoRoom = async (
  roomId: number,
  payload: DemoRoomUpdatePayload,
): Promise<DemoDataStatus> => {
  const response = await apiPatch(`/demo-data/rooms/${roomId}`, payload);
  return response.data;
};

export const addDemoRoomAssets = async (
  roomId: number,
  payload: DemoRoomAssetsPayload,
): Promise<DemoDataStatus> => {
  const response = await apiPost(`/demo-data/rooms/${roomId}/assets`, payload);
  return response.data;
};

export const applyDemoRoomReadings = async (
  roomId: number,
  payload: DemoRoomReadingsPayload,
): Promise<DemoDataStatus> => {
  const response = await apiPost(`/demo-data/rooms/${roomId}/readings`, payload);
  return response.data;
};

export const updateDemoRoomProfile = async (
  roomId: number,
  payload: DemoRoomProfilePayload,
): Promise<DemoDataStatus> => {
  const response = await apiPatch(`/demo-data/rooms/${roomId}/profile`, payload);
  return response.data;
};

export const clearDemoRoomProfile = async (roomId: number): Promise<DemoDataStatus> => {
  const response = await apiDelete(`/demo-data/rooms/${roomId}/profile`);
  return response.data;
};

export const getRoomAiInsights = async (roomId: number): Promise<RoomAiInsights> => {
  const response = await apiGet<RoomAiInsights>(`/ai/room/${roomId}`);
  return response.data;
};

export const createRoomAiRecommendation = async (roomId: number): Promise<AiRecommendationAudit> => {
  const response = await apiPost<AiRecommendationAudit>(`/ai/room/${roomId}/recommendation`, {});
  return response.data;
};

export const acceptRoomAiRecommendation = async (
  roomId: number,
  recommendationId: number,
): Promise<AiRecommendationAudit> => {
  const response = await apiPost<AiRecommendationAudit>(`/ai/room/${roomId}/recommendations/${recommendationId}/accept`, {});
  return response.data;
};

export const addUserToEnv = async (envId: number, userId: number) => {
  const response = await apiPost(`/env/${envId}/member/${userId}`);
  return response.data;
};

export const addUserToEnvByEmail = async (envId: number, email: string) => {
  const response = await apiPost(`/env/${envId}/member/${email}`);
  return response.data;
};

export const changeUserRole = async (envId: number, userId: number, role: string) => {
  const response = await apiPatch(`/env/${envId}/member/${userId}`, { role });
  return response.data;
};

export const removeUser = async (envId: number, userId: number) => {
  const response = await apiDelete(`/env/${envId}/member/${userId}`);
  return response.data;
};

export const getRooms = async (envId: number, skip = 0, count = 5) => {
  try {
    const response = await apiGet<RoomsResponse>(`/env/${envId}/room`, {
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
  const response = await apiGet(`/env/${envId}/room/${roomId}`);
  return response.data as Room;
};

export const createRoom = async (envId: number, data: CreateRoomPayload): Promise<Room> => {
  const response = await apiPost(`/env/${envId}/room`, data);
  return response.data;
};

export const updateRoom = async (envId: number, roomId: number, data: UpdateRoomPayload) => {
  const response = await apiPatch(`/env/${envId}/room/${roomId}`, data);
  return response.data;
};

export const getRoomLayout = async (envId: number, roomId: number): Promise<RoomLayout> => {
  const response = await apiGet(`/env/${envId}/room/${roomId}/layout`);
  return response.data;
};

export const updateRoomLayout = async (envId: number, roomId: number, layout: RoomLayout) => {
  const response = await apiPatch(`/env/${envId}/room/${roomId}/layout`, layout);
  return response.data;
};

export const removeRoom = async (envId: number, roomId: number) => {
  const response = await apiDelete(`/env/${envId}/room/${roomId}`);
  return response.data;
};

export const getSensors = async (roomId: number) => {
  const response = await apiGet(`/room/${roomId}/sensor`);
  return response.data as Sensor[];
};

export const addSensor = async (roomId: number, serialNumber: string) => {
  const response = await apiPost(`/room/${roomId}/sensor`, { serial_number: serialNumber });
  return response.data;
};

export const removeSensor = async (roomId: number, sensorId: number) => {
  const response = await apiDelete(`/room/${roomId}/sensor/${sensorId}`);
  return response.data;
};

export const getDevices = async (roomId: number) => {
  const response = await apiGet(`/room/${roomId}/device`);
  return response.data as Device[];
};

export const addDevice = async (roomId: number, serialNumber: string) => {
  const response = await apiPost(`/room/${roomId}/device`, { serial_number: serialNumber });
  return response.data;
};

export const removeDevice = async (roomId: number, deviceId: number) => {
  const response = await apiDelete(`/room/${roomId}/device/${deviceId}`);
  return response.data;
};

export const getAllDevicesHistory = async (roomId: number, from?: string, to?: string) => {
  const response = await apiGet(`/room/${roomId}/history`, {
    params: { interval: "minute", from, to },
  });
  return response.data as HistoryEntry[];
};

export const getDeviceHistory = async (roomId: number, deviceId: number, from?: string, to?: string) => {
  const response = await apiGet(`/room/${roomId}/history/${deviceId}`, {
    params: { interval: "minute", from, to },
  });
  return response.data as HistoryEntry[];
};

export const getEnvironments = async (skip = 0, count = 5): Promise<EnvironmentsResponse> => {
  try {
    const response = await apiGet<EnvironmentsResponse>(`/env`, {
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
  const response = await apiPatch(`/env/${envId}`, data);
  return response.data;
};

export const deleteEnvironment = async (envId: number) => {
  const response = await apiDelete(`/env/${envId}`);
  return response.data;
};

export const getEnvironmentDetails = async (envId: number): Promise<Environment> => {
  const response = await apiGet(`/env/${envId}`);
  return response.data;
};

export const getMembers = async (envId: number, skip = 0, count = 5) => {
  try {
    const response = await apiGet<UsersResponse>(`/env/${envId}/member`, {
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
    const response = await apiPost<Environment>("/env", data);
    return response.data;
  } catch (error) {
    console.error("Failed to create environment:", error);
    throw new Error("Failed to create environment. Please try again.");
  }
};

export async function getAvailableParameters(roomId: number): Promise<Parameter[]> {
  const res = await apiGet(`/room/${roomId}`);
  return res.data;
}

export const getRoomCurve = async (roomId: number, paramName: string): Promise<CurveData> => {
  const response = await apiGet(`/room/${roomId}/${paramName}/curve`);
  return response.data;
};

export const updateRoomCurve = async (roomId: number, paramName: string, data: CurveData) => {
  const response = await apiPatch(`/room/${roomId}/${paramName}/curve`, data);
  return response.data;
};

export const getRoomHistory = async (roomId: number, params: HistoryParams) => {
  const response = await apiGet(`/room/${roomId}/history`, { params });
  return response.data;
};

export const getParameterHistory = async (roomId: number, paramName: string, params: HistoryParams) => {
  const response = await apiGet(`/room/${roomId}/${paramName}/history`, { params });
  return response.data;
};

export const getRoomDevices = async (roomId: number, skip = 0, count = 5) => {
  const response = await apiGet(`/room/${roomId}/device`, {
    params: { skip, count },
  });
  return response.data;
};

export const getRoomSensors = async (roomId: number, skip = 0, count = 5) => {
  const response = await apiGet(`/room/${roomId}/sensor`, {
    params: { skip, count },
  });
  return response.data;
};
