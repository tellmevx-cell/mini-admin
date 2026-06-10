<script setup lang="ts">
import { computed } from 'vue';

import { usePreferences } from '@vben/preferences';
import { useAccessStore, useUserStore } from '@vben/stores';

import { useBrandingStore } from '#/store';

defineOptions({ name: 'GlobalWatermark' });

const brandingStore = useBrandingStore();
const accessStore = useAccessStore();
const userStore = useUserStore();
const { isDark } = usePreferences();

const items = Array.from({ length: 80 }, (_, index) => index);
const enabled = computed(
  () => Boolean(accessStore.accessToken) && brandingStore.watermarkEnabled,
);
const content = computed(() => {
  if (brandingStore.watermarkText) {
    return brandingStore.watermarkText;
  }

  const userText = [
    userStore.userInfo?.username,
    userStore.userInfo?.realName,
  ].filter(Boolean).join(' - ');

  return userText || brandingStore.name;
});
</script>

<template>
  <div
    v-if="enabled"
    aria-hidden="true"
    class="mini-global-watermark"
    :class="{ 'is-dark': isDark }"
  >
    <span
      v-for="item in items"
      :key="item"
      class="mini-global-watermark__item"
    >
      {{ content }}
    </span>
  </div>
</template>

<style scoped>
.mini-global-watermark {
  position: fixed;
  inset: 0;
  z-index: 9998;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 64px 72px;
  padding: 56px 48px;
  overflow: hidden;
  pointer-events: none;
  user-select: none;
}

.mini-global-watermark__item {
  display: inline-flex;
  min-width: 220px;
  max-width: 280px;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  color: rgb(15 23 42 / 12%);
  font-size: 15px;
  font-weight: 600;
  letter-spacing: 0;
  line-height: 1.4;
  text-align: center;
  text-overflow: ellipsis;
  text-wrap: nowrap;
  transform: rotate(-24deg);
  white-space: nowrap;
}

.mini-global-watermark.is-dark .mini-global-watermark__item {
  color: rgb(255 255 255 / 13%);
}
</style>
