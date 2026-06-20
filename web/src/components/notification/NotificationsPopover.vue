<template>
  <div class="notifications">
    <Button
      icon="pi pi-bell"
      severity="secondary"
      variant="text"
      aria-label="Notifications"
      class="notifications__button"
      :class="{ 'notifications__button--active': isOpen }"
      @click="toggle"
    />
    <span v-if="unreadCount > 0" class="notifications__badge">{{ unreadLabel }}</span>

    <Popover ref="popover" class="notifications-popover" @show="onShow" @hide="isOpen = false">
      <section class="notifications-panel" aria-label="Notifications history">
        <header class="notifications-panel__header">
          <div>
            <span class="notifications-panel__eyebrow">Notifications</span>
            <h2>History</h2>
          </div>
          <Button
            v-if="unreadCount > 0"
            label="Mark read"
            icon="pi pi-check"
            severity="secondary"
            variant="text"
            size="small"
            :loading="isMarkingAll"
            @click="markAllRead"
          />
        </header>

        <div v-if="isLoading" class="notifications-panel__list">
          <Skeleton v-for="index in 4" :key="index" height="4.5rem" />
        </div>

        <div v-else-if="notifications.length === 0" class="notifications-panel__empty">
          <i class="pi pi-bell" />
          <strong>No notifications</strong>
          <span>Critical room events will appear here.</span>
        </div>

        <div v-else class="notifications-panel__list">
          <button
            v-for="notification in notifications"
            :key="notification.id"
            type="button"
            class="notification-item"
            :class="{ 'notification-item--unread': !notification.is_read }"
            @click="openNotification(notification)"
          >
            <span class="notification-item__status" aria-hidden="true" />
            <span class="notification-item__content">
              <span class="notification-item__topline">
                <strong>{{ notification.title }}</strong>
                <time>{{ formatTime(notification.created_at) }}</time>
              </span>
              <span class="notification-item__body">{{ notification.body }}</span>
              <span class="notification-item__meta">
                <Tag
                  :severity="getSeverity(notification.severity)"
                  :value="getSeverityLabel(notification.severity)"
                  rounded
                />
                <span v-if="getNotificationContext(notification)" class="notification-item__context">
                  {{ getNotificationContext(notification) }}
                </span>
              </span>
            </span>
          </button>
        </div>

        <footer v-if="hasMore" class="notifications-panel__footer">
          <Button
            label="Load more"
            icon="pi pi-angle-down"
            severity="secondary"
            variant="text"
            size="small"
            :loading="isLoadingMore"
            @click="loadMore"
          />
        </footer>
      </section>
    </Popover>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import Button from "primevue/button";
import Popover from "primevue/popover";
import Skeleton from "primevue/skeleton";
import Tag from "primevue/tag";
import { useToast } from "primevue/usetoast";
import {
  getNotifications,
  getUnreadNotificationsCount,
  markAllNotificationsRead,
  markNotificationRead,
} from "@/services/apiService";
import { useNotificationLiveStream } from "@/composables/useNotificationLiveStream";
import { PARAMETER_LABELS } from "@/types/sensor";
import type { UserNotification } from "@/types/notification";

const pageSize = 10;
const router = useRouter();
const toast = useToast();
const popover = ref();
const notifications = ref<UserNotification[]>([]);
const totalCount = ref(0);
const unreadCount = ref(0);
const isLoading = ref(false);
const isLoadingMore = ref(false);
const isMarkingAll = ref(false);
const isOpen = ref(false);

const unreadLabel = computed(() => unreadCount.value > 99 ? "99+" : String(unreadCount.value));
const hasMore = computed(() => notifications.value.length < totalCount.value);
const notificationStream = useNotificationLiveStream({
  notification: handleLiveNotification,
  error: (streamError) => {
    console.error("Notification stream error:", streamError);
  },
});

async function refreshUnreadCount() {
  try {
    const response = await getUnreadNotificationsCount();
    unreadCount.value = response.unread_count;
  } catch (error) {
    console.error("Failed to load unread notification count:", error);
  }
}

async function loadNotifications(options: { append?: boolean } = {}) {
  const skip = options.append ? notifications.value.length : 0;
  if (options.append) {
    isLoadingMore.value = true;
  } else {
    isLoading.value = true;
  }

  try {
    const response = await getNotifications(skip, pageSize);
    notifications.value = options.append
      ? [...notifications.value, ...response.data]
      : response.data;
    totalCount.value = response.pagination.total;
  } catch (error) {
    console.error("Failed to load notifications:", error);
  } finally {
    isLoading.value = false;
    isLoadingMore.value = false;
  }
}

function toggle(event: Event) {
  popover.value?.toggle(event);
}

async function onShow() {
  isOpen.value = true;
  await Promise.all([
    loadNotifications(),
    refreshUnreadCount(),
  ]);
}

async function loadMore() {
  await loadNotifications({ append: true });
}

function handleLiveNotification(notification: UserNotification) {
  const exists = notifications.value.some((item) => item.id === notification.id);
  if (!exists) {
    notifications.value = [notification, ...notifications.value];
    totalCount.value += 1;

    if (!notification.is_read)
      unreadCount.value += 1;
  }

  toast.add({
    severity: getToastSeverity(notification.severity),
    summary: notification.title || "New notification",
    detail: notification.body,
    life: notification.severity === "critical" ? 8000 : 5500,
  });
}

async function markAllRead() {
  isMarkingAll.value = true;
  try {
    await markAllNotificationsRead();
    unreadCount.value = 0;
    notifications.value = notifications.value.map((notification) => ({
      ...notification,
      is_read: true,
      read_at: notification.read_at ?? Math.floor(Date.now() / 1000),
    }));
  } catch (error) {
    console.error("Failed to mark notifications read:", error);
  } finally {
    isMarkingAll.value = false;
  }
}

async function openNotification(notification: UserNotification) {
  if (!notification.is_read) {
    try {
      await markNotificationRead(notification.id);
      unreadCount.value = Math.max(0, unreadCount.value - 1);
      notifications.value = notifications.value.map((item) => (
        item.id === notification.id
          ? { ...item, is_read: true, read_at: Math.floor(Date.now() / 1000) }
          : item
      ));
    } catch (error) {
      console.error("Failed to mark notification read:", error);
    }
  }

  const target = getNotificationTarget(notification);
  if (target) {
    popover.value?.hide();
    await router.push(target);
  }
}

function getNotificationTarget(notification: UserNotification) {
  const data = notification.data ?? {};
  const envId = Number(data.environment_id);
  const roomId = Number(data.room_id);

  if (Number.isFinite(envId) && Number.isFinite(roomId) && envId > 0 && roomId > 0)
    return `/env/${envId}/room/${roomId}/parameters`;

  if (Number.isFinite(envId) && envId > 0)
    return `/dashboard/env/${envId}/rooms`;

  return null;
}

function getNotificationContext(notification: UserNotification) {
  const data = notification.data ?? {};
  const parts: string[] = [];
  if (data.room_id) parts.push(`ROOM-${data.room_id}`);
  if (data.sensor_id) parts.push(`SENSOR-${data.sensor_id}`);
  if (data.parameter) parts.push(PARAMETER_LABELS[data.parameter] || data.parameter);
  return parts.join(" / ");
}

function getSeverity(severity: string) {
  if (severity === "critical") return "danger";
  if (severity === "warning") return "warn";
  if (severity === "success") return "success";
  return "info";
}

function getToastSeverity(severity: string) {
  if (severity === "critical") return "error";
  if (severity === "warning") return "warn";
  if (severity === "success") return "success";
  return "info";
}

function getSeverityLabel(severity: string) {
  if (severity === "critical") return "Critical";
  if (severity === "warning") return "Warning";
  if (severity === "success") return "Resolved";
  return "Info";
}

function formatTime(timestampSeconds: number) {
  const timestampMs = timestampSeconds * 1000;
  const diffMs = Date.now() - timestampMs;
  if (diffMs < 60_000) return "now";
  if (diffMs < 3_600_000) return `${Math.floor(diffMs / 60_000)}m`;
  if (diffMs < 86_400_000) return `${Math.floor(diffMs / 3_600_000)}h`;

  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
  }).format(new Date(timestampMs));
}

onMounted(() => {
  void refreshUnreadCount();
  notificationStream.start();
});

onBeforeUnmount(() => {
  notificationStream.stop();
});
</script>

<style scoped>
.notifications {
  height: 36px;
  position: relative;
  width: 36px;
}

.notifications__button {
  color: var(--app-sidebar-muted);
  height: 36px;
  justify-content: center;
  min-height: 36px;
  padding: 0;
  width: 36px;
}

.notifications__button:hover,
.notifications__button--active {
  background: var(--app-sidebar-subtle);
  color: var(--app-sidebar-text);
}

.notifications__badge {
  align-items: center;
  background: var(--app-danger, #ef4444);
  border: 2px solid var(--app-sidebar-bg);
  border-radius: 999px;
  color: white;
  display: inline-flex;
  font-size: 0.62rem;
  font-weight: 800;
  height: 18px;
  justify-content: center;
  line-height: 1;
  min-width: 18px;
  padding: 0 4px;
  pointer-events: none;
  position: absolute;
  right: -4px;
  top: -5px;
}

.notifications-panel {
  background: var(--app-surface);
  border-radius: var(--app-radius);
  display: flex;
  flex-direction: column;
  gap: 10px;
  max-height: min(36rem, calc(100vh - 2rem));
  overflow: hidden;
  padding: 12px;
  width: min(26rem, calc(100vw - 1.5rem));
}

.notifications-panel__header {
  align-items: center;
  border-bottom: 1px solid var(--app-border);
  display: flex;
  justify-content: space-between;
  gap: 10px;
  padding-bottom: 10px;
}

.notifications-panel__eyebrow {
  color: var(--app-muted);
  display: block;
  font-family: var(--app-mono);
  font-size: 0.66rem;
  font-weight: 760;
  line-height: 1rem;
  text-transform: uppercase;
}

.notifications-panel__header h2 {
  color: var(--app-text);
  font-size: 1rem;
  font-weight: 780;
  line-height: 1.35;
  margin: 0;
}

.notifications-panel__list {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-height: 0;
  overflow: hidden auto;
  padding-right: 2px;
}

.notification-item {
  align-items: start;
  background: var(--app-surface-soft);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  color: var(--app-text);
  cursor: pointer;
  display: grid;
  gap: 8px;
  grid-template-columns: 8px minmax(0, 1fr);
  padding: 10px;
  text-align: left;
  width: 100%;
}

.notification-item:hover {
  background: color-mix(in srgb, var(--app-primary) 7%, var(--app-surface-soft));
  border-color: color-mix(in srgb, var(--app-primary) 32%, var(--app-border));
}

.notification-item__status {
  background: transparent;
  border-radius: 999px;
  height: 8px;
  margin-top: 5px;
  width: 8px;
}

.notification-item--unread .notification-item__status {
  background: var(--app-primary);
}

.notification-item__content,
.notification-item__topline,
.notification-item__meta {
  min-width: 0;
}

.notification-item__content {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.notification-item__topline {
  align-items: start;
  display: flex;
  gap: 10px;
  justify-content: space-between;
}

.notification-item__topline strong {
  font-size: 0.86rem;
  line-height: 1.25;
}

.notification-item__topline time {
  color: var(--app-muted);
  flex: 0 0 auto;
  font-family: var(--app-mono);
  font-size: 0.68rem;
}

.notification-item__body {
  color: var(--app-muted);
  font-size: 0.8rem;
  line-height: 1.35;
}

.notification-item__meta {
  align-items: center;
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.notification-item__context {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
}

.notifications-panel__empty {
  align-items: center;
  color: var(--app-muted);
  display: flex;
  flex-direction: column;
  gap: 6px;
  justify-content: center;
  min-height: 11rem;
  text-align: center;
}

.notifications-panel__empty i {
  color: var(--app-primary);
  font-size: 1.4rem;
}

.notifications-panel__empty strong {
  color: var(--app-text);
  font-size: 0.95rem;
}

.notifications-panel__empty span {
  font-size: 0.8rem;
}

.notifications-panel__footer {
  border-top: 1px solid var(--app-border);
  display: flex;
  justify-content: center;
  padding-top: 8px;
}
</style>
