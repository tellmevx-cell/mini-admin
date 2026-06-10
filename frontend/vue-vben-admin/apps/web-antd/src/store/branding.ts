import { computed, ref } from 'vue';

import { preferences } from '@vben/preferences';

import { defineStore } from 'pinia';

import { getAppBrandingApi, type AppBranding } from '#/api';

const fallbackName = import.meta.env.VITE_APP_TITLE || 'MiniAdmin';

const fallbackBranding: AppBranding = {
  copyright: '',
  loginTitle: `${fallbackName} 企业后台`,
  name: fallbackName,
  shortName: fallbackName,
  watermark: {
    enabled: false,
    text: '',
  },
};

export const useBrandingStore = defineStore('branding', () => {
  const branding = ref<AppBranding>({ ...fallbackBranding });
  const loaded = ref(false);
  const loading = ref(false);

  const name = computed(() => branding.value.name || fallbackBranding.name);
  const shortName = computed(
    () => branding.value.shortName || branding.value.name || fallbackBranding.name,
  );
  const loginTitle = computed(
    () => branding.value.loginTitle || fallbackBranding.loginTitle,
  );
  const watermarkEnabled = computed(
    () => Boolean(branding.value.watermark?.enabled),
  );
  const watermarkText = computed(() => branding.value.watermark?.text?.trim() || '');

  async function loadBranding() {
    if (loading.value) {
      return branding.value;
    }

    loading.value = true;
    try {
      branding.value = await getAppBrandingApi();
    } catch {
      branding.value = { ...fallbackBranding };
    } finally {
      loading.value = false;
      loaded.value = true;
      applyBrandingToPreferences();
    }

    return branding.value;
  }

  function applyBrandingToPreferences() {
    preferences.app.name = name.value;
  }

  function $reset() {
    branding.value = { ...fallbackBranding };
    loaded.value = false;
    loading.value = false;
    applyBrandingToPreferences();
  }

  return {
    $reset,
    applyBrandingToPreferences,
    branding,
    loadBranding,
    loaded,
    loading,
    loginTitle,
    name,
    shortName,
    watermarkEnabled,
    watermarkText,
  };
});
