<script lang="ts" setup>
import type { VbenFormSchema } from '@vben/common-ui';
import type { BasicOption } from '@vben/types';

import { computed, onMounted, ref } from 'vue';

import { AuthenticationLogin, z } from '@vben/common-ui';
import { $t } from '@vben/locales';
import { message } from 'ant-design-vue';

import { getCaptchaApi, getTenantOptionsApi, type AuthApi } from '#/api';
import { useAuthStore } from '#/store';

import BackendCaptchaInput from './backend-captcha-input.vue';

defineOptions({ name: 'Login' });

const authStore = useAuthStore();
const captcha = ref<AuthApi.CaptchaResult>();
const captchaLoading = ref(false);
const captchaRequired = ref(false);
const captchaCode = ref('');

interface LoginOption extends BasicOption {
  password?: string;
  tenantCode?: string;
  username?: string;
}

const DEFAULT_LOGIN_OPTIONS: LoginOption[] = [
  {
    label: 'Admin',
    password: '123456',
    tenantCode: '',
    username: 'admin',
    value: '__platform',
  },
  {
    label: '演示租户(demo)',
    password: '123456',
    tenantCode: 'demo',
    username: 'demo',
    value: 'demo',
  },
];
const loginOptions = ref<LoginOption[]>(DEFAULT_LOGIN_OPTIONS);

const formSchema = computed((): VbenFormSchema[] => {
  const schemas: VbenFormSchema[] = [
    {
      component: 'VbenSelect',
      componentProps: {
        options: loginOptions.value,
        placeholder: '请选择平台或租户',
      },
      fieldName: 'selectAccount',
      label: '登录身份',
      rules: z
        .string()
        .min(1, { message: '请选择平台或租户' })
        .optional()
        .default('__platform'),
    },
    {
      component: 'VbenInput',
      componentProps: {
        placeholder: '平台账号可不填',
      },
      fieldName: 'tenantCode',
      label: '租户编码',
      rules: z.string().optional(),
    },
    {
      component: 'VbenInput',
      componentProps: {
        placeholder: $t('authentication.usernameTip'),
      },
      dependencies: {
        trigger(values, form) {
          if (values.selectAccount) {
            const findUser = loginOptions.value.find(
              (item) => item.value === values.selectAccount,
            );
            if (findUser) {
              form.setValues({
                password: findUser.password ?? '',
                tenantCode: findUser.tenantCode ?? '',
                username: findUser.username ?? '',
              });
            }
          }
        },
        triggerFields: ['selectAccount'],
      },
      fieldName: 'username',
      label: $t('authentication.username'),
      rules: z.string().min(1, { message: $t('authentication.usernameTip') }),
    },
    {
      component: 'VbenInputPassword',
      componentProps: {
        placeholder: $t('authentication.password'),
      },
      fieldName: 'password',
      label: $t('authentication.password'),
      rules: z.string().min(1, { message: $t('authentication.passwordTip') }),
    },
  ];

  return schemas;
});

onMounted(async () => {
  try {
    const tenants = await getTenantOptionsApi();
    const tenantOptions = tenants.map((tenant) => ({
      label: `${tenant.name}(${tenant.code})`,
      password: tenant.code === 'demo' ? '123456' : '',
      tenantCode: tenant.code,
      username: tenant.code === 'demo' ? 'demo' : '',
      value: tenant.code,
    }));

    loginOptions.value = [
      DEFAULT_LOGIN_OPTIONS[0] ?? {
        label: 'Admin',
        password: '123456',
        username: 'admin',
        value: 'admin',
      },
      ...tenantOptions,
    ];
  } catch {
    loginOptions.value = DEFAULT_LOGIN_OPTIONS;
  }
});

async function loadCaptcha() {
  captchaLoading.value = true;
  try {
    captchaCode.value = '';
    captcha.value = await getCaptchaApi();
  } finally {
    captchaLoading.value = false;
  }
}

async function handleLogin(values: Record<string, any>) {
  if (captchaRequired.value && !captchaCode.value.trim()) {
    message.warning('请输入验证码');
    return;
  }

  try {
    await authStore.authLogin({
      ...values,
      captchaId: captchaRequired.value ? captcha.value?.id : undefined,
      captchaCode: captchaRequired.value ? captchaCode.value.trim() : undefined,
    });
  } catch (error: any) {
    const failure = error?.data ?? error?.response?.data?.data;
    if (failure?.captchaRequired) {
      captchaRequired.value = true;
      await loadCaptcha();
    }
  }
}
</script>

<template>
  <AuthenticationLogin
    :form-schema="formSchema"
    :loading="authStore.loginLoading"
    @submit="handleLogin"
  >
    <template #after-form>
      <BackendCaptchaInput
        v-if="captchaRequired"
        v-model="captchaCode"
        class="mb-4"
        :image-src="captcha?.imageBase64"
        :loading="captchaLoading"
        @refresh="loadCaptcha"
      />
    </template>
  </AuthenticationLogin>
</template>
