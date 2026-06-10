<script lang="ts" setup>
import type { NotificationItem } from '@vben/layouts';

import { computed, onBeforeUnmount, watch } from 'vue';
import { useRouter } from 'vue-router';

import { AuthenticationLoginExpiredModal } from '@vben/common-ui';
import { VBEN_DOC_URL, VBEN_GITHUB_URL } from '@vben/constants';
import { BookOpenText, CircleHelp, SvgGithubIcon } from '@vben/icons';
import {
  BasicLayout,
  LockScreen,
  Notification,
  UserDropdown,
} from '@vben/layouts';
import { preferences } from '@vben/preferences';
import { useAccessStore, useUserStore } from '@vben/stores';
import { openWindow } from '@vben/utils';

import { $t } from '#/locales';
import { createRouteLocationFromLink } from '#/router/link';
import { useAuthStore, useNotificationStore } from '#/store';
import LoginForm from '#/views/_core/authentication/login.vue';

const NOTIFICATION_POLL_INTERVAL_MS = 30_000;

let notificationRefreshTimer: ReturnType<typeof setInterval> | undefined;

const router = useRouter();
const userStore = useUserStore();
const authStore = useAuthStore();
const notificationStore = useNotificationStore();
const accessStore = useAccessStore();

const menus = computed(() => [
  {
    handler: () => {
      router.push({ name: 'Profile' });
    },
    icon: 'lucide:user',
    text: $t('page.auth.profile'),
  },
  {
    handler: () => {
      openWindow(VBEN_DOC_URL, {
        target: '_blank',
      });
    },
    icon: BookOpenText,
    text: $t('ui.widgets.document'),
  },
  {
    handler: () => {
      openWindow(VBEN_GITHUB_URL, {
        target: '_blank',
      });
    },
    icon: SvgGithubIcon,
    text: 'GitHub',
  },
  {
    handler: () => {
      openWindow(`${VBEN_GITHUB_URL}/issues`, {
        target: '_blank',
      });
    },
    icon: CircleHelp,
    text: $t('ui.widgets.qa'),
  },
]);

const avatar = computed(() => {
  return userStore.userInfo?.avatar ?? preferences.app.defaultAvatar;
});

async function handleLogout() {
  await authStore.logout(false);
}

async function loadNotifications() {
  if (!accessStore.accessToken) {
    return;
  }

  await notificationStore.loadRecent({ silent: true });
}

function startNotificationPolling() {
  stopNotificationPolling();
  window.addEventListener('focus', handleWindowFocus);
  document.addEventListener('visibilitychange', handleVisibilityChange);
  notificationRefreshTimer = setInterval(() => {
    void loadNotifications();
  }, NOTIFICATION_POLL_INTERVAL_MS);
}

function stopNotificationPolling() {
  if (notificationRefreshTimer) {
    clearInterval(notificationRefreshTimer);
    notificationRefreshTimer = undefined;
  }

  window.removeEventListener('focus', handleWindowFocus);
  document.removeEventListener('visibilitychange', handleVisibilityChange);
}

function handleWindowFocus() {
  void loadNotifications();
}

function handleVisibilityChange() {
  if (document.visibilityState === 'visible') {
    void loadNotifications();
  }
}

async function handleNoticeClear() {
  await notificationStore.clearAll();
}

async function markRead(item: NotificationItem) {
  if (!item.id || item.isRead) {
    return;
  }

  await notificationStore.markRead(item);
}

async function remove(item: NotificationItem) {
  if (!item.id) {
    return;
  }

  await notificationStore.remove(item);
}

async function handleMakeAll() {
  await notificationStore.markAllRead();
}

const viewAll = () => {
  router.push({ path: '/system/notification' });
};

const handleClick = async (item: NotificationItem) => {
  await markRead(item);
  if (item.link) {
    navigateTo(item.link, item.query, item.state);
  }
};

function handleNotificationOpenChange(open: boolean) {
  if (open) {
    void loadNotifications();
  }
}

function navigateTo(
  link: string,
  query?: Record<string, any>,
  state?: Record<string, any>,
) {
  if (link.startsWith('http://') || link.startsWith('https://')) {
    // 外部链接，在新标签页打开
    window.open(link, '_blank');
  } else {
    // 内部路由链接，支持 query 参数和 state
    router.push(createRouteLocationFromLink(link, query, state));
  }
}

watch(
  () => accessStore.accessToken,
  (token) => {
    if (token) {
      void loadNotifications();
      startNotificationPolling();
    } else {
      stopNotificationPolling();
      notificationStore.$reset();
    }
  },
  { immediate: true },
);

onBeforeUnmount(stopNotificationPolling);
</script>

<template>
  <BasicLayout @clear-preferences-and-logout="handleLogout">
    <template #user-dropdown>
      <UserDropdown
        :avatar
        :menus
        :text="userStore.userInfo?.realName"
        description="ann.vben@gmail.com"
        tag-text="Pro"
        @logout="handleLogout"
        @clear-preferences-and-logout="handleLogout"
      />
    </template>
    <template #notification>
      <Notification
        :count="notificationStore.unreadCount"
        :dot="notificationStore.showDot"
        :notifications="notificationStore.notifications"
        @clear="handleNoticeClear"
        @read="markRead"
        @remove="remove"
        @make-all="handleMakeAll"
        @on-click="handleClick"
        @open-change="handleNotificationOpenChange"
        @view-all="viewAll"
      />
    </template>
    <template #extra>
      <AuthenticationLoginExpiredModal
        v-model:open="accessStore.loginExpired"
        :avatar
      >
        <LoginForm />
      </AuthenticationLoginExpiredModal>
    </template>
    <template #lock-screen>
      <LockScreen :avatar @to-login="handleLogout" />
    </template>
  </BasicLayout>
</template>
