import { ref } from "vue";
import { createApiRequestConfig } from "@/api";
import type { UserNotification } from "@/types/notification";

type NotificationLiveStatus = "idle" | "connecting" | "open" | "error" | "closed";

interface NotificationLiveHandlers {
  notification?: (notification: UserNotification) => void;
  open?: () => void;
  error?: (error: unknown) => void;
}

const reconnectBaseDelayMs = 1000;
const reconnectMaxDelayMs = 15000;

function buildLiveUrl() {
  const config = createApiRequestConfig({ headers: { Accept: "text/event-stream" } });
  const baseUrl = String(config.baseURL ?? "").replace(/\/$/, "");
  return baseUrl ? `${baseUrl}/notifications/live` : "/notifications/live";
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

function dispatchFrame(frame: string, handlers: NotificationLiveHandlers) {
  const parsed = parseFrame(frame);
  if (!parsed) return;

  if (parsed.eventName === "notification")
    handlers.notification?.(parsed.payload as UserNotification);
}

async function readEventStream(response: Response, handlers: NotificationLiveHandlers, signal: AbortSignal) {
  if (!response.body)
    throw new Error("Notification stream is not readable.");

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

export function useNotificationLiveStream(handlers: NotificationLiveHandlers) {
  const status = ref<NotificationLiveStatus>("idle");
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
      const response = await fetch(buildLiveUrl(), {
        method: "GET",
        headers: buildHeaders(),
        signal: controller.signal,
      });

      if (!response.ok)
        throw new Error(`Notification stream failed with ${response.status}`);

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
