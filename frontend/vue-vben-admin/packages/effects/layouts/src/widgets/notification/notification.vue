<script lang="ts" setup>
import type { NotificationItem } from './types';

import { computed } from 'vue';

import { Bell, CircleCheckBig, CircleX, MailCheck } from '@vben/icons';
import { $t } from '@vben/locales';

import {
  VbenButton,
  VbenIconButton,
  VbenPopover,
  VbenScrollbar,
} from '@vben-core/shadcn-ui';

import { useToggle } from '@vueuse/core';

defineOptions({ name: 'NotificationPopup' });

const props = withDefaults(
  defineProps<{
    /** 未读数量 */
    count?: number;
    /** 显示圆点 */
    dot?: boolean;
    /** 消息列表 */
    notifications?: NotificationItem[];
  }>(),
  {
    count: 0,
    dot: false,
    notifications: () => [],
  },
);

const emit = defineEmits<{
  clear: [];
  makeAll: [];
  onClick: [NotificationItem];
  openChange: [boolean];
  read: [NotificationItem];
  remove: [NotificationItem];
  viewAll: [];
}>();

const [open, toggle] = useToggle();

const badgeText = computed(() => {
  const count = Math.max(0, props.count ?? 0);
  if (count <= 0) {
    return '';
  }

  return count > 99 ? '99+' : String(count);
});

const close = () => {
  open.value = false;
  emit('openChange', false);
};

const handleTriggerClick = () => {
  toggle();
  emit('openChange', open.value);
};

const handleViewAll = () => {
  emit('viewAll');
  close();
};

const handleMakeAll = () => {
  emit('makeAll');
};

const handleClear = () => {
  emit('clear');
};
</script>
<template>
  <VbenPopover v-model:open="open" content-class="relative right-2 w-90 p-0">
    <template #trigger>
      <div class="mr-2 flex-center h-full" @click.stop="handleTriggerClick">
        <VbenIconButton class="bell-button relative text-foreground">
          <span
            v-if="badgeText"
            class="absolute -top-1 -right-1 z-10 min-w-4 rounded-full bg-destructive px-1 text-center text-[10px] leading-4 text-destructive-foreground shadow-sm"
          >
            {{ badgeText }}
          </span>
          <span
            v-else-if="dot"
            class="absolute top-0.5 right-0.5 size-2 rounded-sm bg-primary"
          ></span>
          <Bell class="size-4" />
        </VbenIconButton>
      </div>
    </template>

    <div class="relative">
      <div class="flex items-center justify-between p-4 py-3">
        <div class="text-foreground">{{ $t('ui.widgets.notifications') }}</div>
        <VbenIconButton
          :disabled="notifications.length <= 0"
          :tooltip="$t('ui.widgets.markAllAsRead')"
          @click="handleMakeAll"
        >
          <MailCheck class="size-4" />
        </VbenIconButton>
      </div>
      <VbenScrollbar v-if="notifications.length > 0">
        <ul class="flex! max-h-90 w-full flex-col">
          <template v-for="item in notifications" :key="item.id ?? item.title">
            <li
              class="relative flex w-full cursor-pointer items-start gap-5 border-t border-border p-3 hover:bg-accent"
              @click="emit('onClick', item)"
            >
              <slot name="content" :item="item">
                <span
                  v-if="!item.isRead"
                  class="absolute top-2 right-2 size-2 rounded-sm bg-primary"
                ></span>

                <span
                  class="relative flex size-10 shrink-0 overflow-hidden rounded-full"
                >
                  <img
                    :src="item.avatar"
                    class="aspect-square size-full object-cover"
                  />
                </span>
                <div class="flex flex-col gap-1 leading-none">
                  <p class="font-semibold">{{ item.title }}</p>
                  <p class="my-1 line-clamp-2 text-xs text-muted-foreground">
                    {{ item.message }}
                  </p>
                  <p class="line-clamp-2 text-xs text-muted-foreground">
                    {{ item.date }}
                  </p>
                </div>
                <div
                  class="absolute top-1/2 right-3 flex -translate-y-1/2 flex-row gap-1"
                >
                  <slot name="action" :item="item">
                    <slot name="action-prepend" :item="item"></slot>
                    <VbenIconButton
                      v-if="!item.isRead"
                      size="xs"
                      variant="ghost"
                      class="h-6 px-2"
                      :tooltip="$t('common.confirm')"
                      @click.stop="emit('read', item)"
                    >
                      <CircleCheckBig class="size-4" />
                    </VbenIconButton>
                    <VbenIconButton
                      v-if="item.isRead"
                      size="xs"
                      variant="ghost"
                      class="h-6 px-2 text-destructive"
                      :tooltip="$t('common.delete')"
                      @click.stop="emit('remove', item)"
                    >
                      <CircleX class="size-4" />
                    </VbenIconButton>
                    <slot name="action-append" :item="item"></slot>
                  </slot>
                </div>
              </slot>
            </li>
          </template>
        </ul>
      </VbenScrollbar>

      <template v-else>
        <div class="flex-center min-h-37.5 w-full text-muted-foreground">
          {{ $t('common.noData') }}
        </div>
      </template>

      <div
        class="flex items-center justify-between border-t border-border px-4 py-3"
      >
        <VbenButton
          :disabled="notifications.length <= 0"
          size="sm"
          variant="ghost"
          @click="handleClear"
        >
          {{ $t('ui.widgets.clearNotifications') }}
        </VbenButton>
        <VbenButton size="sm" @click="handleViewAll">
          {{ $t('ui.widgets.viewAll') }}
        </VbenButton>
      </div>
    </div>
  </VbenPopover>
</template>

<style scoped>
:deep(.bell-button) {
  &:hover {
    svg {
      animation: bell-ring 1s both;
    }
  }
}

@keyframes bell-ring {
  0%,
  100% {
    transform-origin: top;
  }

  15% {
    transform: rotateZ(10deg);
  }

  30% {
    transform: rotateZ(-10deg);
  }

  45% {
    transform: rotateZ(5deg);
  }

  60% {
    transform: rotateZ(-5deg);
  }

  75% {
    transform: rotateZ(2deg);
  }
}
</style>
