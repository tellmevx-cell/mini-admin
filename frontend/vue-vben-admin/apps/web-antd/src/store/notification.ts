import type { NotificationItem } from '@vben/layouts';

import { computed, ref } from 'vue';

import { defineStore } from 'pinia';

import {
  clearNotificationsApi,
  deleteNotificationApi,
  getMyNotificationsApi,
  markAllNotificationsReadApi,
  markNotificationReadApi,
  type UserNotificationItem,
} from '#/api';

const notificationAvatar =
  'data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 40 40%22%3E%3Crect width=%2240%22 height=%2240%22 rx=%2220%22 fill=%22%232563eb%22/%3E%3Cpath d=%22M20 9a7 7 0 0 0-7 7v4.9l-2.2 4.1h18.4L27 20.9V16a7 7 0 0 0-7-7Zm-3.4 18a3.7 3.7 0 0 0 6.8 0Z%22 fill=%22white%22/%3E%3C/svg%3E';

function formatNotificationDate(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat('zh-CN', {
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    month: '2-digit',
  }).format(date);
}

function mapNotification(item: UserNotificationItem): NotificationItem {
  return {
    ...item,
    avatar: notificationAvatar,
    date: formatNotificationDate(item.createdAt),
    id: item.id,
    isRead: item.isRead,
    link: item.link ?? undefined,
    message: item.message,
    title: item.title,
  };
}

function resolveNotificationId(itemOrId: NotificationItem | string) {
  return typeof itemOrId === 'string' ? itemOrId : String(itemOrId.id);
}

export const useNotificationStore = defineStore('notification', () => {
  const notifications = ref<NotificationItem[]>([]);
  const unreadCount = ref(0);
  const loading = ref(false);
  const loadedAt = ref('');

  const showDot = computed(() => unreadCount.value > 0);

  async function loadRecent(options: { silent?: boolean } = {}) {
    if (loading.value) {
      return;
    }

    loading.value = true;
    try {
      const result = await getMyNotificationsApi(20);
      notifications.value = result.items.map(mapNotification);
      syncUnreadCount(result.unreadCount);
      loadedAt.value = new Date().toISOString();
    } catch (error) {
      if (!options.silent) {
        console.warn('[notification] failed to load notifications', error);
      }
    } finally {
      loading.value = false;
    }
  }

  function syncUnreadCount(count: number) {
    unreadCount.value = Math.max(0, count);
  }

  async function markRead(itemOrId: NotificationItem | string) {
    const id = resolveNotificationId(itemOrId);
    const notification = notifications.value.find((item) => item.id === id);
    if (notification?.isRead) {
      return;
    }

    await markNotificationReadApi(id);
    if (notification) {
      notification.isRead = true;
    }
    syncUnreadCount(unreadCount.value - 1);
  }

  async function markAllRead() {
    await markAllNotificationsReadApi();
    notifications.value.forEach((item) => {
      item.isRead = true;
    });
    syncUnreadCount(0);
  }

  async function remove(itemOrId: NotificationItem | string) {
    const id = resolveNotificationId(itemOrId);
    const notification = notifications.value.find((item) => item.id === id);

    await deleteNotificationApi(id);
    notifications.value = notifications.value.filter((item) => item.id !== id);
    if (notification && !notification.isRead) {
      syncUnreadCount(unreadCount.value - 1);
    }
  }

  async function clearAll() {
    await clearNotificationsApi();
    notifications.value = [];
    syncUnreadCount(0);
  }

  function $reset() {
    notifications.value = [];
    unreadCount.value = 0;
    loading.value = false;
    loadedAt.value = '';
  }

  return {
    $reset,
    clearAll,
    loadedAt,
    loading,
    loadRecent,
    markAllRead,
    markRead,
    notifications,
    remove,
    showDot,
    syncUnreadCount,
    unreadCount,
  };
});
