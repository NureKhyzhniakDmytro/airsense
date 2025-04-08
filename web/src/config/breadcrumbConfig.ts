import { getRoom } from "@/services/apiService";
import { useEnvironmentStore } from "@/store/environmentStore";
import { useSensorStore } from "@/store/sensorStore";
import { useDeviceStore } from "@/store/deviceStore";

const breadcrumbCache = new Map<string, string>();

export const breadcrumbConfig = {
  envId: {
    label: "Середовище",
    path: (params: number[]) => `/env/${params[0]}`,
    fetchData: async (params: number[]): Promise<string> => {
      const environmentStore = useEnvironmentStore();
      const env = await environmentStore.fetchEnvironment(params[0]);
      return env?.name || `Середовище ${params[0]}`;
    },
  },
  roomId: {
    label: "Кімната",
    dependsOn: "envId",
    path: (params: number[]) => `/env/${params[0]}/room/${params[1]}`,
    fetchData: async (params: number[]): Promise<string> => {
      const cacheKey = `room-${params[0]}-${params[1]}`;
      if (breadcrumbCache.has(cacheKey))
        return breadcrumbCache.get(cacheKey) || `Кімната ${params[1]}`;

      const room = await getRoom(params[0], params[1]);
      const name = room?.name || `Кімната ${params[1]}`;
      breadcrumbCache.set(cacheKey, name);
      return name;
    },
  },
 deviceId: {
   label: "Пристрій",
   dependsOn: "roomId",
   path: (params: number[]) => `/env/${params[0]}/room/${params[1]}/devices/${params[2]}`,
   fetchData: async (params: number[]): Promise<string> => {
     const deviceStore = useDeviceStore();
     const device = await deviceStore.fetchDevice(params[1], params[2]);
     return `Пристрій #${device?.id}`;
   },
 },
  sensorId: {
    label: "Датчик",
    dependsOn: "roomId",
    path: (params: number[]) => `/env/${params[0]}/room/${params[1]}/sensors/${params[2]}`,
    fetchData: async (params: number[]): Promise<string> => {
      const sensorStore = useSensorStore();
      const sensor = await sensorStore.fetchSensor(params[1], params[2]);
      return sensor == null ? `Датчик #${params[2]}` : `${sensor.type_name} #${sensor.id}`;
    },
  },
};
