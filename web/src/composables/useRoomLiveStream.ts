import { ref, type Ref } from "vue";
import { createApiRequestConfig } from "@/api";
import type { Device, Sensor } from "@/types/sensor";
import type {
  RoomLiveDeviceEvent,
  RoomLiveHandlers,
  RoomLiveSensorEvent,
  RoomLiveSnapshot,
} from "@/types/live";

type RoomLiveStatus = "idle" | "connecting" | "open" | "error" | "closed";

const reconnectBaseDelayMs = 1000;
const reconnectMaxDelayMs = 15000;

function buildLiveUrl(roomId: number) {
  const config = createApiRequestConfig({ headers: { Accept: "text/event-stream" } });
  const baseUrl = String(config.baseURL ?? "").replace(/\/$/, "");
  return baseUrl ? `${baseUrl}/room/${roomId}/live` : `/room/${roomId}/live`;
}

function buildHeaders() {
  const config = createApiRequestConfig({ headers: { Accept: "text/event-stream" } });
  const headers = new Headers();
  const source = config.headers;

  if (source && typeof source === "object") {
    Object.entries(source as Record<string, unknown>).forEach(([key, value]) => {
      if (value !== null && value !== undefined)
        headers.set(key, String(value));
    });
  }

  headers.set("Accept", "text/event-stream");
  return headers;
}

function getFrameBoundary(buffer: string) {
  const lf = buffer.indexOf("\n\n");
  const crlf = buffer.indexOf("\r\n\r\n");

  if (lf === -1)
    return crlf === -1 ? null : { index: crlf, length: 4 };
  if (crlf === -1)
    return { index: lf, length: 2 };

  return lf < crlf ? { index: lf, length: 2 } : { index: crlf, length: 4 };
}

function parseFrame(frame: string) {
  let eventName = "message";
  const dataLines: string[] = [];

  frame.split(/\r?\n/).forEach((line) => {
    if (!line || line.startsWith(":")) return;

    const separatorIndex = line.indexOf(":");
    const field = separatorIndex === -1 ? line : line.slice(0, separatorIndex);
    let value = separatorIndex === -1 ? "" : line.slice(separatorIndex + 1);
    if (value.startsWith(" ")) value = value.slice(1);

    if (field === "event") eventName = value;
    if (field === "data") dataLines.push(value);
  });

  if (!dataLines.length) return null;
  return {
    eventName,
    payload: JSON.parse(dataLines.join("\n")) as unknown,
  };
}

function dispatchFrame(frame: string, handlers: RoomLiveHandlers) {
  const parsed = parseFrame(frame);
  if (!parsed) return;

  if (parsed.eventName === "snapshot") {
    handlers.snapshot?.(parsed.payload as RoomLiveSnapshot);
  } else if (parsed.eventName === "sensor") {
    handlers.sensor?.(parsed.payload as RoomLiveSensorEvent);
  } else if (parsed.eventName === "device") {
    handlers.device?.(parsed.payload as RoomLiveDeviceEvent);
  }
}

async function readEventStream(response: Response, handlers: RoomLiveHandlers, signal: AbortSignal) {
  if (!response.body)
    throw new Error("Live telemetry stream is not readable.");

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";

  while (!signal.aborted) {
    const { value, done } = await reader.read();
    if (done) break;

    buffer += decoder.decode(value, { stream: true });
    let boundary = getFrameBoundary(buffer);
    while (boundary) {
      const frame = buffer.slice(0, boundary.index);
      buffer = buffer.slice(boundary.index + boundary.length);
      dispatchFrame(frame, handlers);
      boundary = getFrameBoundary(buffer);
    }
  }
}

export function useRoomLiveStream(roomId: number, handlers: RoomLiveHandlers) {
  const status = ref<RoomLiveStatus>("idle");
  const error = ref<string | null>(null);
  const isRunning = ref(false);
  let abortController: AbortController | null = null;
  let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  let reconnectAttempt = 0;

  const clearReconnect = () => {
    if (!reconnectTimer) return;
    clearTimeout(reconnectTimer);
    reconnectTimer = null;
  };

  const connect = async () => {
    if (!isRunning.value || import.meta.server) return;

    clearReconnect();
    const controller = new AbortController();
    abortController = controller;
    status.value = "connecting";

    try {
      const response = await fetch(buildLiveUrl(roomId), {
        method: "GET",
        headers: buildHeaders(),
        signal: controller.signal,
      });

      if (!response.ok)
        throw new Error(`Live telemetry stream failed with ${response.status}`);

      status.value = "open";
      error.value = null;
      reconnectAttempt = 0;
      handlers.open?.();
      await readEventStream(response, handlers, controller.signal);
    } catch (streamError) {
      if (!isRunning.value || controller.signal.aborted) return;

      status.value = "error";
      error.value = streamError instanceof Error ? streamError.message : String(streamError);
      handlers.error?.(streamError);
    } finally {
      if (abortController === controller)
        abortController = null;
    }

    if (!isRunning.value) return;

    const delay = Math.min(
      reconnectBaseDelayMs * 2 ** reconnectAttempt,
      reconnectMaxDelayMs,
    );
    reconnectAttempt += 1;
    reconnectTimer = setTimeout(() => void connect(), delay);
  };

  const start = () => {
    if (isRunning.value || import.meta.server) return;

    isRunning.value = true;
    reconnectAttempt = 0;
    void connect();
  };

  const stop = () => {
    isRunning.value = false;
    clearReconnect();
    abortController?.abort();
    abortController = null;
    status.value = "closed";
  };

  return {
    status,
    error,
    start,
    stop,
  };
}

export function patchSensorSnapshot(sensors: Ref<Sensor[]>, snapshotSensors: Sensor[]) {
  snapshotSensors.forEach((snapshotSensor) => {
    const index = sensors.value.findIndex((sensor) => sensor.id === snapshotSensor.id);
    if (index >= 0)
      sensors.value.splice(index, 1, snapshotSensor);
  });
  sensors.value = [...sensors.value];
}

export function patchDeviceSnapshot(devices: Ref<Device[]>, snapshotDevices: Device[]) {
  snapshotDevices.forEach((snapshotDevice) => {
    const index = devices.value.findIndex((device) => device.id === snapshotDevice.id);
    if (index >= 0)
      devices.value.splice(index, 1, snapshotDevice);
  });
  devices.value = [...devices.value];
}

export function applySensorLiveEvent(sensors: Ref<Sensor[]>, event: RoomLiveSensorEvent) {
  if (!event.parameter || event.value === null || event.value === undefined)
    return;

  const index = sensors.value.findIndex((sensor) => (
    (event.sensor_id !== null && event.sensor_id !== undefined && sensor.id === event.sensor_id)
    || (event.sensor_serial_number && sensor.serial_number === event.sensor_serial_number)
  ));

  if (index < 0) return;

  const sensor = { ...sensors.value[index] };
  const parameters = [...(sensor.parameters ?? [])];
  const parameterIndex = parameters.findIndex((parameter) => parameter.name === event.parameter);

  if (parameterIndex >= 0) {
    parameters.splice(parameterIndex, 1, {
      ...parameters[parameterIndex],
      value: event.value,
    });
  } else {
    parameters.push({
      name: event.parameter,
      value: event.value,
      unit: "",
      min_value: 0,
      max_value: 0,
      critical_value: 0,
    });
  }

  sensor.parameters = parameters;
  sensors.value.splice(index, 1, sensor);
  sensors.value = [...sensors.value];
}

export function applyDeviceLiveEvent(devices: Ref<Device[]>, event: RoomLiveDeviceEvent) {
  if (event.fan_speed === null || event.fan_speed === undefined)
    return;

  const activeAt = event.active_at ?? Math.floor(Date.now() / 1000);
  const patchDevice = (device: Device): Device => ({
    ...device,
    fan_speed: event.fan_speed ?? device.fan_speed,
    active_at: activeAt,
  });

  if (event.device_id === null || event.device_id === undefined) {
    devices.value = devices.value.map(patchDevice);
    return;
  }

  const index = devices.value.findIndex((device) => (
    device.id === event.device_id
    || (event.device_serial_number && device.serial_number === event.device_serial_number)
  ));

  if (index < 0) return;

  devices.value.splice(index, 1, patchDevice(devices.value[index]));
  devices.value = [...devices.value];
}
