import { getRoom } from "@/services/apiService";
import { useEnvironmentStore } from "@/store/environmentStore";
import { useSensorStore } from "@/store/sensorStore";
import { useDeviceStore } from "@/store/deviceStore";

type RouteParamsGeneric = Record<string, string | string[]>;

const breadcrumbCache = new Map<string, string>();

export type BreadcrumbConfig = {
  label: string;
  path?: string;
  fetchData: (params: RouteParamsGeneric) => Promise<string>;
};

export const breadcrumbConfig: Record<string, BreadcrumbConfig> = {
  envId: {
    label: "Environment",
    path: "environment",
    fetchData: async (params: RouteParamsGeneric): Promise<string> => {
      const envId = Number(params.envId);

      const environmentStore = useEnvironmentStore();
      const env = await environmentStore.fetchEnvironment(envId);
      return env?.name || `Environment ${envId}`;
    },
  },
  roomId: {
    label: "Room",
    path: "room",
    fetchData: async (params: RouteParamsGeneric): Promise<string> => {
      const envId = Number(params.envId);
      const roomId = Number(params.roomId);

      const cacheKey = `room-${envId}-${roomId}`;
      if (breadcrumbCache.has(cacheKey))
        return breadcrumbCache.get(cacheKey) || `Room ${roomId}`;

      const room = await getRoom(envId, roomId);
      const name = room?.name || `Room ${roomId}`;
      breadcrumbCache.set(cacheKey, name);
      return name;
    },
  },
 deviceId: {
   label: "Device",
   path: "device",
   fetchData: async (params: RouteParamsGeneric): Promise<string> => {
     const roomId = Number(params.roomId);
     const deviceId = Number(params.deviceId);

     const deviceStore = useDeviceStore();
     const device = await deviceStore.fetchDevice(roomId, deviceId);
     return `Device #${device?.id ?? deviceId}`;
   },
 },
  sensorId: {
    label: "Sensor",
    path: "sensor",
    fetchData: async (params: RouteParamsGeneric): Promise<string> => {
      const roomId = Number(params.roomId);
      const sensorId = Number(params.sensorId);

      const sensorStore = useSensorStore();
      const sensor = await sensorStore.fetchSensor(roomId, sensorId);
      return sensor == null ? `Sensor #${sensorId}` : `${sensor.type_name} #${sensor.id}`;
    },
  },
};
